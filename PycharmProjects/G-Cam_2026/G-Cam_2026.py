cat > /tmp/gcam_install.sh <<'BASH'
#!/usr/bin/env bash
set -e

APP_DIR="/opt/gcam"
VENV_DIR="$APP_DIR/venv"
SERVICE_FILE="/etc/systemd/system/gcam.service"
RTSP_URL="rtsp://admin:admin@192.168.22.225:554/h264/ch1/main/av_stream"
PI_IP="192.168.22.101"
CAMERA_IP="192.168.22.225"
ROUTER_IP="192.168.22.1"

echo "=============================="
echo " G-Cam RealTech Systems Setup "
echo "=============================="

sudo apt-get update
sudo apt-get install -y \
    python3 python3-venv python3-pip python3-dev \
    tesseract-ocr tesseract-ocr-eng \
    ffmpeg alsa-utils espeak-ng curl wget git \
    libopenblas-dev libatlas-base-dev \
    libglib2.0-0 libgl1 libjpeg62-turbo libtiff6 libopenjp2-7

sudo mkdir -p "$APP_DIR"/{assets,data,logs,models,static/captures,templates}
sudo chown -R "$USER":"$USER" "$APP_DIR"

python3 -m venv "$VENV_DIR"
"$VENV_DIR/bin/pip" install --upgrade pip wheel setuptools
"$VENV_DIR/bin/pip" install \
    flask==3.0.3 \
    waitress==3.0.0 \
    numpy==1.26.4 \
    opencv-python-headless==4.10.0.84 \
    pytesseract==0.3.10

echo "Downloading detection models..."
cd "$APP_DIR/models"

# MobileNet SSD (lightweight for Pi)
wget -O MobileNetSSD_deploy.prototxt \
  https://raw.githubusercontent.com/chuanqi305/MobileNet-SSD/master/deploy.prototxt

wget -O MobileNetSSD_deploy.caffemodel \
  https://github.com/chuanqi305/MobileNet-SSD/raw/master/mobilenet_iter_73000.caffemodel

# OpenCV Haar cascade for plates
wget -O haarcascade_russian_plate_number.xml \
  https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_russian_plate_number.xml

echo "Creating default warning audio..."
espeak-ng -w "$APP_DIR/assets/warning.wav" "Warning. Person detected. Please move away from the protected area."

echo "Setting USB audio as default when card 1 exists..."
if aplay -l | grep -q "card 1"; then
  sudo bash -c 'cat > /etc/asound.conf <<EOF
defaults.pcm.card 1
defaults.ctl.card 1
EOF'
fi

cat > "$APP_DIR/app.py" <<'PY'
import os
import re
import cv2
import json
import time
import queue
import atexit
import shutil
import threading
import datetime as dt
import subprocess
from pathlib import Path
from typing import Dict, Any, Optional, Tuple, List

import numpy as np
import pytesseract
from flask import Flask, Response, jsonify, render_template, send_from_directory
from waitress import serve

APP_DIR = Path("/opt/gcam")
DATA_DIR = APP_DIR / "data"
STATIC_DIR = APP_DIR / "static"
CAPTURE_DIR = STATIC_DIR / "captures"
MODEL_DIR = APP_DIR / "models"
ASSET_DIR = APP_DIR / "assets"
LOG_DIR = APP_DIR / "logs"

RTSP_URL = "rtsp://admin:admin@192.168.22.225:554/h264/ch1/main/av_stream"
HOST = "0.0.0.0"
PORT = 8080

STATE_FILE = DATA_DIR / "state.json"
LOCK = threading.Lock()

FRAME_JPEG_QUALITY = 80
PERSON_CONFIDENCE = 0.45
BOTTLE_CONFIDENCE = 0.35
PERSON_COOLDOWN_SEC = 12
PLATE_COOLDOWN_SEC = 10
AUDIO_COOLDOWN_SEC = 8
GARBAGE_INTERVAL_SEC = 30 * 60
GARBAGE_BOOT_DELAY_SEC = 45

# Tune these if needed after first run
CLUTTER_LOW_THRESHOLD = 0.030
CLUTTER_MEDIUM_THRESHOLD = 0.065
CLUTTER_HIGH_THRESHOLD = 0.100

# If you want to limit garbage analysis to a specific part of the frame,
# set this to values like (x1_ratio, y1_ratio, x2_ratio, y2_ratio)
# Example bottom-half ROI: (0.0, 0.45, 1.0, 1.0)
GARBAGE_ROI = (0.0, 0.45, 1.0, 1.0)

MOBILENET_CLASSES = [
    "background", "aeroplane", "bicycle", "bird", "boat",
    "bottle", "bus", "car", "cat", "chair", "cow", "diningtable",
    "dog", "horse", "motorbike", "person", "pottedplant", "sheep",
    "sofa", "train", "tvmonitor"
]

# Practical garbage-like classes available in this lightweight model.
GARBAGE_OBJECT_CLASSES = {"bottle", "chair", "sofa", "pottedplant"}

app = Flask(__name__, template_folder=str(APP_DIR / "templates"), static_folder=str(STATIC_DIR))

latest_frame = None
latest_frame_ts = 0.0
frame_lock = threading.Lock()
stop_event = threading.Event()
audio_lock = threading.Lock()

net = None
plate_cascade = None


def now_str() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def safe_write_json(path: Path, payload: Dict[str, Any]) -> None:
    tmp = path.with_suffix(".tmp")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
    tmp.replace(path)


def default_state() -> Dict[str, Any]:
    return {
        "app_name": 'G-Cam "RealTech Systems"',
        "started_at": now_str(),
        "camera_ip": "192.168.22.225",
        "router_ip": "192.168.22.1",
        "raspberry_ip": "192.168.22.101",
        "rtsp_url": RTSP_URL,
        "system": {
            "camera_connected": False,
            "last_frame_at": None,
            "last_error": None
        },
        "counts": {
            "HIGH": 0,
            "MEDIUM": 0,
            "LOW": 0
        },
        "last_person": {
            "detected": False,
            "time": None,
            "image": None
        },
        "last_plate": {
            "detected": False,
            "time": None,
            "image": None,
            "plate_number": None
        },
        "last_garbage": {
            "detected": False,
            "time": None,
            "image": None,
            "severity": None,
            "score": 0.0,
            "objects": []
        },
        "events": []
    }


def load_state() -> Dict[str, Any]:
    if not STATE_FILE.exists():
        state = default_state()
        safe_write_json(STATE_FILE, state)
        return state
    with open(STATE_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def update_state(mutator):
    with LOCK:
        state = load_state()
        mutator(state)
        safe_write_json(STATE_FILE, state)


def add_event(event_type: str, details: Dict[str, Any]) -> None:
    def _mutate(state):
        state["events"].insert(0, {
            "type": event_type,
            "time": now_str(),
            **details
        })
        state["events"] = state["events"][:50]
    update_state(_mutate)


def set_error(message: Optional[str]) -> None:
    def _mutate(state):
        state["system"]["last_error"] = message
    update_state(_mutate)


def save_capture(prefix: str, frame: np.ndarray) -> str:
    CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
    ts = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = f"{prefix}_{ts}.jpg"
    path = CAPTURE_DIR / filename
    cv2.imwrite(str(path), frame, [int(cv2.IMWRITE_JPEG_QUALITY), 90])
    return f"captures/{filename}"


def get_frame_copy() -> Optional[np.ndarray]:
    with frame_lock:
        if latest_frame is None:
            return None
        return latest_frame.copy()


def init_models() -> None:
    global net, plate_cascade
    proto = MODEL_DIR / "MobileNetSSD_deploy.prototxt"
    model = MODEL_DIR / "MobileNetSSD_deploy.caffemodel"
    plate_xml = MODEL_DIR / "haarcascade_russian_plate_number.xml"

    if not proto.exists() or not model.exists():
        raise FileNotFoundError("MobileNet SSD model files missing in /opt/gcam/models")
    if not plate_xml.exists():
        raise FileNotFoundError("Plate cascade file missing in /opt/gcam/models")

    net = cv2.dnn.readNetFromCaffe(str(proto), str(model))
    plate_cascade = cv2.CascadeClassifier(str(plate_xml))
    if plate_cascade.empty():
        raise RuntimeError("Failed to load plate cascade")


def open_rtsp() -> cv2.VideoCapture:
    cap = cv2.VideoCapture(RTSP_URL, cv2.CAP_FFMPEG)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    return cap


def reader_loop() -> None:
    global latest_frame, latest_frame_ts
    reconnect_wait = 3

    while not stop_event.is_set():
        cap = open_rtsp()
        if not cap.isOpened():
            set_error("Cannot open RTSP stream")
            update_state(lambda s: s["system"].update({"camera_connected": False}))
            time.sleep(reconnect_wait)
            continue

        update_state(lambda s: s["system"].update({"camera_connected": True, "last_error": None}))

        while not stop_event.is_set():
            ok, frame = cap.read()
            if not ok or frame is None:
                set_error("RTSP read failed, reconnecting")
                update_state(lambda s: s["system"].update({"camera_connected": False}))
                break

            latest_frame_ts = time.time()

            # resize to reduce CPU on Pi 1GB
            h, w = frame.shape[:2]
            target_w = 960
            if w > target_w:
                new_h = int(h * (target_w / w))
                frame = cv2.resize(frame, (target_w, new_h))

            with frame_lock:
                latest_frame = frame

            update_state(lambda s: s["system"].update({
                "camera_connected": True,
                "last_frame_at": now_str(),
                "last_error": None
            }))

        cap.release()
        time.sleep(reconnect_wait)


def detect_objects(frame: np.ndarray, min_conf: float = 0.35) -> List[Dict[str, Any]]:
    blob = cv2.dnn.blobFromImage(cv2.resize(frame, (300, 300)), 0.007843, (300, 300), 127.5)
    net.setInput(blob)
    detections = net.forward()

    h, w = frame.shape[:2]
    results = []

    for i in range(detections.shape[2]):
        conf = float(detections[0, 0, i, 2])
        if conf < min_conf:
            continue

        idx = int(detections[0, 0, i, 1])
        if idx < 0 or idx >= len(MOBILENET_CLASSES):
            continue

        label = MOBILENET_CLASSES[idx]
        box = detections[0, 0, i, 3:7] * np.array([w, h, w, h])
        start_x, start_y, end_x, end_y = box.astype("int")

        start_x = max(0, start_x)
        start_y = max(0, start_y)
        end_x = min(w - 1, end_x)
        end_y = min(h - 1, end_y)

        if end_x <= start_x or end_y <= start_y:
            continue

        results.append({
            "label": label,
            "confidence": conf,
            "box": (start_x, start_y, end_x, end_y)
        })

    return results


def draw_boxes(frame: np.ndarray, detections: List[Dict[str, Any]], color=(0, 255, 0)) -> np.ndarray:
    out = frame.copy()
    for d in detections:
        x1, y1, x2, y2 = d["box"]
        label = f'{d["label"]} {d["confidence"]:.2f}'
        cv2.rectangle(out, (x1, y1), (x2, y2), color, 2)
        cv2.putText(out, label, (x1, max(20, y1 - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, color, 2)
    return out


def clean_plate_text(text: str) -> str:
    text = re.sub(r"[^A-Z0-9]", "", text.upper())
    return text[:12]


def detect_plate(frame: np.ndarray) -> Tuple[Optional[str], Optional[np.ndarray]]:
    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    plates = plate_cascade.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=4, minSize=(60, 20))

    best_plate = None
    best_img = None

    for (x, y, w, h) in plates:
        roi = frame[y:y+h, x:x+w]
        if roi.size == 0:
            continue

        roi_gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
        roi_gray = cv2.bilateralFilter(roi_gray, 11, 17, 17)
        roi_gray = cv2.threshold(roi_gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)[1]

        text = pytesseract.image_to_string(
            roi_gray,
            config="--oem 3 --psm 7 -c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
        )
        plate_text = clean_plate_text(text)

        if len(plate_text) >= 5:
            annotated = frame.copy()
            cv2.rectangle(annotated, (x, y), (x+w, y+h), (255, 0, 0), 2)
            cv2.putText(annotated, plate_text, (x, max(20, y - 10)), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 0, 0), 2)
            best_plate = plate_text
            best_img = annotated
            break

    return best_plate, best_img


def get_roi(frame: np.ndarray) -> np.ndarray:
    h, w = frame.shape[:2]
    x1 = int(w * GARBAGE_ROI[0])
    y1 = int(h * GARBAGE_ROI[1])
    x2 = int(w * GARBAGE_ROI[2])
    y2 = int(h * GARBAGE_ROI[3])
    return frame[y1:y2, x1:x2]


def clutter_score(frame: np.ndarray) -> float:
    roi = get_roi(frame)
    if roi.size == 0:
        return 0.0
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blur, 40, 120)
    score = float(np.count_nonzero(edges)) / float(edges.size)
    return round(score, 4)


def detect_garbage(frame: np.ndarray) -> Dict[str, Any]:
    detections = detect_objects(frame, min_conf=0.30)
    garbage_objects = [d for d in detections if d["label"] in GARBAGE_OBJECT_CLASSES and d["confidence"] >= BOTTLE_CONFIDENCE]
    score = clutter_score(frame)

    if len(garbage_objects) >= 2 or score >= CLUTTER_HIGH_THRESHOLD:
        severity = "HIGH"
    elif len(garbage_objects) >= 1 or score >= CLUTTER_MEDIUM_THRESHOLD:
        severity = "MEDIUM"
    elif score >= CLUTTER_LOW_THRESHOLD:
        severity = "LOW"
    else:
        severity = None

    annotated = frame.copy()
    h, w = frame.shape[:2]
    rx1 = int(w * GARBAGE_ROI[0])
    ry1 = int(h * GARBAGE_ROI[1])
    rx2 = int(w * GARBAGE_ROI[2])
    ry2 = int(h * GARBAGE_ROI[3])
    cv2.rectangle(annotated, (rx1, ry1), (rx2, ry2), (0, 255, 255), 2)
    cv2.putText(annotated, f"Garbage ROI", (rx1 + 5, max(20, ry1 - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 255, 255), 2)

    for d in garbage_objects:
        x1, y1, x2, y2 = d["box"]
        cv2.rectangle(annotated, (x1, y1), (x2, y2), (0, 140, 255), 2)
        cv2.putText(annotated, f'{d["label"]} {d["confidence"]:.2f}', (x1, max(20, y1 - 8)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 140, 255), 2)

    label = severity if severity else "NONE"
    cv2.putText(annotated, f"Severity: {label} | Score: {score:.4f}", (20, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 0, 255) if severity else (0, 255, 0), 2)

    return {
        "severity": severity,
        "score": score,
        "objects": [d["label"] for d in garbage_objects],
        "annotated": annotated
    }


def play_warning_audio() -> None:
    with audio_lock:
        wav = ASSET_DIR / "warning.wav"
        if not wav.exists():
            return
        try:
            subprocess.Popen(
                ["aplay", "-q", str(wav)],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
        except Exception:
            pass


def person_plate_loop() -> None:
    last_person_trigger = 0.0
    last_plate_trigger = 0.0
    last_audio = 0.0
    frame_index = 0

    while not stop_event.is_set():
        frame = get_frame_copy()
        if frame is None:
            time.sleep(1)
            continue

        frame_index += 1

        # Person detection every 4th frame to save CPU
        if frame_index % 4 == 0:
            detections = detect_objects(frame, min_conf=PERSON_CONFIDENCE)
            persons = [d for d in detections if d["label"] == "person" and d["confidence"] >= PERSON_CONFIDENCE]

            if persons and time.time() - last_person_trigger >= PERSON_COOLDOWN_SEC:
                annotated = draw_boxes(frame, persons, color=(0, 0, 255))
                rel_img = save_capture("person", annotated)
                t = now_str()

                def _mutate(state):
                    state["last_person"] = {
                        "detected": True,
                        "time": t,
                        "image": rel_img
                    }
                update_state(_mutate)
                add_event("person", {"image": rel_img, "count": len(persons)})
                last_person_trigger = time.time()

                if time.time() - last_audio >= AUDIO_COOLDOWN_SEC:
                    threading.Thread(target=play_warning_audio, daemon=True).start()
                    last_audio = time.time()

        # Plate OCR every 20th frame to save CPU
        if frame_index % 20 == 0 and time.time() - last_plate_trigger >= PLATE_COOLDOWN_SEC:
            plate_text, annotated = detect_plate(frame)
            if plate_text and annotated is not None:
                rel_img = save_capture("plate", annotated)
                t = now_str()

                def _mutate(state):
                    state["last_plate"] = {
                        "detected": True,
                        "time": t,
                        "image": rel_img,
                        "plate_number": plate_text
                    }
                update_state(_mutate)
                add_event("plate", {"image": rel_img, "plate_number": plate_text})
                last_plate_trigger = time.time()

        time.sleep(0.08)


def garbage_loop() -> None:
    time.sleep(GARBAGE_BOOT_DELAY_SEC)

    while not stop_event.is_set():
        frame = get_frame_copy()
        if frame is not None:
            result = detect_garbage(frame)

            severity = result["severity"]
            if severity:
                rel_img = save_capture("garbage", result["annotated"])
            else:
                rel_img = save_capture("garbage", result["annotated"])

            t = now_str()

            def _mutate(state):
                state["counts"] = {"HIGH": 0, "MEDIUM": 0, "LOW": 0}
                if severity in state["counts"]:
                    state["counts"][severity] = 1

                state["last_garbage"] = {
                    "detected": bool(severity),
                    "time": t,
                    "image": rel_img,
                    "severity": severity,
                    "score": result["score"],
                    "objects": result["objects"]
                }

            update_state(_mutate)
            add_event("garbage", {
                "image": rel_img,
                "severity": severity,
                "score": result["score"],
                "objects": result["objects"]
            })

        for _ in range(GARBAGE_INTERVAL_SEC):
            if stop_event.is_set():
                break
            time.sleep(1)


def mjpeg_stream():
    while not stop_event.is_set():
        frame = get_frame_copy()
        if frame is None:
            time.sleep(0.2)
            continue

        ret, jpg = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), FRAME_JPEG_QUALITY])
        if not ret:
            continue

        yield (
            b"--frame\r\n"
            b"Content-Type: image/jpeg\r\n\r\n" + jpg.tobytes() + b"\r\n"
        )
        time.sleep(0.08)


@app.route("/")
def index():
    state = load_state()
    return render_template("index.html", state=state, ts=int(time.time()))


@app.route("/video_feed")
def video_feed():
    return Response(mjpeg_stream(), mimetype="multipart/x-mixed-replace; boundary=frame")


@app.route("/api/state")
def api_state():
    return jsonify(load_state())


@app.route("/captures/<path:filename>")
def captures(filename):
    return send_from_directory(str(CAPTURE_DIR), filename)


def startup():
    init_models()

    reader = threading.Thread(target=reader_loop, daemon=True)
    processor = threading.Thread(target=person_plate_loop, daemon=True)
    garbage = threading.Thread(target=garbage_loop, daemon=True)

    reader.start()
    processor.start()
    garbage.start()


def shutdown():
    stop_event.set()


atexit.register(shutdown)

if __name__ == "__main__":
    startup()
    serve(app, host=HOST, port=PORT, threads=8)
PY

cat > "$APP_DIR/templates/index.html" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>G-Cam "RealTech Systems"</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root{
      --bg:#0b1220;
      --card:#111a2e;
      --card2:#18233e;
      --text:#eaf1ff;
      --muted:#9eb3d9;
      --line:#253555;
      --accent:#33c3ff;
      --high:#ef4444;
      --med:#f59e0b;
      --low:#22c55e;
    }
    *{box-sizing:border-box}
    body{
      margin:0;
      font-family:Arial,Helvetica,sans-serif;
      background:linear-gradient(180deg,#08101d,#0f172a 40%,#0b1220);
      color:var(--text);
    }
    .wrap{
      width:min(1380px,95%);
      margin:0 auto;
      padding:20px 0 40px;
    }
    .header{
      display:flex;
      justify-content:space-between;
      align-items:center;
      gap:18px;
      padding:18px 22px;
      background:rgba(17,26,46,.9);
      border:1px solid var(--line);
      border-radius:18px;
      box-shadow:0 20px 40px rgba(0,0,0,.25);
      margin-bottom:18px;
    }
    .title h1{
      margin:0;
      font-size:32px;
      line-height:1.1;
    }
    .title p{
      margin:8px 0 0;
      color:var(--muted);
      font-size:14px;
    }
    .meta{
      text-align:right;
      color:var(--muted);
      font-size:14px;
    }
    .sev-grid{
      display:grid;
      grid-template-columns:repeat(3,1fr);
      gap:14px;
      margin-bottom:18px;
    }
    .sev-card{
      background:var(--card);
      border:1px solid var(--line);
      border-radius:16px;
      padding:18px;
      min-height:120px;
    }
    .sev-label{
      font-size:14px;
      color:var(--muted);
      margin-bottom:10px;
      letter-spacing:.4px;
    }
    .sev-value{
      font-size:52px;
      font-weight:700;
      line-height:1;
    }
    .high .sev-value{color:var(--high)}
    .medium .sev-value{color:var(--med)}
    .low .sev-value{color:var(--low)}

    .tabs{
      display:flex;
      gap:8px;
      margin-bottom:16px;
      flex-wrap:wrap;
    }
    .tab-btn{
      border:1px solid var(--line);
      background:var(--card);
      color:var(--text);
      padding:12px 16px;
      border-radius:12px;
      cursor:pointer;
      font-weight:700;
    }
    .tab-btn.active{
      background:var(--accent);
      color:#00111d;
      border-color:transparent;
    }

    .panel{
      display:none;
      background:var(--card);
      border:1px solid var(--line);
      border-radius:18px;
      padding:18px;
      box-shadow:0 20px 40px rgba(0,0,0,.20);
    }
    .panel.active{display:block}

    .panel-grid{
      display:grid;
      grid-template-columns:2fr 1fr;
      gap:18px;
      align-items:start;
    }

    .media-card{
      background:var(--card2);
      border:1px solid var(--line);
      border-radius:16px;
      overflow:hidden;
    }
    .media-card h3{
      margin:0;
      padding:14px 16px;
      border-bottom:1px solid var(--line);
      font-size:18px;
    }
    .media-body{
      padding:14px;
    }
    .media-body img{
      width:100%;
      display:block;
      border-radius:12px;
      border:1px solid var(--line);
      background:#000;
    }
    .info{
      display:grid;
      gap:12px;
    }
    .info-box{
      background:var(--card2);
      border:1px solid var(--line);
      border-radius:14px;
      padding:14px;
    }
    .info-box h4{
      margin:0 0 8px;
      color:var(--muted);
      font-size:13px;
      text-transform:uppercase;
      letter-spacing:.6px;
    }
    .info-box .big{
      font-size:24px;
      font-weight:700;
    }
    .badge{
      display:inline-block;
      padding:6px 10px;
      border-radius:999px;
      font-size:13px;
      font-weight:700;
    }
    .badge.high{background:rgba(239,68,68,.15); color:#ff8b8b; border:1px solid rgba(239,68,68,.35)}
    .badge.medium{background:rgba(245,158,11,.15); color:#ffc56c; border:1px solid rgba(245,158,11,.35)}
    .badge.low{background:rgba(34,197,94,.15); color:#7bf7ac; border:1px solid rgba(34,197,94,.35)}
    .badge.none{background:rgba(148,163,184,.15); color:#cdd7ea; border:1px solid rgba(148,163,184,.35)}

    .footer-note{
      margin-top:16px;
      color:var(--muted);
      font-size:13px;
    }

    @media (max-width: 980px){
      .panel-grid{grid-template-columns:1fr}
      .sev-grid{grid-template-columns:1fr}
      .header{flex-direction:column;align-items:flex-start}
      .meta{text-align:left}
    }
  </style>
</head>
<body>
<div class="wrap">
  <div class="header">
    <div class="title">
      <h1>G-Cam "RealTech Systems"</h1>
      <p>Offline monitoring dashboard for live feed, person alerts, garbage alerts, and vehicle plate capture</p>
    </div>
    <div class="meta">
      <div><strong>Camera:</strong> {{ state.camera_ip }}</div>
      <div><strong>Raspberry Pi:</strong> {{ state.raspberry_ip }}</div>
      <div><strong>Router:</strong> {{ state.router_ip }}</div>
      <div><strong>Last frame:</strong> {{ state.system.last_frame_at or "Waiting..." }}</div>
    </div>
  </div>

  <div class="sev-grid">
    <div class="sev-card high">
      <div class="sev-label">HIGH Tab</div>
      <div class="sev-value" id="highCount">{{ state.counts.HIGH }}</div>
    </div>
    <div class="sev-card medium">
      <div class="sev-label">MEDIUM Tab</div>
      <div class="sev-value" id="mediumCount">{{ state.counts.MEDIUM }}</div>
    </div>
    <div class="sev-card low">
      <div class="sev-label">LOW Tab</div>
      <div class="sev-value" id="lowCount">{{ state.counts.LOW }}</div>
    </div>
  </div>

  <div class="tabs">
    <button class="tab-btn active" onclick="showTab('live', this)">Live Feed</button>
    <button class="tab-btn" onclick="showTab('garbage', this)">Last Garbage Detects Image</button>
    <button class="tab-btn" onclick="showTab('person', this)">Last Person Detects Image</button>
    <button class="tab-btn" onclick="showTab('plate', this)">Last License Plate Detect</button>
  </div>

  <div id="tab-live" class="panel active">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Live RTSP Feed</h3>
        <div class="media-body">
          <img id="liveFeed" src="/video_feed" alt="Live Feed">
        </div>
      </div>
      <div class="info">
        <div class="info-box">
          <h4>Camera Status</h4>
          <div class="big" id="cameraStatus">{{ "Connected" if state.system.camera_connected else "Disconnected" }}</div>
        </div>
        <div class="info-box">
          <h4>Last Error</h4>
          <div id="lastError">{{ state.system.last_error or "None" }}</div>
        </div>
        <div class="info-box">
          <h4>Latest Garbage Severity</h4>
          {% set sev = state.last_garbage.severity %}
          <div id="garbageSeverity">
            {% if sev == "HIGH" %}
              <span class="badge high">HIGH</span>
            {% elif sev == "MEDIUM" %}
              <span class="badge medium">MEDIUM</span>
            {% elif sev == "LOW" %}
              <span class="badge low">LOW</span>
            {% else %}
              <span class="badge none">NONE</span>
            {% endif %}
          </div>
        </div>
        <div class="info-box">
          <h4>Last Plate Number</h4>
          <div class="big" id="plateNumber">{{ state.last_plate.plate_number or "N/A" }}</div>
        </div>
      </div>
    </div>
  </div>

  <div id="tab-garbage" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last Garbage Detection Image</h3>
        <div class="media-body">
          {% if state.last_garbage.image %}
            <img id="garbageImg" src="/{{ state.last_garbage.image }}?t={{ ts }}" alt="Last Garbage Detection">
          {% else %}
            <img id="garbageImg" src="" alt="No Garbage Detection Yet">
          {% endif %}
        </div>
      </div>
      <div class="info">
        <div class="info-box">
          <h4>Detection Time</h4>
          <div id="garbageTime">{{ state.last_garbage.time or "Waiting..." }}</div>
        </div>
        <div class="info-box">
          <h4>Severity</h4>
          <div id="garbageSeverityText" class="big">{{ state.last_garbage.severity or "NONE" }}</div>
        </div>
        <div class="info-box">
          <h4>Scene Score</h4>
          <div id="garbageScore">{{ state.last_garbage.score }}</div>
        </div>
        <div class="info-box">
          <h4>Detected Objects</h4>
          <div id="garbageObjects">{{ state.last_garbage.objects|join(', ') if state.last_garbage.objects else "None" }}</div>
        </div>
      </div>
    </div>
  </div>

  <div id="tab-person" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last Person Detection Image</h3>
        <div class="media-body">
          {% if state.last_person.image %}
            <img id="personImg" src="/{{ state.last_person.image }}?t={{ ts }}" alt="Last Person Detection">
          {% else %}
            <img id="personImg" src="" alt="No Person Detection Yet">
          {% endif %}
        </div>
      </div>
      <div class="info">
        <div class="info-box">
          <h4>Detection Time</h4>
          <div id="personTime">{{ state.last_person.time or "Waiting..." }}</div>
        </div>
        <div class="info-box">
          <h4>Status</h4>
          <div id="personStatus" class="big">{{ "Detected" if state.last_person.detected else "No recent detection" }}</div>
        </div>
      </div>
    </div>
  </div>

  <div id="tab-plate" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last License Plate Image</h3>
        <div class="media-body">
          {% if state.last_plate.image %}
            <img id="plateImg" src="/{{ state.last_plate.image }}?t={{ ts }}" alt="Last Plate Detection">
          {% else %}
            <img id="plateImg" src="" alt="No Plate Detection Yet">
          {% endif %}
        </div>
      </div>
      <div class="info">
        <div class="info-box">
          <h4>Detection Time</h4>
          <div id="plateTime">{{ state.last_plate.time or "Waiting..." }}</div>
        </div>
        <div class="info-box">
          <h4>Plate Number</h4>
          <div id="plateNumberBox" class="big">{{ state.last_plate.plate_number or "N/A" }}</div>
        </div>
      </div>
    </div>
  </div>

  <div class="footer-note">
    Open this page locally from your network at <strong>http://192.168.22.101:8080</strong>
  </div>
</div>

<script>
function showTab(name, btn){
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.getElementById('tab-' + name).classList.add('active');
  btn.classList.add('active');
}

function badgeHtml(sev){
  if(sev === 'HIGH') return '<span class="badge high">HIGH</span>';
  if(sev === 'MEDIUM') return '<span class="badge medium">MEDIUM</span>';
  if(sev === 'LOW') return '<span class="badge low">LOW</span>';
  return '<span class="badge none">NONE</span>';
}

async function refreshState(){
  try{
    const res = await fetch('/api/state?t=' + Date.now());
    const s = await res.json();

    document.getElementById('highCount').textContent = s.counts.HIGH;
    document.getElementById('mediumCount').textContent = s.counts.MEDIUM;
    document.getElementById('lowCount').textContent = s.counts.LOW;

    document.getElementById('cameraStatus').textContent = s.system.camera_connected ? 'Connected' : 'Disconnected';
    document.getElementById('lastError').textContent = s.system.last_error || 'None';
    document.getElementById('garbageSeverity').innerHTML = badgeHtml(s.last_garbage.severity);
    document.getElementById('plateNumber').textContent = s.last_plate.plate_number || 'N/A';

    document.getElementById('garbageTime').textContent = s.last_garbage.time || 'Waiting...';
    document.getElementById('garbageSeverityText').textContent = s.last_garbage.severity || 'NONE';
    document.getElementById('garbageScore').textContent = s.last_garbage.score;
    document.getElementById('garbageObjects').textContent = (s.last_garbage.objects && s.last_garbage.objects.length) ? s.last_garbage.objects.join(', ') : 'None';

    document.getElementById('personTime').textContent = s.last_person.time || 'Waiting...';
    document.getElementById('personStatus').textContent = s.last_person.detected ? 'Detected' : 'No recent detection';

    document.getElementById('plateTime').textContent = s.last_plate.time || 'Waiting...';
    document.getElementById('plateNumberBox').textContent = s.last_plate.plate_number || 'N/A';

    if(s.last_garbage.image){
      document.getElementById('garbageImg').src = '/' + s.last_garbage.image + '?t=' + Date.now();
    }
    if(s.last_person.image){
      document.getElementById('personImg').src = '/' + s.last_person.image + '?t=' + Date.now();
    }
    if(s.last_plate.image){
      document.getElementById('plateImg').src = '/' + s.last_plate.image + '?t=' + Date.now();
    }
  }catch(err){
    console.log(err);
  }
}

setInterval(refreshState, 5000);
</script>
</body>
</html>
HTML

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=G-Cam RealTech Systems
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$APP_DIR
Environment=PYTHONUNBUFFERED=1
ExecStart=$VENV_DIR/bin/python $APP_DIR/app.py
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable gcam.service
sudo systemctl restart gcam.service

echo
echo "=========================================="
echo " G-Cam setup completed"
echo "=========================================="
echo "Dashboard URL: http://$PI_IP:8080"
echo "Service check : sudo systemctl status gcam.service"
echo "Logs          : journalctl -u gcam.service -f"
echo "App folder    : $APP_DIR"
echo
echo "To replace warning audio later:"
echo "  cp your_file.wav $APP_DIR/assets/warning.wav"
echo "  sudo systemctl restart gcam.service"
BASH

chmod +x /tmp/gcam_install.sh
bash /tmp/gcam_install.sh