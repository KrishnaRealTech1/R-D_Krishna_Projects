"""
plate_ocr_service.py v4.3.0
Single clean ANPR service with EV green-number-plate and two-wheeler support.

Pipeline:
  image_url -> YOLO plate detection -> crop -> PaddleOCR -> Indian plate cleanup/validation
  fallback: OpenCV colour masks for yellow, white, and green/EV plates -> PaddleOCR
  two-wheeler: small/square crop support + two-line OCR text joining

Run:
  uvicorn plate_ocr_service:app --host 0.0.0.0 --port 8000
"""

from __future__ import annotations

import asyncio
import collections
import itertools
import logging
import os
import re
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from pathlib import Path
from typing import Any

import cv2
import httpx
import numpy as np
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from paddleocr import PaddleOCR
from PIL import Image
from pydantic import BaseModel, Field
from ultralytics import YOLO

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("anpr")

APP_VERSION = "4.3.0"
DEFAULT_MODEL_PATHS = (
    "/app/models/morsetech-sv1.pt",
    "./models/morsetech-sv1.pt",
    "/app/models/license_plate.pt",
    "./models/license_plate.pt",
)

_executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="anpr")


# ── Model loading ─────────────────────────────────────────────────────────
def _resolve_model_path() -> str:
    configured = os.getenv("YOLO_MODEL_PATH")
    candidates = [configured] if configured else []
    candidates.extend(DEFAULT_MODEL_PATHS)

    for candidate in candidates:
        if candidate and Path(candidate).exists():
            return candidate

    checked = ", ".join(str(p) for p in candidates if p)
    raise FileNotFoundError(
        "YOLO plate model was not found. Set YOLO_MODEL_PATH or place the model at one of: "
        f"{checked}"
    )


def _load_paddle_ocr() -> PaddleOCR:
    """
    PaddleOCR has changed constructor arguments between releases.
    First try the classic v2-style arguments used by the original service;
    if the installed version rejects them, fall back to a minimal constructor.
    """
    try:
        return PaddleOCR(
            use_angle_cls=False,
            lang="en",
            use_gpu=False,
            show_log=False,
        )
    except TypeError:
        log.warning("Installed PaddleOCR rejected legacy args; retrying with minimal args")
        return PaddleOCR(lang="en")


log.info("Loading YOLO plate detector...")
MODEL_PATH = _resolve_model_path()
yolo = YOLO(MODEL_PATH)
yolo.overrides["verbose"] = False
log.info("YOLO ready ✅ model=%s", MODEL_PATH)

log.info("Loading PaddleOCR...")
paddle = _load_paddle_ocr()
log.info("PaddleOCR ready ✅")


# ── Metrics ───────────────────────────────────────────────────────────────
_lock = threading.Lock()
_m: dict[str, Any] = {
    "total": 0,
    "found": 0,
    "not_found": 0,
    "no_box": 0,
    "errors": 0,
    "source_yolo": 0,
    "source_opencv": 0,
    "plate_white": 0,
    "plate_yellow": 0,
    "plate_green_ev": 0,
    "plate_unknown": 0,
    "layout_single_line": 0,
    "layout_two_line": 0,
    "layout_unknown": 0,
    "fetch_ms": collections.deque(maxlen=1000),
    "yolo_ms": collections.deque(maxlen=1000),
    "ocr_ms": collections.deque(maxlen=1000),
    "total_ms": collections.deque(maxlen=1000),
}


def _rec(**kw: Any) -> None:
    with _lock:
        _m["total"] += 1
        if kw.get("error"):
            _m["errors"] += 1
        elif kw.get("no_box"):
            _m["no_box"] += 1
        elif kw.get("found"):
            _m["found"] += 1
        else:
            _m["not_found"] += 1

        src = kw.get("source", "")
        if src == "yolo":
            _m["source_yolo"] += 1
        elif src == "opencv_fallback":
            _m["source_opencv"] += 1

        plate_color = kw.get("plate_color") or "unknown"
        if plate_color in {"white", "yellow", "green_ev"}:
            _m[f"plate_{plate_color}"] += 1
        elif kw.get("found"):
            _m["plate_unknown"] += 1

        plate_layout = kw.get("plate_layout") or "unknown"
        if plate_layout in {"single_line", "two_line"}:
            _m[f"layout_{plate_layout}"] += 1
        elif kw.get("found"):
            _m["layout_unknown"] += 1

        for k in ("fetch_ms", "yolo_ms", "ocr_ms", "total_ms"):
            if k in kw:
                _m[k].append(kw[k])


def _avg(d: collections.deque) -> float:
    return round(sum(d) / len(d), 1) if d else 0


def _p95(d: collections.deque) -> float:
    if not d:
        return 0
    s = sorted(d)
    return round(s[min(int(len(s) * 0.95), len(s) - 1)], 1)


# ── FastAPI ───────────────────────────────────────────────────────────────
app = FastAPI(title="ANPR", version=APP_VERSION)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


class OcrReq(BaseModel):
    image_url: str = Field(..., description="Publicly reachable image URL")


class OcrRes(BaseModel):
    plate: str | None
    confidence: float | None = None
    box: list[int] | None = None
    source: str | None = None
    plate_color: str | None = None
    plate_layout: str | None = None
    timing: dict[str, int] = {}


# ── Indian plate pattern ──────────────────────────────────────────────────
STANDARD_PLATE_RE = re.compile(r"^[A-Z]{2}\d{1,2}[A-Z]{1,3}\d{1,4}$")
BH_PLATE_RE = re.compile(r"^\d{2}BH\d{4}[A-Z]{1,2}$")

# Current and commonly-seen legacy Indian state/UT registration prefixes.
# The regex above is intentionally broad; this set prevents impossible prefixes
# such as RN/IN from being accepted as final plates.
VALID_STATE_CODES = frozenset(
    {
        "AN", "AP", "AR", "AS", "BR", "CG", "CH", "DD", "DL", "DN",
        "GA", "GJ", "HP", "HR", "JH", "JK", "KA", "KL", "LA", "LD",
        "MH", "ML", "MN", "MP", "MZ", "NL", "OD", "OR", "PB", "PY",
        "RJ", "SK", "TN", "TR", "TS", "UK", "UP", "WB",
    }
)

# Bias state-code repair for the deployment region. For Tamil Nadu cameras keep
# the default TN. For multi-state deployment set, for example:
#   PREFERRED_STATE_CODES=TN,KA,KL,AP,TS
PREFERRED_STATE_CODES = tuple(
    code.strip().upper()
    for code in os.getenv("PREFERRED_STATE_CODES", "TN").split(",")
    if code.strip().upper() in VALID_STATE_CODES
)
STATE_CORRECTION_MAX_DIST = float(os.getenv("STATE_CORRECTION_MAX_DIST", "1.05"))


def _clean(t: str) -> str:
    # OCR often inserts punctuation or line separators; remove everything except
    # letters and digits before trying plate-specific validation.
    return re.sub(r"[^A-Z0-9]", "", t.upper().strip())


# Digit zone: OCR letters that should become digits.
_L2D = {
    "O": "0",
    "Q": "0",
    "D": "0",
    "I": "1",
    "L": "1",
    "T": "1",
    "Z": "2",
    "S": "5",
    "B": "8",
    "G": "6",
    "C": "0",
}

# Letter zone: OCR digits that should become letters.
_D2L = {
    "0": "O",
    "1": "I",
    "2": "Z",
    "5": "S",
    "6": "G",
    "8": "B",
}

_LETTER_AMBIGUOUS = {
    "O": ["O", "D", "Q", "C"],
    "I": ["I", "T", "1", "J"],
    "S": ["S", "5"],
    "B": ["B", "8"],
    "G": ["G", "6", "C"],
    "Z": ["Z", "2"],
    "U": ["U", "V"],
    "V": ["V", "U"],
    "M": ["M", "N"],
    "N": ["N", "M"],
    "P": ["P", "R"],
    "R": ["R", "P"],
    "T": ["T", "I", "J", "7"],
    "C": ["C", "G", "O"],
}

# State-code OCR confusions. The R→T entry is intentionally only used during
# invalid-state repair, not general letter zones; it fixes common TN front/rear
# plate reads like RN39... while still requiring a valid final state code.
_STATE_CHAR_CONFUSIONS = {
    "0": {"O", "D", "Q"},
    "1": {"I", "T", "J"},
    "2": {"Z"},
    "5": {"S"},
    "6": {"G", "C"},
    "7": {"T", "Y"},
    "8": {"B"},
    "O": {"0", "D", "Q", "C"},
    "D": {"0", "O", "Q"},
    "Q": {"0", "O", "D"},
    "I": {"1", "T", "J", "L"},
    "L": {"1", "I"},
    "T": {"1", "7", "I", "J", "R"},
    "J": {"I", "T", "1"},
    "R": {"P", "T"},
    "P": {"R"},
    "S": {"5"},
    "B": {"8"},
    "G": {"6", "C"},
    "C": {"G", "O", "0"},
    "Z": {"2"},
    "U": {"V"},
    "V": {"U"},
    "M": {"N"},
    "N": {"M"},
}


def _is_valid_standard_plate(text: str) -> bool:
    return bool(STANDARD_PLATE_RE.match(text)) and text[:2] in VALID_STATE_CODES


def _state_char_cost(src: str, dst: str) -> float:
    if src == dst:
        return 0.0
    if dst in _STATE_CHAR_CONFUSIONS.get(src, set()) or src in _STATE_CHAR_CONFUSIONS.get(dst, set()):
        return 0.25
    return 1.0


def _state_code_distance(src: str, dst: str) -> float:
    if len(src) != 2 or len(dst) != 2:
        return 99.0
    return _state_char_cost(src[0], dst[0]) + _state_char_cost(src[1], dst[1])


def _fix_state_prefix(text: str) -> tuple[str, float]:
    """
    Repair only impossible state prefixes. A valid state prefix is never changed.
    Returns (possibly_repaired_text, penalty).
    """
    if len(text) < 2:
        return text, 99.0

    state = text[:2]
    if state in VALID_STATE_CODES:
        return text, 0.0

    preferred = list(PREFERRED_STATE_CODES)
    fallback = sorted(VALID_STATE_CODES - set(preferred))
    search_space = preferred + fallback

    best_state = state
    best_dist = 99.0
    for candidate in search_space:
        dist = _state_code_distance(state, candidate)
        # Prefer states listed earlier in PREFERRED_STATE_CODES when tied.
        if dist < best_dist:
            best_state = candidate
            best_dist = dist

    if best_dist <= STATE_CORRECTION_MAX_DIST:
        fixed = best_state + text[2:]
        log.info("  fix state prefix: %r → %r penalty=%.2f", text, fixed, best_dist)
        return fixed, best_dist

    return text, 99.0


def _apply_zone_fix(chars: list[str], series_len: int) -> str:
    """
    Standard Indian plate zones:
      0-1                 state letters
      2-3                 district digits
      4..4+series_len-1   series letters
      rest                registration number digits
    """
    r = chars[:]
    digit_start = 4 + series_len

    for i in range(min(2, len(r))):
        if r[i] in _D2L:
            r[i] = _D2L[r[i]]

    for i in range(2, min(4, len(r))):
        if r[i] in _L2D:
            r[i] = _L2D[r[i]]

    for i in range(4, min(digit_start, len(r))):
        if r[i] in _D2L:
            r[i] = _D2L[r[i]]

    for i in range(digit_start, len(r)):
        if r[i] in _L2D:
            r[i] = _L2D[r[i]]

    return "".join(r)


def _fix_standard_plate_chars(text: str) -> str:
    text = _clean(text)
    if len(text) < 6 or len(text) > 11:
        return text

    chars = list(text)

    for series_len in [1, 2, 3]:
        digit_start = 4 + series_len
        if digit_start >= len(chars):
            continue
        result = _apply_zone_fix(chars, series_len)
        if STANDARD_PLATE_RE.match(result):
            log.info("  fix standard P1: %r → %r series=%s", text, result, series_len)
            return result

    for series_len in [1, 2, 3]:
        digit_start = 4 + series_len
        if digit_start >= len(chars):
            continue

        base = list(_apply_zone_fix(chars, series_len))
        ambig_pos = [
            i for i in range(4, min(digit_start, len(base)))
            if base[i] in _LETTER_AMBIGUOUS
        ]

        if not ambig_pos or len(ambig_pos) > 3:
            continue

        choices = [_LETTER_AMBIGUOUS[base[i]] for i in ambig_pos]
        for combo in itertools.product(*choices):
            trial = base[:]
            for pos, ch in zip(ambig_pos, combo):
                trial[pos] = ch
            for i in range(digit_start, len(trial)):
                if trial[i] in _L2D:
                    trial[i] = _L2D[trial[i]]
            result = "".join(trial)
            if STANDARD_PLATE_RE.match(result):
                log.info("  fix standard P2: %r → %r series=%s", text, result, series_len)
                return result

    result = _apply_zone_fix(chars, 2)
    log.info("  fix standard fallback: %r → %r", text, result)
    return result


def _fix_bh_plate_chars(text: str) -> str:
    """
    Optional Bharat-series support, e.g. 22BH1234AA.
    This is useful because some EVs can also use BH registration.
    """
    text = _clean(text)
    if len(text) < 9 or len(text) > 10:
        return text

    r = list(text)

    # YY
    for i in range(0, min(2, len(r))):
        if r[i] in _L2D:
            r[i] = _L2D[r[i]]

    # BH
    if len(r) >= 4:
        if r[2] in _D2L:
            r[2] = _D2L[r[2]]
        if r[3] in _D2L:
            r[3] = _D2L[r[3]]

    # ####
    for i in range(4, min(8, len(r))):
        if r[i] in _L2D:
            r[i] = _L2D[r[i]]

    # trailing letters
    for i in range(8, len(r)):
        if r[i] in _D2L:
            r[i] = _D2L[r[i]]

    result = "".join(r)
    if BH_PLATE_RE.match(result):
        log.info("  fix BH: %r → %r", text, result)
    return result


def _change_penalty(before: str, after: str) -> float:
    if len(before) != len(after):
        return 0.20
    changes = sum(1 for a, b in zip(before, after) if a != b)
    return changes * 0.14


def _text_variants(text: str) -> list[tuple[str, float]]:
    """
    Generate low-risk OCR variants. A one-character deletion is kept as a
    fallback for tilted two-wheeler plates, but it has a higher penalty so a
    direct valid read still wins.
    """
    cleaned = _clean(text)
    if not cleaned:
        return []

    seen: dict[str, float] = {}

    def add(candidate: str, penalty: float) -> None:
        if 6 <= len(candidate) <= 11 and (candidate not in seen or penalty < seen[candidate]):
            seen[candidate] = penalty

    add(cleaned, 0.0)

    # Generic one-character deletion is useful when OCR inserts noise, but the
    # penalty prevents it from changing TN39OI6977 into TN90I6977. Use plate ROI
    # detection/cropping to fix those cases rather than aggressive text guessing.
    if 8 <= len(cleaned) <= 12:
        for i in range(len(cleaned)):
            add(cleaned[:i] + cleaned[i + 1:], 0.35)

    return sorted(seen.items(), key=lambda item: (item[1], item[0]))


def _add_validation_candidate(
    candidates: dict[str, float],
    plate: str,
    penalty: float,
) -> None:
    if plate not in candidates or penalty < candidates[plate]:
        candidates[plate] = penalty


def _validate_with_penalty(text: str) -> tuple[str, float] | None:
    candidates: dict[str, float] = {}

    for variant, variant_penalty in _text_variants(text):
        if _is_valid_standard_plate(variant):
            _add_validation_candidate(candidates, variant, variant_penalty)

        fixed_standard = _fix_standard_plate_chars(variant)
        fixed_state, state_penalty = _fix_state_prefix(fixed_standard)
        if _is_valid_standard_plate(fixed_state):
            total_penalty = variant_penalty + state_penalty + _change_penalty(variant, fixed_standard)
            _add_validation_candidate(candidates, fixed_state, total_penalty)

        fixed_bh = _fix_bh_plate_chars(variant)
        if BH_PLATE_RE.match(fixed_bh):
            total_penalty = variant_penalty + _change_penalty(variant, fixed_bh)
            _add_validation_candidate(candidates, fixed_bh, total_penalty)

    if not candidates:
        return None

    best_plate = min(candidates, key=lambda p: (candidates[p], 0 if p[:2] in PREFERRED_STATE_CODES else 1, p))
    return best_plate, candidates[best_plate]


def _validate(text: str) -> str | None:
    validated = _validate_with_penalty(text)
    return validated[0] if validated else None


# ── Plate colour detection and masks ──────────────────────────────────────
YELLOW_LOW = np.array([15, 80, 80])
YELLOW_HIGH = np.array([35, 255, 255])
WHITE_LOW = np.array([0, 0, 180])
WHITE_HIGH = np.array([180, 55, 255])
GREEN_LOW = np.array([35, 45, 45])
GREEN_HIGH = np.array([95, 255, 255])


def _mask_ratio(hsv: np.ndarray, lower: np.ndarray, upper: np.ndarray) -> float:
    mask = cv2.inRange(hsv, lower, upper)
    return float(cv2.countNonZero(mask)) / float(mask.size or 1)


def _infer_plate_color(crop_bgr: np.ndarray) -> str | None:
    """
    Infer plate background colour from the crop.
    green_ev means a green-background EV plate.
    """
    if crop_bgr.size == 0:
        return None

    hsv = cv2.cvtColor(crop_bgr, cv2.COLOR_BGR2HSV)
    ratios = {
        "green_ev": _mask_ratio(hsv, GREEN_LOW, GREEN_HIGH),
        "yellow": _mask_ratio(hsv, YELLOW_LOW, YELLOW_HIGH),
        "white": _mask_ratio(hsv, WHITE_LOW, WHITE_HIGH),
    }
    color, ratio = max(ratios.items(), key=lambda item: item[1])
    return color if ratio >= 0.08 else None


# ── Image enhancement ─────────────────────────────────────────────────────
def _enhance(img_rgb: np.ndarray) -> np.ndarray:
    h, w = img_rgb.shape[:2]
    img = img_rgb

    if h < 40:
        img = cv2.resize(img, (w * 2, h * 2), interpolation=cv2.INTER_LANCZOS4)
        h2, w2 = img.shape[:2]
        img = cv2.resize(img, (w2 * 2, h2 * 2), interpolation=cv2.INTER_CUBIC)
        sharp_k = np.array([[0, -1, 0], [-1, 6, -1], [0, -1, 0]])
        img = cv2.filter2D(img, -1, sharp_k)
    elif h < 60:
        scale = max(60 / h, 2.5)
        img = cv2.resize(img, (int(w * scale), int(h * scale)), interpolation=cv2.INTER_LANCZOS4)
    elif h < 120:
        scale = 120 / h
        img = cv2.resize(img, (int(w * scale), int(h * scale)), interpolation=cv2.INTER_CUBIC)

    lab = cv2.cvtColor(img, cv2.COLOR_RGB2LAB)
    l, a, b = cv2.split(lab)
    l = cv2.createCLAHE(clipLimit=3.0, tileGridSize=(4, 4)).apply(l)
    img = cv2.cvtColor(cv2.merge([l, a, b]), cv2.COLOR_LAB2RGB)
    img = cv2.filter2D(img, -1, np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]]))
    return img


def _multi_scale_crop(crop_bgr: np.ndarray) -> list[tuple[str, np.ndarray]]:
    h, w = crop_bgr.shape[:2]
    scales = [
        ("100%", crop_bgr),
        ("150%", cv2.resize(crop_bgr, (int(w * 1.5), int(h * 1.5)), interpolation=cv2.INTER_LANCZOS4)),
        ("200%", cv2.resize(crop_bgr, (w * 2, h * 2), interpolation=cv2.INTER_LANCZOS4)),
    ]
    if h < 55 or w < 160:
        scales.append(("300%", cv2.resize(crop_bgr, (w * 3, h * 3), interpolation=cv2.INTER_LANCZOS4)))
    return [(label, cv2.cvtColor(img, cv2.COLOR_BGR2RGB)) for label, img in scales]


# ── OCR ───────────────────────────────────────────────────────────────────
def _ocr_crop(crop_bgr: np.ndarray) -> list[tuple[str, float, str]]:
    results: list[tuple[str, float, str]] = []

    crop_rgb = cv2.cvtColor(crop_bgr, cv2.COLOR_BGR2RGB)
    enhanced_rgb = _enhance(crop_rgb.copy())

    h, w = crop_bgr.shape[:2]
    log.info("  OCR crop size: %sx%s px", w, h)

    def _line_center(line: Any) -> tuple[float, float]:
        try:
            pts = line[0]
            xs = [float(p[0]) for p in pts]
            ys = [float(p[1]) for p in pts]
            return sum(ys) / len(ys), sum(xs) / len(xs)
        except Exception:
            return 0.0, 0.0

    def _run_paddle(img: np.ndarray, label: str) -> None:
        try:
            res = paddle.ocr(img, cls=False)
            if not res or not res[0]:
                return

            run_lines: list[tuple[str, float, float, float]] = []
            for line in res[0]:
                if line and len(line) >= 2:
                    txt, conf = line[1][0], float(line[1][1])
                    cy, cx = _line_center(line)
                    log.info("  OCR %s: %r conf=%.2f", label, txt, conf)
                    results.append((txt, conf, "single_line"))
                    run_lines.append((txt, conf, cy, cx))

            # Two-wheeler plates are often split over two lines, for example:
            #   TN37
            #   DQ7734
            # PaddleOCR returns those as two separate strings, so join lines in
            # visual reading order and validate the combined value too.
            if len(run_lines) >= 2:
                ordered = sorted(run_lines, key=lambda item: (item[2], item[3]))
                joined = "".join(item[0] for item in ordered)
                joined_conf = min(item[1] for item in ordered)
                log.info("  OCR %s joined-two-line: %r conf=%.2f", label, joined, joined_conf)
                results.append((joined, joined_conf, "two_line"))

                # Some OCR outputs create 3 small fragments; keep this as a safe
                # extra candidate because validation still filters false joins.
                if len(ordered) >= 3:
                    for start in range(0, len(ordered) - 1):
                        pair = ordered[start:start + 2]
                        pair_joined = "".join(item[0] for item in pair)
                        pair_conf = min(item[1] for item in pair)
                        results.append((pair_joined, pair_conf, "two_line"))
        except Exception as e:
            log.warning("OCR %s failed: %s", label, e)

    for label, img_rgb in _multi_scale_crop(crop_bgr):
        enhanced_scaled = _enhance(img_rgb.copy())
        _run_paddle(enhanced_scaled, f"enhanced-{label}")
    if _best_plate(results)[0]:
        return results

    gray = cv2.cvtColor(enhanced_rgb, cv2.COLOR_RGB2GRAY)
    _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    _run_paddle(thresh, "otsu")
    _run_paddle(cv2.bitwise_not(thresh), "otsu-inv")
    if _best_plate(results)[0]:
        return results

    for label, img_rgb in _multi_scale_crop(crop_bgr):
        _run_paddle(img_rgb, f"raw-{label}")

    return results


def _best_plate(texts: list[tuple[str, float, str]]) -> tuple[str | None, float | None, str | None]:
    # Rank by OCR confidence minus validation penalty. This prevents an
    # impossible but high-confidence value like RN39... from beating a corrected
    # valid-state candidate like TN39...
    seen: dict[str, tuple[float, float, str]] = {}
    for txt, conf, layout in texts:
        validated = _validate_with_penalty(txt)
        if not validated:
            continue

        plate, penalty = validated
        rank_score = conf - penalty
        reported_conf = max(0.0, min(1.0, conf - min(penalty * 0.12, 0.22)))
        if plate not in seen or rank_score > seen[plate][0]:
            seen[plate] = (rank_score, reported_conf, layout)

    if not seen:
        return None, None, None

    best = max(seen, key=lambda k: seen[k][0])
    _, best_conf, best_layout = seen[best]
    return best, round(best_conf, 3), best_layout


# ── OpenCV colour-mask fallback ───────────────────────────────────────────
def _mask_candidates(
    img_bgr: np.ndarray,
    hsv_lower: np.ndarray,
    hsv_upper: np.ndarray,
    label: str,
) -> list[tuple[np.ndarray, list[int], int, str]]:
    hsv = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2HSV)
    mask = cv2.inRange(hsv, hsv_lower, hsv_upper)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))

    cnts, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    ih, iw = img_bgr.shape[:2]
    image_area = max(iw * ih, 1)
    min_area = max(180, int(image_area * 0.00005))
    max_area = min(180000, int(image_area * 0.18))
    cands: list[tuple[np.ndarray, list[int], int, str]] = []

    for c in cnts:
        x, y, w, h = cv2.boundingRect(c)
        ar = w / max(h, 1)
        area = w * h
        extent = cv2.contourArea(c) / max(area, 1)

        # Four-wheeler plates are usually wide single-line rectangles.
        # Two-wheeler plates can be smaller and nearly square because the text
        # is often stacked over two lines. OCR validation remains the final gate.
        is_wide_single_line = 1.8 < ar < 7.8
        is_two_wheeler_two_line = 0.55 < ar <= 1.8
        is_reasonable_size = min_area < area < max_area and w >= 18 and h >= 14
        is_filled_plate_patch = extent >= 0.25

        if (is_wide_single_line or is_two_wheeler_two_line) and is_reasonable_size and is_filled_plate_patch:
            pad = max(6, int(max(w, h) * 0.10))
            crop = img_bgr[
                max(0, y - pad):min(ih, y + h + pad),
                max(0, x - pad):min(iw, x + w + pad),
            ]
            layout_hint = "two_line" if is_two_wheeler_two_line else "single_line"
            priority = int(area * (1.15 if layout_hint == "two_line" else 1.0))
            log.info(
                "  %s candidate: (%s,%s) %sx%s area=%s ar=%.2f layout_hint=%s",
                label,
                x,
                y,
                w,
                h,
                area,
                ar,
                layout_hint,
            )
            cands.append((crop, [x, y, x + w, y + h], priority, label))

    return sorted(cands, key=lambda item: -item[2])


def _opencv_fallback(img_bgr: np.ndarray) -> list[tuple[np.ndarray, list[int], int, str]]:
    """
    Colour fallback masks:
      yellow   → commercial vehicle plates
      white    → private vehicle plates
      green_ev → electric vehicle green plates
    """
    yellow = _mask_candidates(img_bgr, YELLOW_LOW, YELLOW_HIGH, "yellow")
    white = _mask_candidates(img_bgr, WHITE_LOW, WHITE_HIGH, "white")
    green = _mask_candidates(img_bgr, GREEN_LOW, GREEN_HIGH, "green_ev")
    combined = yellow + white + green
    return sorted(combined, key=lambda item: -item[2])


def _edge_candidates(img_bgr: np.ndarray, label: str = "edge") -> list[tuple[np.ndarray, list[int], int, str]]:
    """
    Plate candidate finder that does not rely on background colour. It searches
    for dense horizontal text/edge regions, which helps when the YOLO box is a
    vehicle box rather than a plate box.
    """
    ih, iw = img_bgr.shape[:2]
    if ih < 20 or iw < 20:
        return []

    gray = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2GRAY)
    gray = cv2.bilateralFilter(gray, 7, 60, 60)

    rect_kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (13, 5))
    blackhat = cv2.morphologyEx(gray, cv2.MORPH_BLACKHAT, rect_kernel)

    grad_x = cv2.Sobel(blackhat, ddepth=cv2.CV_32F, dx=1, dy=0, ksize=-1)
    grad_x = np.absolute(grad_x)
    min_val, max_val = float(np.min(grad_x)), float(np.max(grad_x))
    if max_val - min_val < 1e-6:
        return []
    grad_x = ((grad_x - min_val) / (max_val - min_val) * 255).astype("uint8")

    grad_x = cv2.GaussianBlur(grad_x, (3, 3), 0)
    grad_x = cv2.morphologyEx(grad_x, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_RECT, (17, 3)))
    _, thresh = cv2.threshold(grad_x, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    thresh = cv2.morphologyEx(thresh, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_RECT, (15, 5)))
    thresh = cv2.erode(thresh, None, iterations=1)
    thresh = cv2.dilate(thresh, None, iterations=1)

    cnts, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    image_area = max(iw * ih, 1)
    min_area = max(160, int(image_area * 0.00004))
    max_area = min(180000, int(image_area * 0.20))
    cands: list[tuple[np.ndarray, list[int], int, str]] = []

    for c in cnts:
        x, y, w, h = cv2.boundingRect(c)
        area = w * h
        ar = w / max(h, 1)
        density = cv2.countNonZero(thresh[y:y + h, x:x + w]) / float(area or 1)

        is_wide_single_line = 1.65 < ar < 8.5
        is_two_wheeler_two_line = 0.50 < ar <= 1.65 and h >= 20
        is_reasonable_size = min_area < area < max_area and w >= 20 and h >= 14
        is_text_dense = 0.06 <= density <= 0.72

        if (is_wide_single_line or is_two_wheeler_two_line) and is_reasonable_size and is_text_dense:
            pad = max(6, int(max(w, h) * 0.12))
            crop = img_bgr[
                max(0, y - pad):min(ih, y + h + pad),
                max(0, x - pad):min(iw, x + w + pad),
            ]
            priority = int(area * (1.0 + density) * (1.15 if is_two_wheeler_two_line else 1.0))
            log.info(
                "  %s candidate: (%s,%s) %sx%s area=%s ar=%.2f density=%.2f",
                label,
                x,
                y,
                w,
                h,
                area,
                ar,
                density,
            )
            cands.append((crop, [x, y, x + w, y + h], priority, label))

    return sorted(cands, key=lambda item: -item[2])


def _box_iou(a: list[int], b: list[int]) -> float:
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b
    ix1, iy1 = max(ax1, bx1), max(ay1, by1)
    ix2, iy2 = min(ax2, bx2), min(ay2, by2)
    iw, ih = max(0, ix2 - ix1), max(0, iy2 - iy1)
    inter = iw * ih
    area_a = max(0, ax2 - ax1) * max(0, ay2 - ay1)
    area_b = max(0, bx2 - bx1) * max(0, by2 - by1)
    union = area_a + area_b - inter
    return inter / float(union or 1)


def _dedupe_candidates(
    candidates: list[tuple[np.ndarray, list[int], int, str]],
    iou_threshold: float = 0.55,
) -> list[tuple[np.ndarray, list[int], int, str]]:
    kept: list[tuple[np.ndarray, list[int], int, str]] = []
    for candidate in sorted(candidates, key=lambda item: -item[2]):
        _, box, _, _ = candidate
        if all(_box_iou(box, kept_box) < iou_threshold for _, kept_box, _, _ in kept):
            kept.append(candidate)
    return kept


def _plate_candidates_in_region(img_bgr: np.ndarray) -> list[tuple[np.ndarray, list[int], int, str]]:
    color_candidates = _opencv_fallback(img_bgr)
    edge_candidates = _edge_candidates(img_bgr)
    return _dedupe_candidates(color_candidates + edge_candidates)


# ── Core pipeline ─────────────────────────────────────────────────────────
def _pipeline(img_bgr: np.ndarray) -> dict[str, Any]:
    result: dict[str, Any] = {
        "plate": None,
        "confidence": None,
        "box": None,
        "source": None,
        "plate_color": None,
        "plate_layout": None,
        "yolo_ms": 0,
        "ocr_ms": 0,
    }
    ih, iw = img_bgr.shape[:2]

    t0 = time.perf_counter()

    def _yolo_pass(conf_thresh: float, augment: bool, label: str):
        log.info("YOLO %s: conf=%s augment=%s", label, conf_thresh, augment)
        return yolo.predict(
            img_bgr,
            imgsz=1280,
            conf=conf_thresh,
            iou=0.45,
            augment=augment,
            verbose=False,
        )

    dets_a = _yolo_pass(0.25, False, "PassA")
    boxes_a = dets_a[0].boxes if dets_a else None
    result["yolo_ms"] = int((time.perf_counter() - t0) * 1000)

    def _try_boxes(boxes: Any, ocr_timer_ref: list[int]) -> bool:
        if boxes is None or len(boxes) == 0:
            return False

        sorted_idx = boxes.conf.argsort(descending=True).tolist()
        for idx in sorted_idx[:5]:
            yconf = float(boxes.conf[idx])
            x1, y1, x2, y2 = map(int, boxes.xyxy[idx].tolist())
            log.info("  box=(%s,%s,%s,%s) yolo_conf=%.2f", x1, y1, x2, y2, yconf)

            pad = max(8, int((y2 - y1) * 0.12))
            crop_x1 = max(0, x1 - pad)
            crop_y1 = max(0, y1 - pad)
            crop_x2 = min(iw, x2 + pad)
            crop_y2 = min(ih, y2 + pad)
            crop = img_bgr[crop_y1:crop_y2, crop_x1:crop_x2]

            # When the model returns a vehicle/front/rear box instead of a tight
            # plate box, find smaller plate-like ROIs inside that box first.
            # OCR on a small ROI is both faster and less likely to pick up body
            # panels, road text, stickers, or rider clothing.
            roi_runs: list[tuple[np.ndarray, list[int], str | None, str]] = []
            for roi_crop, local_box, _, color_hint in _plate_candidates_in_region(crop)[:4]:
                lx1, ly1, lx2, ly2 = local_box
                global_box = [crop_x1 + lx1, crop_y1 + ly1, crop_x1 + lx2, crop_y1 + ly2]
                roi_runs.append((roi_crop, global_box, color_hint, f"roi-{color_hint}"))

            # Keep the original YOLO crop as a fallback for cases where the ROI
            # extractor misses a low-contrast or very dirty plate.
            roi_runs.append((crop, [x1, y1, x2, y2], None, "full-yolo-crop"))

            for ocr_crop, ocr_box, color_hint, ocr_label in roi_runs:
                log.info("  trying %s box=%s", ocr_label, ocr_box)
                t_ocr = time.perf_counter()
                texts = _ocr_crop(ocr_crop)
                ocr_timer_ref[0] += int((time.perf_counter() - t_ocr) * 1000)

                plate, conf, layout = _best_plate(texts)
                if plate:
                    result.update(
                        plate=plate,
                        confidence=conf,
                        box=ocr_box,
                        source="yolo",
                        plate_color=color_hint if color_hint in {"white", "yellow", "green_ev"} else _infer_plate_color(ocr_crop),
                        plate_layout=layout,
                    )
                    return True
        return False

    ocr_ms_acc = [0]

    if _try_boxes(boxes_a, ocr_ms_acc):
        result["ocr_ms"] = ocr_ms_acc[0]
        return result

    log.info("Pass A found no valid plate — running Pass B")
    t1 = time.perf_counter()
    dets_b = _yolo_pass(0.12, True, "PassB")
    boxes_b = dets_b[0].boxes if dets_b else None
    result["yolo_ms"] += int((time.perf_counter() - t1) * 1000)

    if _try_boxes(boxes_b, ocr_ms_acc):
        result["ocr_ms"] = ocr_ms_acc[0]
        return result

    result["ocr_ms"] = ocr_ms_acc[0]
    log.info("YOLO found no valid plate — trying OpenCV colour fallback")

    cands = _plate_candidates_in_region(img_bgr)
    log.info("  OpenCV/edge found %s candidate region(s)", len(cands))

    t2 = time.perf_counter()
    for crop, box, _, color in cands[:5]:
        texts = _ocr_crop(crop)
        plate, conf, layout = _best_plate(texts)
        if plate:
            result["ocr_ms"] += int((time.perf_counter() - t2) * 1000)
            result.update(
                plate=plate,
                confidence=conf,
                box=box,
                source="opencv_fallback",
                plate_color=color if color in {"white", "yellow", "green_ev"} else _infer_plate_color(crop),
                plate_layout=layout,
            )
            return result

    result["ocr_ms"] += int((time.perf_counter() - t2) * 1000)
    log.info("No plate found by any method")
    return result


# ── Routes ────────────────────────────────────────────────────────────────
@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "version": APP_VERSION,
        "models": {
            "yolo": MODEL_PATH,
            "ocr": "PaddleOCR",
        },
        "supported_vehicle_types": ["four_wheeler", "two_wheeler"],
        "supported_plate_colours": ["white", "yellow", "green_ev"],
        "supported_plate_layouts": ["single_line", "two_line"],
        "supported_formats": ["standard_indian", "bharat_bh"],
    }


@app.get("/metrics")
def metrics() -> dict[str, Any]:
    with _lock:
        total = _m["total"] or 1
        return {
            "summary": {
                "total": _m["total"],
                "found": _m["found"],
                "not_found": _m["not_found"],
                "no_box": _m["no_box"],
                "errors": _m["errors"],
                "detection_rate": f"{round(_m['found'] / total * 100, 1)}%",
                "source_breakdown": {
                    "yolo": _m["source_yolo"],
                    "opencv_fallback": _m["source_opencv"],
                },
                "plate_colour_breakdown": {
                    "white": _m["plate_white"],
                    "yellow": _m["plate_yellow"],
                    "green_ev": _m["plate_green_ev"],
                    "unknown": _m["plate_unknown"],
                },
                "plate_layout_breakdown": {
                    "single_line": _m["layout_single_line"],
                    "two_line": _m["layout_two_line"],
                    "unknown": _m["layout_unknown"],
                },
            },
            "latency_ms": {
                "window": len(_m["total_ms"]),
                "fetch": {"avg": _avg(_m["fetch_ms"]), "p95": _p95(_m["fetch_ms"])},
                "yolo": {"avg": _avg(_m["yolo_ms"]), "p95": _p95(_m["yolo_ms"])},
                "ocr": {"avg": _avg(_m["ocr_ms"]), "p95": _p95(_m["ocr_ms"])},
                "total": {"avg": _avg(_m["total_ms"]), "p95": _p95(_m["total_ms"])},
            },
        }


@app.post("/ocr", response_model=OcrRes)
async def ocr_plate(req: OcrReq) -> OcrRes:
    t0 = time.perf_counter()

    tf = time.perf_counter()
    try:
        async with httpx.AsyncClient(timeout=15.0, follow_redirects=True) as c:
            r = await c.get(req.image_url)
            r.raise_for_status()

        img = cv2.cvtColor(
            np.array(Image.open(BytesIO(r.content)).convert("RGB")),
            cv2.COLOR_RGB2BGR,
        )
        log.info("Fetched %sx%s", img.shape[1], img.shape[0])
    except Exception as e:
        log.warning("Fetch error: %s", e)
        ms = int((time.perf_counter() - t0) * 1000)
        _rec(error=True, total_ms=ms)
        return OcrRes(plate=None, timing={"total_ms": ms})

    fetch_ms = int((time.perf_counter() - tf) * 1000)

    try:
        loop = asyncio.get_running_loop()
        res = await loop.run_in_executor(_executor, _pipeline, img)
    except Exception as e:
        log.error("Pipeline error: %s", e, exc_info=True)
        ms = int((time.perf_counter() - t0) * 1000)
        _rec(error=True, fetch_ms=fetch_ms, total_ms=ms)
        return OcrRes(plate=None, timing={"fetch_ms": fetch_ms, "total_ms": ms})

    total_ms = int((time.perf_counter() - t0) * 1000)
    _rec(
        found=res["plate"] is not None,
        no_box=res["box"] is None,
        source=res["source"],
        plate_color=res["plate_color"],
        plate_layout=res["plate_layout"],
        fetch_ms=fetch_ms,
        yolo_ms=res["yolo_ms"],
        ocr_ms=res["ocr_ms"],
        total_ms=total_ms,
    )

    timing = {
        "fetch_ms": fetch_ms,
        "yolo_ms": res["yolo_ms"],
        "ocr_ms": res["ocr_ms"],
        "total_ms": total_ms,
    }
    log.info(
        "RESULT plate=%r source=%s color=%s layout=%s fetch=%sms yolo=%sms ocr=%sms total=%sms",
        res["plate"],
        res["source"],
        res["plate_color"],
        res["plate_layout"],
        fetch_ms,
        res["yolo_ms"],
        res["ocr_ms"],
        total_ms,
    )

    return OcrRes(
        plate=res["plate"],
        confidence=res["confidence"],
        box=res["box"],
        source=res["source"],
        plate_color=res["plate_color"],
        plate_layout=res["plate_layout"],
        timing=timing,
    )
