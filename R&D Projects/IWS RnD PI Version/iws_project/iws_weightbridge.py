from enum import Enum, auto
from dataclasses import dataclass, field
from typing import Optional, List, Callable, Deque
from datetime import datetime, timedelta
from collections import deque
from pathlib import Path
import asyncio
import threading
import subprocess
import tempfile
import os
import xml.etree.ElementTree as ET

# ---------- BASE DIR ---------- #
BASE_DIR = Path(__file__).resolve().parent


# ---------- XML CONFIG LOADER ---------- #
class AppConfig:
    def __init__(self, path: Path):
        self.path = Path(path)
        self.values = {}
        if self.path.exists():
            try:
                tree = ET.parse(self.path)
                root = tree.getroot()
                settings = root.find("appSettings")
                if settings is not None:
                    for add in settings.findall("add"):
                        key = add.get("key")
                        val = add.get("value")
                        if key:
                            self.values[key] = val
            except Exception as ex:
                print("Failed to load config.xml:", ex)

    def get(self, key: str, default=None):
        return self.values.get(key, default)

    def get_int(self, key: str, default: int) -> int:
        val = self.get(key)
        if val is None:
            return default
        try:
            return int(val)
        except ValueError:
            return default

    def get_float(self, key: str, default: float) -> float:
        val = self.get(key)
        if val is None:
            return default
        try:
            return float(val)
        except ValueError:
            return default


# Load config.xml from same folder as script
CFG = AppConfig(BASE_DIR / "config.xml")

# ---------- GPIO (real on Pi, dummy on PC) ---------- #
try:
    import RPi.GPIO as GPIO
except ImportError:
    class DummyGPIO:
        BCM = BOARD = IN = OUT = PUD_UP = HIGH = LOW = None

        @staticmethod
        def setmode(*args, **kwargs): pass

        @staticmethod
        def setup(*args, **kwargs): pass

        @staticmethod
        def output(*args, **kwargs): pass

        @staticmethod
        def input(*args, **kwargs): return 0

        @staticmethod
        def add_event_detect(*args, **kwargs): pass

    GPIO = DummyGPIO

import serial
import cv2
from ftplib import FTP

from PyQt5.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QGridLayout, QLabel, QTextEdit, QTableWidget, QTableWidgetItem,
    QMenuBar, QAction, QFileDialog
)
from PyQt5.QtCore import QTimer, Qt, QDateTime

# ---------- CONFIG FROM XML + DEFAULTS ---------- #

# Serial ports & baudrates
WEIGHT_BRIDGE_PORT = CFG.get("wsCOMport", "/dev/ttyUSB2")
WEIGHT_BRIDGE_BAUDRATE = CFG.get_int("wsBaudRate", 9600)

RFID_IN_PORT = CFG.get("rfidInCOMport", "/dev/ttyUSB0")
RFID_OUT_PORT = CFG.get("rfidOutCOMport", "/dev/ttyUSB1")
RFID_IN_BAUDRATE = CFG.get_int("rfidInBaudRate", 9600)
RFID_OUT_BAUDRATE = CFG.get_int("rfidOutBaudRate", 9600)

# FTP
FTP_HOST = CFG.get("ftpHost", "192.168.1.10")
FTP_PORT = CFG.get_int("ftpPort", 21)
FTP_USER = CFG.get("ftpUser", "ftpuser")
FTP_PASSWORD = CFG.get("ftpPassword", "ftppassword")
FTP_BASE_PATH = CFG.get("ftpPath", "/weighbridge")

# Site
SITE_NAME = CFG.get("siteName", "PVT AWS SITE 01")
IMAGE_PREFIX = CFG.get("imgPrefix", "AWS_")

# Timings
RFID_TRIGGER_DURATION_SEC = CFG.get_int("rfidTriggerDurationSec", 10)
IN_BARRIER_CLOSE_DELAY_SEC = CFG.get_int("bridgeWaitTime", 25)
POST_PROCESS_GO_DELAY_SEC = CFG.get_int("postProcessGoDelaySec", 10)
OUT_BARRIER_CLOSE_DELAY_SEC = CFG.get_int("outBarrierCloseDelaySec", 15)

# Weights / tolerances (kg)
MIN_WEIGHT_KG = CFG.get_int("minWeightKg", 1000)
STABLE_TOLERANCE_IN = CFG.get_int("stableToleranceIn", 100)
STABLE_TOLERANCE_OUT = CFG.get_int("stableToleranceOut", 50)
STABLE_TIME_IN_SEC = CFG.get_int("stableTimeInSec", 15)
STABLE_TIME_OUT_SEC = CFG.get_int("stableTimeOutSec", 10)
READY_TOLERANCE_KG = CFG.get_int("readyToleranceKg", 50)

# Paths
_snapshot_path = CFG.get("snapshotPath", "data/images")
_audio_path = CFG.get("audioPath", "data/audio")

SNAPSHOT_ROOT = (BASE_DIR / _snapshot_path).resolve()
AUDIO_DIR = (BASE_DIR / _audio_path).resolve()

# Camera URLs
RTSP_CAMERA_URLS = [
    CFG.get("camera1Url", "rtsp://user:pass@cam1/stream"),
    CFG.get("camera2Url", "rtsp://user:pass@cam2/stream"),
    CFG.get("camera3Url", "rtsp://user:pass@cam3/stream"),
    CFG.get("camera4Url", "rtsp://user:pass@cam4/stream"),
]

# Audio files (MP3s placed in AUDIO_DIR)
AUDIO_FILES = {
    "ENTER_VEHICLE": AUDIO_DIR / "enter_vehicle.mp3",
    "VERIFY_WEIGHT": AUDIO_DIR / "verify_weight.mp3",
    "PROCESSING": AUDIO_DIR / "processing.mp3",
    "STOP": AUDIO_DIR / "stop.mp3",
    "GO": AUDIO_DIR / "go.mp3",
}


# ---------- MODELS ---------- #

class SignalColor(Enum):
    RED = auto()
    GREEN = auto()
    YELLOW = auto()


class SystemState(Enum):
    READY = auto()
    VEHICLE_IN_OPEN = auto()
    WEIGHT_STABILIZING_IN = auto()
    PROCESSING = auto()
    PRINTING = auto()
    VEHICLE_OUT_OPEN = auto()
    COMPLETE = auto()


@dataclass
class WeightReading:
    timestamp: datetime
    weight_kg: float
    raw_line: str = ""


@dataclass
class TripData:
    vehicle_number: Optional[str] = None
    rfid_tag: Optional[str] = None
    in_weight: Optional[float] = None
    out_weight: Optional[float] = None
    net_weight: Optional[float] = None
    images_paths: List[str] = field(default_factory=list)
    created_at: datetime = field(default_factory=datetime.now)
    site_name: str = SITE_NAME
    image_prefix: str = IMAGE_PREFIX


@dataclass
class SystemStatus:
    state: SystemState
    signal_color: SignalColor
    current_weight: float = 0.0
    vehicle_number: str = ""
    rfid_in: str = ""
    rfid_out: str = ""
    online: bool = True
    last_error: str = ""
    display_text: str = ""


# ---------- HARDWARE ABSTRACTION ---------- #

class WeightBridge:
    """RS232 weight bridge with universal-ish parsing (e.g. 'wn009853 kg')."""

    def __init__(self, port=WEIGHT_BRIDGE_PORT, baudrate=WEIGHT_BRIDGE_BAUDRATE):
        self.port = port
        self.baudrate = baudrate
        self._serial = None
        self._running = False
        self._callback: Optional[Callable[[WeightReading], None]] = None

    def set_callback(self, cb: Callable[[WeightReading], None]):
        self._callback = cb

    def _parse_weight_line(self, line: str) -> Optional[WeightReading]:
        raw = line.strip()
        if not raw:
            return None
        lower = raw.lower()
        if "kg" not in lower:
            return None
        numeric_part = ""
        for ch in raw:
            if ch.isdigit():
                numeric_part += ch
        if not numeric_part:
            return None
        weight_kg = int(numeric_part)
        return WeightReading(timestamp=datetime.now(), weight_kg=weight_kg, raw_line=raw)

    async def start(self):
        self._serial = serial.Serial(self.port, self.baudrate, timeout=0.1)
        self._running = True
        buffer = ""
        while self._running:
            if self._serial.in_waiting:
                data = self._serial.read(self._serial.in_waiting)
                try:
                    chunk = data.decode(errors="ignore")
                except Exception:
                    chunk = ""
                buffer += chunk
                *lines, buffer = buffer.split("\n")
                for line in lines:
                    reading = self._parse_weight_line(line)
                    if reading and self._callback:
                        self._callback(reading)
            await asyncio.sleep(0.05)

    async def stop(self):
        self._running = False
        await asyncio.sleep(0.1)
        if self._serial and self._serial.is_open:
            self._serial.close()


class RFIDReader:
    def __init__(self, port: str, baudrate: int):
        self.port = port
        self.baudrate = baudrate
        self._serial = None
        self._running = False
        self._callback: Optional[Callable[[str], None]] = None

    def set_callback(self, cb: Callable[[str], None]):
        self._callback = cb

    async def start(self):
        self._serial = serial.Serial(self.port, self.baudrate, timeout=0.1)
        self._running = True
        buffer = ""
        while self._running:
            if self._serial.in_waiting:
                data = self._serial.read(self._serial.in_waiting)
                try:
                    chunk = data.decode(errors="ignore")
                except Exception:
                    chunk = ""
                buffer += chunk
                *lines, buffer = buffer.split("\n")
                for line in lines:
                    tag = line.strip()
                    if tag and self._callback:
                        self._callback(tag)
            await asyncio.sleep(0.05)

    async def stop(self):
        self._running = False
        await asyncio.sleep(0.1)
        if self._serial and self._serial.is_open:
            self._serial.close()


class SignalLight:
    def __init__(self, red_pin: int, green_pin: int, yellow_pin: int):
        self.red_pin = red_pin
        self.green_pin = green_pin
        self.yellow_pin = yellow_pin
        GPIO.setmode(GPIO.BCM)
        for p in (self.red_pin, self.green_pin, self.yellow_pin):
            GPIO.setup(p, GPIO.OUT)
            GPIO.output(p, GPIO.LOW)

    def set_color(self, color: SignalColor):
        GPIO.output(self.red_pin, GPIO.LOW)
        GPIO.output(self.green_pin, GPIO.LOW)
        GPIO.output(self.yellow_pin, GPIO.LOW)
        if color == SignalColor.RED:
            GPIO.output(self.red_pin, GPIO.HIGH)
        elif color == SignalColor.GREEN:
            GPIO.output(self.green_pin, GPIO.HIGH)
        elif color == SignalColor.YELLOW:
            GPIO.output(self.yellow_pin, GPIO.HIGH)


class BoomBarrier:
    def __init__(self, control_pin: int):
        self.control_pin = control_pin
        GPIO.setmode(GPIO.BCM)
        GPIO.setup(self.control_pin, GPIO.OUT)
        GPIO.output(self.control_pin, GPIO.LOW)  # closed

    def open(self):
        GPIO.output(self.control_pin, GPIO.HIGH)

    def close(self):
        GPIO.output(self.control_pin, GPIO.LOW)


class PhotoElectricSensor:
    def __init__(self, input_pin: int):
        self.input_pin = input_pin
        GPIO.setmode(GPIO.BCM)
        GPIO.setup(self.input_pin, GPIO.IN, pull_up_down=GPIO.PUD_UP)
        self._callback: Optional[Callable[[bool], None]] = None
        try:
            GPIO.add_event_detect(self.input_pin, GPIO.BOTH,
                                  callback=self._gpio_callback, bouncetime=100)
        except Exception:
            # On dummy or unsupported env
            pass

    def set_callback(self, cb: Callable[[bool], None]):
        self._callback = cb

    def _gpio_callback(self, channel: int):
        state = GPIO.input(self.input_pin) == GPIO.LOW  # vehicle present when LOW
        if self._callback:
            self._callback(state)

    def is_vehicle_present(self) -> bool:
        return GPIO.input(self.input_pin) == GPIO.LOW


class AudioSystem:
    def play(self, file_path: Path):
        file_str = str(file_path)
        if not os.path.exists(file_str):
            return
        subprocess.Popen(
            ["ffplay", "-nodisp", "-autoexit", file_str],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )


class InstructionDisplay:
    """DOT matrix text interface (here just prints + used by UI)."""

    def __init__(self):
        self.last_text = ""

    def show_text(self, text: str):
        self.last_text = text
        print("[INSTRUCTION DISPLAY]", text)


class RTSPCameras:
    def __init__(self, urls: List[str]):
        self.urls = urls
        self.caps = [cv2.VideoCapture(u) for u in urls]

    def get_frames(self):
        frames = []
        for cap in self.caps:
            ret, frame = cap.read()
            if ret:
                frames.append(frame)
            else:
                frames.append(None)
        return frames

    def capture_and_save(self, output_dir: Path, prefix: str):
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        frames = self.get_frames()
        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        paths = []
        for idx, frame in enumerate(frames, start=1):
            if frame is None:
                continue
            filename = f"{prefix}_CAM{idx}_{ts}.jpg"
            full_path = output_dir / filename
            cv2.imwrite(str(full_path), frame)
            paths.append(full_path)
        return paths

    def release(self):
        for c in self.caps:
            c.release()


class FTPClient:
    def __init__(self):
        self.host = FTP_HOST
        self.port = FTP_PORT
        self.user = FTP_USER
        self.password = FTP_PASSWORD
        self.base_path = FTP_BASE_PATH

    def upload_files(self, local_files: List[Path], remote_subdir: str):
        with FTP() as ftp:
            ftp.connect(self.host, self.port)
            ftp.login(self.user, self.password)
            target_dir = f"{self.base_path}/{remote_subdir}"
            for part in target_dir.split("/"):
                if not part:
                    continue
                try:
                    ftp.mkd(part)
                except Exception:
                    pass
                ftp.cwd(part)
            for f in local_files:
                with open(f, "rb") as fh:
                    ftp.storbinary(f"STOR {Path(f).name}", fh)


class Printer:
    """Simple slip printing via system 'lp' command."""

    def print_trip(self, trip: TripData):
        lines = []
        lines.append(f"Site: {trip.site_name}")
        lines.append(f"Date/Time: {trip.created_at.strftime('%Y-%m-%d %H:%M:%S')}")
        lines.append(f"RFID Tag: {trip.rfid_tag or ''}")
        lines.append(f"Vehicle: {trip.vehicle_number or ''}")
        lines.append(f"In Weight: {trip.in_weight or 0} kg")
        lines.append(f"Out Weight: {trip.out_weight or 0} kg")
        lines.append(f"Net Weight: {trip.net_weight or 0} kg")
        lines.append("")
        txt = "\n".join(lines)

        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".txt") as tmp:
            tmp.write(txt)
            tmp_path = tmp.name

        try:
            subprocess.run(["lp", tmp_path], check=False)
        finally:
            try:
                os.unlink(tmp_path)
            except Exception:
                pass


# ---------- CONTROLLER / STATE MACHINE ---------- #

class WeighbridgeController:
    def __init__(
        self,
        signal_light: SignalLight,
        boom_in: BoomBarrier,
        boom_out: BoomBarrier,
        weight_bridge: WeightBridge,
        rfid_in: RFIDReader,
        rfid_out: RFIDReader,
        photo_sensor: PhotoElectricSensor,
        audio: AudioSystem,
        instruction_display: InstructionDisplay,
        cameras: RTSPCameras,
        ftp_client: FTPClient,
        printer: Printer,
        ui_status_callback: Optional[Callable[[SystemStatus], None]] = None,
    ):
        self.signal_light = signal_light
        self.boom_in = boom_in
        self.boom_out = boom_out
        self.weight_bridge = weight_bridge
        self.rfid_in = rfid_in
        self.rfid_out = rfid_out
        self.photo_sensor = photo_sensor
        self.audio = audio
        self.instruction_display = instruction_display
        self.cameras = cameras
        self.ftp = ftp_client
        self.printer = printer
        self.ui_status_callback = ui_status_callback

        self.state = SystemState.READY
        self.current_weight = 0.0
        self.weight_history: Deque[WeightReading] = deque(maxlen=200)
        self.status = SystemStatus(
            state=self.state,
            signal_color=SignalColor.GREEN,
            current_weight=0.0,
            display_text="READY",
        )

        self._rfid_in_last_detected: Optional[datetime] = None
        self._rfid_out_last_detected: Optional[datetime] = None
        self._current_trip: Optional[TripData] = None
        self._task: Optional[asyncio.Task] = None
        self._running = False

        self.weight_bridge.set_callback(self._on_weight)
        self.rfid_in.set_callback(self._on_rfid_in)
        self.rfid_out.set_callback(self._on_rfid_out)
        self.photo_sensor.set_callback(self._on_photo_sensor)

    # ---- callbacks ---- #

    def _on_weight(self, reading: WeightReading):
        self.current_weight = reading.weight_kg
        self.weight_history.append(reading)
        self.status.current_weight = reading.weight_kg
        self._notify_ui()

    def _on_rfid_in(self, tag: str):
        self._rfid_in_last_detected = datetime.now()
        if not self._current_trip:
            self._current_trip = TripData(rfid_tag=tag)
        else:
            self._current_trip.rfid_tag = tag
        self.status.rfid_in = tag
        self._notify_ui()

    def _on_rfid_out(self, tag: str):
        self._rfid_out_last_detected = datetime.now()
        if self._current_trip and not self._current_trip.rfid_tag:
            self._current_trip.rfid_tag = tag
        self.status.rfid_out = tag
        self._notify_ui()

    def _on_photo_sensor(self, vehicle_present: bool):
        self._notify_ui()

    # ---- helpers ---- #

    def _notify_ui(self):
        if self.ui_status_callback:
            self.ui_status_callback(self.status)

    def _set_state(self, new_state: SystemState):
        self.state = new_state
        self.status.state = new_state
        self._notify_ui()

    def _set_signal(self, color: SignalColor):
        self.status.signal_color = color
        self.signal_light.set_color(color)
        self._notify_ui()

    def _set_display(self, text: str):
        self.status.display_text = text
        self.instruction_display.show_text(text)
        self._notify_ui()

    def _is_weight_stable(self, tolerance: float, duration_sec: int) -> bool:
        if not self.weight_history:
            return False
        now = datetime.now()
        cutoff = now - timedelta(seconds=duration_sec)
        relevant = [w for w in self.weight_history if w.timestamp >= cutoff]
        if len(relevant) < 3:
            return False
        weights = [w.weight_kg for w in relevant]
        w_min, w_max = min(weights), max(weights)
        return (w_max - w_min) <= (2 * tolerance)

    # ---- lifecycle ---- #

    async def start(self):
        self._running = True
        asyncio.create_task(self.weight_bridge.start())
        asyncio.create_task(self.rfid_in.start())
        asyncio.create_task(self.rfid_out.start())

        self._set_state(SystemState.READY)
        self._set_signal(SignalColor.GREEN)
        self.boom_in.close()
        self.boom_out.close()
        self._set_display("READY")

        self._task = asyncio.create_task(self._run_loop())

    async def stop(self):
        self._running = False
        if self._task:
            await self._task
        await self.weight_bridge.stop()
        await self.rfid_in.stop()
        await self.rfid_out.stop()
        self.cameras.release()

    async def _run_loop(self):
        while self._running:
            try:
                if self.state == SystemState.READY:
                    await self._handle_ready()
                elif self.state == SystemState.VEHICLE_IN_OPEN:
                    await self._handle_vehicle_in_open()
                elif self.state == SystemState.WEIGHT_STABILIZING_IN:
                    await self._handle_weight_stabilizing_in()
                elif self.state == SystemState.PROCESSING:
                    await self._handle_processing()
                elif self.state == SystemState.PRINTING:
                    await self._handle_printing()
                elif self.state == SystemState.VEHICLE_OUT_OPEN:
                    await self._handle_vehicle_out_open()
                elif self.state == SystemState.COMPLETE:
                    self._handle_complete()
            except Exception as ex:
                self.status.last_error = str(ex)
                self._notify_ui()
            await asyncio.sleep(0.1)

    # ---- state handlers ---- #

    async def _handle_ready(self):
        # READY STATE:
        #   Signal GREEN, both barriers closed, Display READY
        self._set_signal(SignalColor.GREEN)
        self.boom_in.close()
        self.boom_out.close()
        self._set_display("READY")

        # RFID detected continuously for configured time
        if self._rfid_in_last_detected:
            elapsed = (datetime.now() - self._rfid_in_last_detected).total_seconds()
            if elapsed >= RFID_TRIGGER_DURATION_SEC:
                self._set_state(SystemState.VEHICLE_IN_OPEN)
                self._set_signal(SignalColor.RED)
                self.boom_in.open()
                self._set_display("STOP")
                self.audio.play(AUDIO_FILES["ENTER_VEHICLE"])

    async def _handle_vehicle_in_open(self):
        # Wait until vehicle present (photo sensor), then wait extra time and close IN barrier
        if self.photo_sensor.is_vehicle_present():
            await asyncio.sleep(IN_BARRIER_CLOSE_DELAY_SEC)
            self.boom_in.close()
            self._set_state(SystemState.WEIGHT_STABILIZING_IN)
            self._set_display("PLEASE CHECK VEHICLE NUMBER & WEIGHT")
            self.audio.play(AUDIO_FILES["VERIFY_WEIGHT"])

    async def _handle_weight_stabilizing_in(self):
        # Wait for stable loaded weight
        if self._is_weight_stable(STABLE_TOLERANCE_IN, STABLE_TIME_IN_SEC) and self.current_weight >= MIN_WEIGHT_KG:
            if not self._current_trip:
                self._current_trip = TripData()
            self._current_trip.in_weight = self.current_weight
            # Keep message for 10 seconds before processing
            await asyncio.sleep(10)
            self._set_state(SystemState.PROCESSING)
            self._set_display("PROCESSING")
            self.audio.play(AUDIO_FILES["PROCESSING"])

    async def _handle_processing(self):
        # Capture images from RTSP cams
        out_dir = SNAPSHOT_ROOT / datetime.now().strftime("%Y%m%d")
        images = self.cameras.capture_and_save(out_dir, prefix=IMAGE_PREFIX)
        if self._current_trip:
            self._current_trip.images_paths = [str(p) for p in images]

        # Prepare simple metadata file -> FTP
        meta_lines = [
            f"Site={SITE_NAME}",
            f"Datetime={datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            f"RFID={self._current_trip.rfid_tag if self._current_trip else ''}",
            f"Vehicle={self._current_trip.vehicle_number if self._current_trip else ''}",
            f"InWeight={self._current_trip.in_weight if self._current_trip else 0}",
        ]
        meta_path = out_dir / f"{IMAGE_PREFIX}_META_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
        meta_path.parent.mkdir(parents=True, exist_ok=True)
        with open(meta_path, "w") as f:
            f.write("\n".join(meta_lines))

        to_upload = images + [meta_path]
        if to_upload:
            self.ftp.upload_files(to_upload, remote_subdir=datetime.now().strftime("%Y%m%d"))

        self._set_state(SystemState.PRINTING)

    async def _handle_printing(self):
        # Print slip
        if self._current_trip:
            self.printer.print_trip(self._current_trip)
        # Wait then open OUT barrier
        await asyncio.sleep(POST_PROCESS_GO_DELAY_SEC)
        self.boom_out.open()
        self._set_signal(SignalColor.YELLOW)
        self._set_display("GO")
        self.audio.play(AUDIO_FILES["GO"])
        self._set_state(SystemState.VEHICLE_OUT_OPEN)

    async def _handle_vehicle_out_open(self):
        # Wait for weight to come back near 0 and be stable
        if self._is_weight_stable(STABLE_TOLERANCE_OUT, OUT_BARRIER_CLOSE_DELAY_SEC) and self.current_weight <= READY_TOLERANCE_KG:
            if self._current_trip and self._current_trip.out_weight is None:
                self._current_trip.out_weight = self.current_weight
                if self._current_trip.in_weight is not None:
                    self._current_trip.net_weight = (self._current_trip.in_weight or 0) - (self._current_trip.out_weight or 0)
            self.boom_out.close()
            self._set_state(SystemState.COMPLETE)

    def _handle_complete(self):
        # Reset trip and go back to READY
        self._current_trip = None
        self._rfid_in_last_detected = None
        self._rfid_out_last_detected = None
        self._set_state(SystemState.READY)


# ---------- UI (PyQt5) ---------- #

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("IWS - PVT AWS Weighbridge")
        self.setGeometry(0, 0, 1280, 768)
        self.status: Optional[SystemStatus] = None

        container = QWidget()
        self.setCentralWidget(container)
        main_layout = QVBoxLayout()
        container.setLayout(main_layout)

        menubar = self.menuBar()
        self._build_menu(menubar)

        # Camera placeholders
        self.camera_labels = [QLabel(f"Camera {i + 1}") for i in range(4)]
        for lbl in self.camera_labels:
            lbl.setAlignment(Qt.AlignCenter)
            lbl.setStyleSheet("background-color: #222; color: white;")
        cameras_layout = QGridLayout()
        cameras_layout.addWidget(self.camera_labels[0], 0, 0)
        cameras_layout.addWidget(self.camera_labels[1], 0, 1)
        cameras_layout.addWidget(self.camera_labels[2], 1, 0)
        cameras_layout.addWidget(self.camera_labels[3], 1, 1)

        # Status row
        status_layout = QHBoxLayout()
        self.time_label = QLabel("Time: --")
        self.weight_label = QLabel("Weight: 0 kg")
        self.vehicle_label = QLabel("Vehicle: ---")
        self.rfid_label = QLabel("RFID: ---")
        self.signal_label = QLabel("Signal: GREEN")
        self.display_label = QLabel("Display: READY")

        for w in [
            self.time_label, self.weight_label, self.vehicle_label,
            self.rfid_label, self.signal_label, self.display_label
        ]:
            status_layout.addWidget(w)

        # Bottom: logs + trips
        bottom_layout = QHBoxLayout()
        self.log_widget = QTextEdit()
        self.log_widget.setReadOnly(True)
        self.log_widget.setPlaceholderText("Server Log...")

        self.trips_table = QTableWidget(0, 4)
        self.trips_table.setHorizontalHeaderLabels(["Time", "Vehicle", "RFID", "Net Weight"])

        bottom_layout.addWidget(self.log_widget, 2)
        bottom_layout.addWidget(self.trips_table, 3)

        main_layout.addLayout(cameras_layout, 3)
        main_layout.addLayout(status_layout, 1)
        main_layout.addLayout(bottom_layout, 3)

        # Clock
        self.clock_timer = QTimer(self)
        self.clock_timer.timeout.connect(self._update_time)
        self.clock_timer.start(1000)

    def _build_menu(self, menubar: QMenuBar):
        file_menu = menubar.addMenu("Menu")
        about_action = QAction("About", self)
        about_action.triggered.connect(self._show_about)
        file_menu.addAction(about_action)
        import_action = QAction("Import Vehicle List", self)
        import_action.triggered.connect(self._import_vehicle_list)
        file_menu.addAction(import_action)
        exit_action = QAction("Exit", self)
        exit_action.triggered.connect(QApplication.instance().quit)
        file_menu.addAction(exit_action)

    def _show_about(self):
        self.log_widget.append("IWS - PVT AWS Weighbridge System (Configurable Version)")

    def _import_vehicle_list(self):
        file_path, _ = QFileDialog.getOpenFileName(
            self, "Select Vehicle List Excel", "", "Excel Files (*.xlsx *.xls)"
        )
        if file_path:
            self.log_widget.append(f"Imported vehicle list: {file_path}")
            # TODO: parse and use mapping RFID -> vehicle number

    def update_status(self, status: SystemStatus):
        self.status = status
        self.weight_label.setText(f"Weight: {status.current_weight:.0f} kg")
        self.vehicle_label.setText(f"Vehicle: {status.vehicle_number or '---'}")
        self.rfid_label.setText(f"RFID: {status.rfid_in or status.rfid_out or '---'}")

        if status.signal_color == SignalColor.GREEN:
            signal_text = "GREEN"
        elif status.signal_color == SignalColor.RED:
            signal_text = "RED"
        elif status.signal_color == SignalColor.YELLOW:
            signal_text = "YELLOW"
        else:
            signal_text = "UNKNOWN"
        self.signal_label.setText(f"Signal: {signal_text}")

        self.display_label.setText(f"Display: {status.display_text or ''}")

        if status.last_error:
            self.log_widget.append(f"[ERROR] {status.last_error}")

    def append_log(self, message: str):
        ts = QDateTime.currentDateTime().toString("yyyy-MM-dd HH:mm:ss")
        self.log_widget.append(f"[{ts}] {message}")

    def _update_time(self):
        now_str = QDateTime.currentDateTime().toString("yyyy-MM-dd HH:mm:ss")
        self.time_label.setText(f"Time: {now_str}")


# ---------- ENTRY POINT ---------- #

def main():
    import sys
    app = QApplication(sys.argv)
    window = MainWindow()
    window.showMaximized()

    # GPIO pin numbers can also be moved to config if you want later
    signal_light = SignalLight(red_pin=17, green_pin=27, yellow_pin=22)
    boom_in = BoomBarrier(control_pin=5)
    boom_out = BoomBarrier(control_pin=6)
    weight_bridge = WeightBridge()
    rfid_in = RFIDReader(port=RFID_IN_PORT, baudrate=RFID_IN_BAUDRATE)
    rfid_out = RFIDReader(port=RFID_OUT_PORT, baudrate=RFID_OUT_BAUDRATE)
    photo_sensor = PhotoElectricSensor(input_pin=23)
    audio = AudioSystem()
    instruction_display = InstructionDisplay()
    cameras = RTSPCameras(RTSP_CAMERA_URLS)
    ftp_client = FTPClient()
    printer = Printer()

    def ui_status_callback(status: SystemStatus):
        window.update_status(status)

    controller = WeighbridgeController(
        signal_light=signal_light,
        boom_in=boom_in,
        boom_out=boom_out,
        weight_bridge=weight_bridge,
        rfid_in=rfid_in,
        rfid_out=rfid_out,
        photo_sensor=photo_sensor,
        audio=audio,
        instruction_display=instruction_display,
        cameras=cameras,
        ftp_client=ftp_client,
        printer=printer,
        ui_status_callback=ui_status_callback,
    )

    loop = asyncio.new_event_loop()

    async def async_main():
        await controller.start()

    def run_loop():
        asyncio.set_event_loop(loop)
        loop.create_task(async_main())
        loop.run_forever()

    t = threading.Thread(target=run_loop, daemon=True)
    t.start()

    exit_code = app.exec()
    loop.call_soon_threadsafe(loop.stop)
    sys.exit(exit_code)


if __name__ == "__main__":
    main()

