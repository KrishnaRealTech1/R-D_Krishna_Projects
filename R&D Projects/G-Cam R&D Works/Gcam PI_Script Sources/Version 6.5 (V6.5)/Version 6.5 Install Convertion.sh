#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/gcam"
VENV_DIR="$APP_DIR/venv"

SERVICE_FILE="/etc/systemd/system/gcam.service"
CONTROLLER_SERVICE_FILE="/etc/systemd/system/gcam-controller.service"

APP_RESTART_SERVICE="/etc/systemd/system/gcam-app-restart.service"
APP_RESTART_TIMER="/etc/systemd/system/gcam-app-restart.timer"

PI_REBOOT_SERVICE="/etc/systemd/system/gcam-pi-reboot.service"
PI_REBOOT_TIMER="/etc/systemd/system/gcam-pi-reboot.timer"

MAINT_SERVICE="/etc/systemd/system/gcam-maintenance.service"
MAINT_TIMER="/etc/systemd/system/gcam-maintenance.timer"
MAINT_SCRIPT="/usr/local/bin/gcam-maintenance.sh"

BATTERY_SCRIPT="/usr/local/bin/read-battery-voltage.sh"
REBOOT_HELPER="/usr/local/bin/gcam-force-reboot.sh"

CLOUDFLARE_PING_SCRIPT="/usr/local/bin/gcam-cloudflare-ping.sh"
CLOUDFLARE_PING_SERVICE="/etc/systemd/system/gcam-cloudflare-ping.service"

SUDOERS_FILE="/etc/sudoers.d/gcam-reboot"
GCAM_CONTROLLER_SUDOERS_FILE="/etc/sudoers.d/gcam-controller"

PI_USER="${SUDO_USER:-$USER}"
PI_HOME="$(getent passwd "$PI_USER" | cut -d: -f6)"

DEFAULT_RTSP_URL="rtsp://admin:admin@192.168.22.225:554/h264/ch1/main/av_stream"
DEFAULT_CLOUDFLARE_LIVE_RTSP_URL="rtsp://admin:admin@192.168.22.225:554/h264/ch1/sub/av_stream"
DEFAULT_PI_IP="192.168.22.101"
DEFAULT_ROUTER_IP="192.168.22.1"
DEFAULT_CAMERA_IP="192.168.22.225"
DEFAULT_CAMERA_NAME="RTGCAMx"
DEFAULT_DEVICE_ID="RTGCAMx"
DEFAULT_CAMERA_CONFIG_URL="https://192.168.22.225:443"

DEFAULT_SUBDOMAIN_NAME="RTGCAMx"
DEFAULT_BASE_DOMAIN="ifill.in"

DEFAULT_MQTT_HOST="3.111.78.82"
DEFAULT_MQTT_PORT="1883"
DEFAULT_MQTT_USERNAME="realiot"
DEFAULT_MQTT_PASSWORD="realmqtt@123"
DEFAULT_MQTT_GARBAGE_TOPIC="RTGCAMx/garbage_level"
DEFAULT_MQTT_CAMERA_EVENT_TOPIC="RTGCAMx/camera_event"
DEFAULT_MQTT_COMMAND_TOPIC="RTGCAMx/command"
DEFAULT_MQTT_RESPONSE_TOPIC="RTGCAMx/device_response"
DEFAULT_MQTT_COMMAND_TOKEN="GCAM_SECRET_123"

DEFAULT_TAILSCALE_MAIL_ID=""

DEFAULT_SFTP_HOST="3.111.78.82"
DEFAULT_SFTP_PORT="22"
DEFAULT_SFTP_USERNAME="ftpuser"
DEFAULT_SFTP_PASSWORD="ftp123!@#"
DEFAULT_SFTP_BASE_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}"
DEFAULT_SFTP_PERSON_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Person"
DEFAULT_SFTP_PERSON_VIDEO_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Video/Person_Video"
DEFAULT_SFTP_VEHICLE_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Vehicle"
DEFAULT_SFTP_VEHICLE_VIDEO_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Video/Vehicle_Video"
DEFAULT_SFTP_GARBAGE_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Garbage"
DEFAULT_SFTP_AUDIO_DIR="/home/ftpuser/ftp/files/${DEFAULT_DEVICE_ID}/Audio"

DEFAULT_BATTERY_MODE="command"
DEFAULT_BATTERY_SYSFS_PATH=""
DEFAULT_BATTERY_COMMAND="$BATTERY_SCRIPT"
DEFAULT_BATTERY_DIVIDER_RATIO="1.0"

DEFAULT_REBOOT_TIME_1="03:00:00"
DEFAULT_REBOOT_TIME_2="15:00:00"

DEFAULT_ROUTER_REBOOT_TIME_1="03:30"
DEFAULT_ROUTER_REBOOT_TIME_2="15:30"

echo "=================================================="
echo " G-Cam Full Clean Reinstall - V6.5 FIXED"
echo " Live + Person + Garbage + MQTT Audio + Data Status"
echo " + Open/Close Software + Camera Temp URL"
echo " + USB Audio Fix + Person False Detection Fix"
echo " + Near Full Screen Garbage ROI"
echo "=================================================="
echo

read -rp "Enter Device ID [${DEFAULT_DEVICE_ID}]: " DEVICE_ID
DEVICE_ID="${DEVICE_ID:-$DEFAULT_DEVICE_ID}"

read -rp "Enter Camera Name [${DEFAULT_CAMERA_NAME}]: " CAMERA_NAME
CAMERA_NAME="${CAMERA_NAME:-$DEFAULT_CAMERA_NAME}"

read -rp "Enter Main RTSP URL [${DEFAULT_RTSP_URL}]: " RTSP_URL
RTSP_URL="${RTSP_URL:-$DEFAULT_RTSP_URL}"

read -rp "Enter Cloudflare Live Sub RTSP URL [${DEFAULT_CLOUDFLARE_LIVE_RTSP_URL}]: " CLOUDFLARE_LIVE_RTSP_URL
CLOUDFLARE_LIVE_RTSP_URL="${CLOUDFLARE_LIVE_RTSP_URL:-$DEFAULT_CLOUDFLARE_LIVE_RTSP_URL}"

read -rp "Enter Raspberry Pi Local IP [${DEFAULT_PI_IP}]: " PI_IP
PI_IP="${PI_IP:-$DEFAULT_PI_IP}"

read -rp "Enter Router IP [${DEFAULT_ROUTER_IP}]: " ROUTER_IP
ROUTER_IP="${ROUTER_IP:-$DEFAULT_ROUTER_IP}"

read -rp "Enter Camera IP [${DEFAULT_CAMERA_IP}]: " CAMERA_IP
CAMERA_IP="${CAMERA_IP:-$DEFAULT_CAMERA_IP}"

read -rp "Enter Camera Configuration URL [${DEFAULT_CAMERA_CONFIG_URL}]: " CAMERA_CONFIG_URL
CAMERA_CONFIG_URL="${CAMERA_CONFIG_URL:-$DEFAULT_CAMERA_CONFIG_URL}"

read -rp "Enter Permanent Subdomain Name [${DEFAULT_SUBDOMAIN_NAME}]: " SUBDOMAIN_NAME
SUBDOMAIN_NAME="${SUBDOMAIN_NAME:-$DEFAULT_SUBDOMAIN_NAME}"

read -rp "Enter Base Domain [${DEFAULT_BASE_DOMAIN}]: " BASE_DOMAIN
BASE_DOMAIN="${BASE_DOMAIN:-$DEFAULT_BASE_DOMAIN}"

SUBDOMAIN_NAME="$(echo "$SUBDOMAIN_NAME" | tr '[:upper:]' '[:lower:]' | xargs)"
BASE_DOMAIN="$(echo "$BASE_DOMAIN" | tr '[:upper:]' '[:lower:]' | xargs)"
PERMANENT_LIVE_BASE_URL="https://${SUBDOMAIN_NAME}.${BASE_DOMAIN}"

read -rp "Enter Tailscale Mail ID [${DEFAULT_TAILSCALE_MAIL_ID}]: " TAILSCALE_MAIL_ID
TAILSCALE_MAIL_ID="${TAILSCALE_MAIL_ID:-$DEFAULT_TAILSCALE_MAIL_ID}"

echo
echo "MQTT configuration"
read -rp "Enter MQTT Host [${DEFAULT_MQTT_HOST}]: " MQTT_HOST
MQTT_HOST="${MQTT_HOST:-$DEFAULT_MQTT_HOST}"

read -rp "Enter MQTT Port [${DEFAULT_MQTT_PORT}]: " MQTT_PORT
MQTT_PORT="${MQTT_PORT:-$DEFAULT_MQTT_PORT}"

read -rp "Enter MQTT Username [${DEFAULT_MQTT_USERNAME}]: " MQTT_USERNAME
MQTT_USERNAME="${MQTT_USERNAME:-$DEFAULT_MQTT_USERNAME}"

read -rp "Enter MQTT Password [${DEFAULT_MQTT_PASSWORD}]: " MQTT_PASSWORD
MQTT_PASSWORD="${MQTT_PASSWORD:-$DEFAULT_MQTT_PASSWORD}"

read -rp "Enter MQTT Garbage Topic [${DEFAULT_MQTT_GARBAGE_TOPIC}]: " MQTT_GARBAGE_TOPIC
MQTT_GARBAGE_TOPIC="${MQTT_GARBAGE_TOPIC:-$DEFAULT_MQTT_GARBAGE_TOPIC}"

read -rp "Enter MQTT Camera Event Topic [${DEFAULT_MQTT_CAMERA_EVENT_TOPIC}]: " MQTT_CAMERA_EVENT_TOPIC
MQTT_CAMERA_EVENT_TOPIC="${MQTT_CAMERA_EVENT_TOPIC:-$DEFAULT_MQTT_CAMERA_EVENT_TOPIC}"

read -rp "Enter MQTT Command Topic [${DEFAULT_MQTT_COMMAND_TOPIC}]: " MQTT_COMMAND_TOPIC
MQTT_COMMAND_TOPIC="${MQTT_COMMAND_TOPIC:-$DEFAULT_MQTT_COMMAND_TOPIC}"

read -rp "Enter MQTT Response Topic [${DEFAULT_MQTT_RESPONSE_TOPIC}]: " MQTT_RESPONSE_TOPIC
MQTT_RESPONSE_TOPIC="${MQTT_RESPONSE_TOPIC:-$DEFAULT_MQTT_RESPONSE_TOPIC}"

read -rp "Enter MQTT Command Token [${DEFAULT_MQTT_COMMAND_TOKEN}]: " MQTT_COMMAND_TOKEN
MQTT_COMMAND_TOKEN="${MQTT_COMMAND_TOKEN:-$DEFAULT_MQTT_COMMAND_TOKEN}"

echo
echo "SFTP configuration"
echo "IMPORTANT: Enter REAL LINUX PATH like /home/ftpuser/ftp/files/RTGCAMx"
read -rp "Enter SFTP Host [${DEFAULT_SFTP_HOST}]: " SFTP_HOST
SFTP_HOST="${SFTP_HOST:-$DEFAULT_SFTP_HOST}"

read -rp "Enter SFTP Port [${DEFAULT_SFTP_PORT}]: " SFTP_PORT
SFTP_PORT="${SFTP_PORT:-$DEFAULT_SFTP_PORT}"

read -rp "Enter SFTP Username [${DEFAULT_SFTP_USERNAME}]: " SFTP_USERNAME
SFTP_USERNAME="${SFTP_USERNAME:-$DEFAULT_SFTP_USERNAME}"

read -rp "Enter SFTP Password [${DEFAULT_SFTP_PASSWORD}]: " SFTP_PASSWORD
SFTP_PASSWORD="${SFTP_PASSWORD:-$DEFAULT_SFTP_PASSWORD}"

read -rp "Paste Main SFTP Path [${DEFAULT_SFTP_BASE_DIR}]: " SFTP_BASE_DIR
SFTP_BASE_DIR="${SFTP_BASE_DIR:-$DEFAULT_SFTP_BASE_DIR}"

read -rp "Paste Person SFTP Path [${DEFAULT_SFTP_PERSON_DIR}]: " SFTP_PERSON_DIR
SFTP_PERSON_DIR="${SFTP_PERSON_DIR:-$DEFAULT_SFTP_PERSON_DIR}"

read -rp "Paste Person Video SFTP Path [${DEFAULT_SFTP_PERSON_VIDEO_DIR}]: " SFTP_PERSON_VIDEO_DIR
SFTP_PERSON_VIDEO_DIR="${SFTP_PERSON_VIDEO_DIR:-$DEFAULT_SFTP_PERSON_VIDEO_DIR}"

read -rp "Paste Vehicle Image SFTP Path [${DEFAULT_SFTP_VEHICLE_DIR}]: " SFTP_VEHICLE_DIR
SFTP_VEHICLE_DIR="${SFTP_VEHICLE_DIR:-$DEFAULT_SFTP_VEHICLE_DIR}"

read -rp "Paste Vehicle Video SFTP Path [${DEFAULT_SFTP_VEHICLE_VIDEO_DIR}]: " SFTP_VEHICLE_VIDEO_DIR
SFTP_VEHICLE_VIDEO_DIR="${SFTP_VEHICLE_VIDEO_DIR:-$DEFAULT_SFTP_VEHICLE_VIDEO_DIR}"

read -rp "Paste Garbage SFTP Path [${DEFAULT_SFTP_GARBAGE_DIR}]: " SFTP_GARBAGE_DIR
SFTP_GARBAGE_DIR="${SFTP_GARBAGE_DIR:-$DEFAULT_SFTP_GARBAGE_DIR}"

read -rp "Paste Audio SFTP Path [${DEFAULT_SFTP_AUDIO_DIR}]: " SFTP_AUDIO_DIR
SFTP_AUDIO_DIR="${SFTP_AUDIO_DIR:-$DEFAULT_SFTP_AUDIO_DIR}"

if [[ "${SFTP_BASE_DIR}" != /* ]]; then
  SFTP_BASE_DIR="/${SFTP_BASE_DIR}"
fi
SFTP_BASE_DIR="${SFTP_BASE_DIR%/}"

if [[ "${SFTP_PERSON_DIR}" != /* ]]; then
  SFTP_PERSON_DIR="/${SFTP_PERSON_DIR}"
fi
SFTP_PERSON_DIR="${SFTP_PERSON_DIR%/}"

if [[ "${SFTP_PERSON_VIDEO_DIR}" != /* ]]; then
  SFTP_PERSON_VIDEO_DIR="/${SFTP_PERSON_VIDEO_DIR}"
fi
SFTP_PERSON_VIDEO_DIR="${SFTP_PERSON_VIDEO_DIR%/}"

if [[ "${SFTP_VEHICLE_DIR}" != /* ]]; then
  SFTP_VEHICLE_DIR="/${SFTP_VEHICLE_DIR}"
fi
SFTP_VEHICLE_DIR="${SFTP_VEHICLE_DIR%/}"

if [[ "${SFTP_VEHICLE_VIDEO_DIR}" != /* ]]; then
  SFTP_VEHICLE_VIDEO_DIR="/${SFTP_VEHICLE_VIDEO_DIR}"
fi
SFTP_VEHICLE_VIDEO_DIR="${SFTP_VEHICLE_VIDEO_DIR%/}"

echo
BATTERY_MODE="command"
BATTERY_SYSFS_PATH=""
BATTERY_COMMAND="$BATTERY_SCRIPT"
BATTERY_DIVIDER_RATIO="1.0"

echo "Battery interface enabled: INA226 via I2C command helper."
echo
echo "Maintenance and reboot schedule"
read -rp "Enter full Pi reboot time #1 [${DEFAULT_REBOOT_TIME_1}]: " REBOOT_TIME_1
REBOOT_TIME_1="${REBOOT_TIME_1:-$DEFAULT_REBOOT_TIME_1}"

read -rp "Enter full Pi reboot time #2 [${DEFAULT_REBOOT_TIME_2}]: " REBOOT_TIME_2
REBOOT_TIME_2="${REBOOT_TIME_2:-$DEFAULT_REBOOT_TIME_2}"

read -rp "Enter router reboot GPIO time #1 [${DEFAULT_ROUTER_REBOOT_TIME_1}]: " ROUTER_REBOOT_TIME_1
ROUTER_REBOOT_TIME_1="${ROUTER_REBOOT_TIME_1:-$DEFAULT_ROUTER_REBOOT_TIME_1}"

read -rp "Enter router reboot GPIO time #2 [${DEFAULT_ROUTER_REBOOT_TIME_2}]: " ROUTER_REBOOT_TIME_2
ROUTER_REBOOT_TIME_2="${ROUTER_REBOOT_TIME_2:-$DEFAULT_ROUTER_REBOOT_TIME_2}"

echo
echo "[1/17] Stop old services..."
sudo systemctl stop gcam.service 2>/dev/null || true
sudo systemctl disable gcam.service 2>/dev/null || true
sudo systemctl stop gcam-controller.service 2>/dev/null || true
sudo systemctl disable gcam-controller.service 2>/dev/null || true
sudo systemctl stop gcam-app-restart.timer 2>/dev/null || true
sudo systemctl disable gcam-app-restart.timer 2>/dev/null || true
sudo systemctl stop gcam-pi-reboot.timer 2>/dev/null || true
sudo systemctl disable gcam-pi-reboot.timer 2>/dev/null || true
sudo systemctl stop gcam-maintenance.timer 2>/dev/null || true
sudo systemctl disable gcam-maintenance.timer 2>/dev/null || true
sudo systemctl kill gcam.service 2>/dev/null || true
sudo systemctl kill gcam-controller.service 2>/dev/null || true
sudo pkill -9 -f "/opt/gcam/venv/bin/python /opt/gcam/app.py" 2>/dev/null || true
sudo pkill -9 -f "/opt/gcam/venv/bin/python /opt/gcam/controller.py" 2>/dev/null || true
sudo pkill -9 -f "cloudflared tunnel --url" 2>/dev/null || true
sudo pkill -9 aplay 2>/dev/null || true
sudo pkill -9 ffplay 2>/dev/null || true
sudo rm -f \
  "$SERVICE_FILE" \
  "$CONTROLLER_SERVICE_FILE" \
  "$APP_RESTART_SERVICE" "$APP_RESTART_TIMER" \
  "$PI_REBOOT_SERVICE" "$PI_REBOOT_TIMER" \
  "$MAINT_SERVICE" "$MAINT_TIMER" \
  "$CLOUDFLARE_PING_SERVICE"
sudo rm -f "$MAINT_SCRIPT" "$SUDOERS_FILE" "$GCAM_CONTROLLER_SUDOERS_FILE"
sudo systemctl daemon-reload || true

echo "[2/17] Remove old app..."
sudo rm -rf "$APP_DIR"

echo "[3/17] Fix DNS if needed..."
if ! ping -c 1 deb.debian.org >/dev/null 2>&1; then
  sudo rm -f /etc/resolv.conf || true
  printf 'nameserver 8.8.8.8\nnameserver 1.1.1.1\n' | sudo tee /etc/resolv.conf >/dev/null
fi

echo "[4/17] Update apt..."
sudo apt-get update

echo "[5/17] Install system packages..."
sudo apt-get install -y \
  python3 python3-venv python3-pip python3-dev \
  python3-lgpio python3-smbus i2c-tools \
  ffmpeg alsa-utils espeak-ng curl wget ca-certificates jq sudo \
  iproute2 net-tools ethtool procps \
  libgl1 libjpeg62-turbo libtiff6 libopenjp2-7 libopenblas-dev \
  gpiod
if dpkg -s libglib2.0-0t64 >/dev/null 2>&1; then
  sudo apt-get install -y libglib2.0-0t64
else
  sudo apt-get install -y libglib2.0-0
fi

sudo raspi-config nonint do_i2c 0 || true
sudo modprobe i2c-dev || true

echo "[6/17] Install cloudflared..."
ARCH="$(dpkg --print-architecture)"
if [[ "$ARCH" == "arm64" ]]; then
  wget -q -O /tmp/cloudflared.deb https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64.deb
elif [[ "$ARCH" == "armhf" ]]; then
  wget -q -O /tmp/cloudflared.deb https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm.deb
else
  echo "Unsupported architecture for cloudflared package: $ARCH"
  exit 1
fi
sudo dpkg -i /tmp/cloudflared.deb || true
sudo apt-get -f install -y || true

echo "[7/17] Configure ALSA default to USB speaker hw:0,0..."
sudo tee /etc/asound.conf >/dev/null <<'EOF'
pcm.!default {
    type plug
    slave.pcm "hw:0,0"
}

ctl.!default {
    type hw
    card 0
}
EOF
sudo pkill -9 aplay 2>/dev/null || true
sudo pkill -9 ffplay 2>/dev/null || true

echo "[8/17] Create folders..."
sudo mkdir -p "$APP_DIR"/{assets,data,logs,models,files,files/garbage,files/audio,files/default_audio,files/video,tmp,templates}
sudo chown -R "$PI_USER:$PI_USER" "$APP_DIR"

echo "[9/17] Create Python environment..."
python3 -m venv "$VENV_DIR"
"$VENV_DIR/bin/pip" install --upgrade pip setuptools wheel
"$VENV_DIR/bin/pip" install \
  flask==3.0.3 \
  waitress==3.0.0 \
  numpy==1.26.4 \
  opencv-python-headless==4.10.0.84 \
  requests==2.32.3 \
  urllib3==2.2.2 \
  paho-mqtt==2.1.0 \
  paramiko==3.4.0 \
  psutil==6.0.0

echo "[10/17] Download person + garbage models..."
cd "$APP_DIR/models"
wget -q -O MobileNetSSD_deploy.prototxt \
  https://raw.githubusercontent.com/chuanqi305/MobileNet-SSD/master/deploy.prototxt
wget -q -O MobileNetSSD_deploy.caffemodel \
  https://github.com/chuanqi305/MobileNet-SSD/raw/master/mobilenet_iter_73000.caffemodel

# Small YOLO-style ONNX model for garbage object detection
# Keep filename exactly as used in app.py
install -m 644 "$PI_HOME/garbage_yolo.onnx" "$APP_DIR/models/garbage_yolo.onnx"

echo "[11/17] Create warning audio..."
espeak-ng -s 145 -a 55 -w "$APP_DIR/assets/warning_src.wav" \
  "Warning. Person detected. Please leave the monitored area."
ffmpeg -y -i "$APP_DIR/assets/warning_src.wav" \
  -af "highpass=f=180,lowpass=f=3200,volume=0.28" \
  -ac 1 -ar 16000 -c:a pcm_s16le "$APP_DIR/assets/warning.wav" >/dev/null 2>&1
rm -f "$APP_DIR/assets/warning_src.wav"

echo "[12/17] Write config..."
cat > "$APP_DIR/data/config.json" <<JSON
{
  "rtsp_url": "$RTSP_URL",
  "cloudflare_live_rtsp_url": "$CLOUDFLARE_LIVE_RTSP_URL",
  "pi_ip": "$PI_IP",
  "router_ip": "$ROUTER_IP",
  "camera_ip": "$CAMERA_IP",
  "camera_name": "$CAMERA_NAME",
  "device_id": "$DEVICE_ID",
  "camera_config_url": "$CAMERA_CONFIG_URL",
  "subdomain_name": "$SUBDOMAIN_NAME",
  "base_domain": "$BASE_DOMAIN",
  "permanent_live_base_url": "$PERMANENT_LIVE_BASE_URL",
  "tailscale_mail_id": "$TAILSCALE_MAIL_ID",
  "software_version": "Version 6.5",
  "software_features": "AI Mode/Normal Mode Interface for Garbage Detection",
  "router_reboot_schedule": {
    "time_1": "$ROUTER_REBOOT_TIME_1",
    "time_2": "$ROUTER_REBOOT_TIME_2"
  },
  "garbage_interval_sec": 3600,
  "garbage_detection_mode": "ai",

  "vehicle_detection_mode": "static",
  "vehicle_geofence_overlap_threshold": 0.35,
  "vehicle_roi": [0.00, 0.08, 1.00, 1.00],
  "vehicle_polygon": [
    [0.00, 0.08],
    [1.00, 0.08],
    [1.00, 1.00],
    [0.00, 1.00]
  ],

  "feature_flags": {
    "person_detection_enabled": true,
    "vehicle_detection_enabled": true,
    "garbage_detection_enabled": true,
    "person_video_recording_enabled": true,
    "vehicle_video_recording_enabled": true
  },
  "mqtt": {
    "host": "$MQTT_HOST",
    "port": $MQTT_PORT,
    "username": "$MQTT_USERNAME",
    "password": "$MQTT_PASSWORD",
    "garbage_topic": "$MQTT_GARBAGE_TOPIC",
    "camera_event_topic": "$MQTT_CAMERA_EVENT_TOPIC",
    "command_topic": "$MQTT_COMMAND_TOPIC",
    "response_topic": "$MQTT_RESPONSE_TOPIC",
    "command_token": "$MQTT_COMMAND_TOKEN"
  },
  "sftp": {
    "host": "$SFTP_HOST",
    "port": $SFTP_PORT,
    "username": "$SFTP_USERNAME",
    "password": "$SFTP_PASSWORD",
    "base_dir": "$SFTP_BASE_DIR",
    "person_dir": "$SFTP_PERSON_DIR",
    "person_video_dir": "$SFTP_PERSON_VIDEO_DIR",
    "vehicle_dir": "$SFTP_VEHICLE_DIR",
    "vehicle_video_dir": "$SFTP_VEHICLE_VIDEO_DIR",
    "garbage_dir": "$SFTP_GARBAGE_DIR",
    "audio_dir": "$SFTP_AUDIO_DIR",
    "default_audio_dir": "/home/ftpuser/ftp/files/Default_Audio"
  },
  "battery": {
    "mode": "$BATTERY_MODE",
    "sysfs_path": "$BATTERY_SYSFS_PATH",
    "command": "$BATTERY_COMMAND",
    "divider_ratio": $BATTERY_DIVIDER_RATIO
  }
}
JSON

echo "[13/17] Write helper scripts..."
sudo tee "$BATTERY_SCRIPT" >/dev/null <<'EOF'
#!/usr/bin/env python3
import json
import sys
import time
from smbus2 import SMBus

I2C_BUS = 1
I2C_ADDR = 0x40

SHUNT_OHMS = 0.01
CURRENT_SIGN = 1
VOLTAGE_CORRECTION = 1.0104
CURRENT_CORRECTION = 0.7627

REG_CONFIG = 0x00
REG_SHUNT_VOLTAGE = 0x01
REG_BUS_VOLTAGE = 0x02

CONFIG_CONTINUOUS_DEFAULT = 0x4527

SAMPLES = 8
SAMPLE_DELAY = 0.02

def read_u16_be(bus: SMBus, reg: int) -> int:
    raw = bus.read_word_data(I2C_ADDR, reg)
    return ((raw & 0xFF) << 8) | (raw >> 8)

def read_s16_be(bus: SMBus, reg: int) -> int:
    value = read_u16_be(bus, reg)
    if value & 0x8000:
        value -= 0x10000
    return value

def main() -> int:
    try:
        with SMBus(I2C_BUS) as bus:
            try:
                high = (CONFIG_CONTINUOUS_DEFAULT >> 8) & 0xFF
                low = CONFIG_CONTINUOUS_DEFAULT & 0xFF
                bus.write_i2c_block_data(I2C_ADDR, REG_CONFIG, [high, low])
                time.sleep(0.1)
            except Exception:
                pass

            bus_voltage_sum = 0.0
            shunt_voltage_sum = 0.0
            bus_voltage_raw_sum = 0

            for _ in range(SAMPLES):
                bus_voltage_raw = read_u16_be(bus, REG_BUS_VOLTAGE)
                shunt_voltage_raw = read_s16_be(bus, REG_SHUNT_VOLTAGE)

                bus_voltage_sum += bus_voltage_raw * 0.00125
                shunt_voltage_sum += shunt_voltage_raw * 0.0000025
                bus_voltage_raw_sum += bus_voltage_raw

                time.sleep(SAMPLE_DELAY)

            bus_voltage_v = (bus_voltage_sum / SAMPLES) * VOLTAGE_CORRECTION
            shunt_voltage_v = shunt_voltage_sum / SAMPLES

            current_a = ((shunt_voltage_v / SHUNT_OHMS) * CURRENT_SIGN) * CURRENT_CORRECTION
            current_ma = current_a * 1000.0

            print(json.dumps({
                "battery_voltage": round(bus_voltage_v, 3),
                "battery_current_a": round(current_a, 3),
                "battery_current_ma": round(current_ma, 1),
                "debug_bus_voltage_raw": round(bus_voltage_raw_sum / SAMPLES),
                "debug_shunt_voltage_uv": round(shunt_voltage_v * 1_000_000, 2)
            }))
            return 0

    except Exception as exc:
        print(json.dumps({
            "battery_voltage": None,
            "battery_current_a": None,
            "battery_current_ma": None,
            "error": str(exc)
        }))
        return 0

if __name__ == "__main__":
    sys.exit(main())
EOF
sudo chmod +x "$BATTERY_SCRIPT"

sudo tee "$REBOOT_HELPER" >/dev/null <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
/usr/bin/systemctl reboot -i
EOF
sudo chmod +x "$REBOOT_HELPER"

echo "$PI_USER ALL=(root) NOPASSWD: $REBOOT_HELPER" | sudo tee "$SUDOERS_FILE" >/dev/null
sudo chmod 440 "$SUDOERS_FILE"
sudo visudo -cf "$SUDOERS_FILE" >/dev/null

cat > /tmp/gcam-controller-sudoers <<EOF
$PI_USER ALL=(root) NOPASSWD: /usr/bin/systemctl start gcam.service
$PI_USER ALL=(root) NOPASSWD: /usr/bin/systemctl stop gcam.service
$PI_USER ALL=(root) NOPASSWD: /usr/bin/systemctl restart gcam.service
$PI_USER ALL=(root) NOPASSWD: /usr/bin/systemctl is-active gcam.service
EOF
sudo cp /tmp/gcam-controller-sudoers "$GCAM_CONTROLLER_SUDOERS_FILE"
sudo chmod 440 "$GCAM_CONTROLLER_SUDOERS_FILE"
sudo visudo -cf "$GCAM_CONTROLLER_SUDOERS_FILE" >/dev/null
rm -f /tmp/gcam-controller-sudoers

echo "[14/17] Write templates..."
cat > "$APP_DIR/templates/live.html" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>G-Cam Live</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    html, body { margin:0; padding:0; background:#000; height:100%; }
    .wrap { display:flex; align-items:center; justify-content:center; height:100%; }
    img { width:100%; height:auto; max-height:100vh; object-fit:contain; display:block; }
  </style>
</head>
<body>
  <div class="wrap">
    <img src="{{ stream_endpoint }}" alt="Live Feed">
  </div>
</body>
</html>
HTML

cat > "$APP_DIR/templates/live_talk.html" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>G-Cam Live Talk</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root{
      --bg:#07101b;
      --card:#101a2e;
      --card2:#16233f;
      --line:#263657;
      --text:#eef4ff;
      --muted:#a7b9da;
      --accent:#39c6ff;
      --danger:#ef4444;
      --ok:#22c55e;
    }
    *{box-sizing:border-box}
    body{
      margin:0;
      min-height:100vh;
      font-family:Arial,Helvetica,sans-serif;
      background:linear-gradient(180deg,#07101b,#0f172a);
      color:var(--text);
      display:flex;
      align-items:center;
      justify-content:center;
      padding:16px;
    }
    .card{
      width:min(520px,100%);
      background:var(--card);
      border:1px solid var(--line);
      border-radius:20px;
      padding:20px;
      box-shadow:0 20px 45px rgba(0,0,0,.3);
    }
    h1{
      margin:0 0 8px;
      font-size:28px;
    }
    .muted{
      color:var(--muted);
      font-size:14px;
      line-height:1.5;
      margin-bottom:16px;
    }
    .talk-btn{
      width:100%;
      height:150px;
      border:none;
      border-radius:24px;
      background:var(--accent);
      color:#001421;
      font-size:30px;
      font-weight:800;
      cursor:pointer;
      touch-action:none;
      user-select:none;
      box-shadow:0 12px 30px rgba(57,198,255,.25);
    }
    .talk-btn.talking{
      background:var(--danger);
      color:#fff;
      box-shadow:0 12px 30px rgba(239,68,68,.25);
    }
    .stop-btn{
      width:100%;
      margin-top:12px;
      border:1px solid var(--line);
      border-radius:14px;
      background:var(--card2);
      color:var(--text);
      padding:12px;
      font-size:16px;
      cursor:pointer;
    }
    .status{
      margin-top:14px;
      padding:12px;
      border-radius:12px;
      background:var(--card2);
      border:1px solid var(--line);
      min-height:46px;
      font-size:14px;
      color:var(--muted);
      white-space:pre-wrap;
    }
    .ok{color:var(--ok)}
    .bad{color:#ff9b9b}
  </style>
</head>
<body>
  <div class="card">
    <h1>Live Talk</h1>
    <div class="muted">
      Device: <strong>{{ device_id }}</strong><br>
      Press and hold the button. Your mobile microphone will play live on the device speaker.
    </div>

    <button id="talkBtn" class="talk-btn">HOLD TO TALK</button>
    <button class="stop-btn" onclick="forceStop()">Stop / Clear</button>

    <div id="status" class="status">Ready.</div>
  </div>

<script>
const CHUNK_MS = {{ chunk_ms }};
const LIVE_TALK_TOKEN = {{ command_token | tojson }};

const MAX_CLIENT_QUEUE = 30;

const talkBtn = document.getElementById('talkBtn');
const statusEl = document.getElementById('status');

let mediaStream = null;
let talking = false;
let recorder = null;
let uploadCount = 0;
let activePointerId = null;
let clientQueue = [];
let uploadLoopRunning = false;
let activeUploadController = null;
let talkSession = 0;
let stoppingTalk = false;
let pendingUploads = 0;

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function setStatus(message, cls='') {
  statusEl.className = 'status ' + cls;
  statusEl.textContent = message;
}

function getToken() {
  return LIVE_TALK_TOKEN || '';
}

async function postJson(url, body = {}) {
  const token = getToken();

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-GCam-Token': token
    },
    body: JSON.stringify({...body, token})
  });

  const data = await response.json().catch(() => ({}));

  if (!response.ok || !data.ok) {
    throw new Error(data.error || ('HTTP ' + response.status));
  }

  return data;
}

async function uploadChunk(blob, sessionId) {
  if (!blob || blob.size <= 0 || sessionId !== talkSession || (!talking && !stoppingTalk)) {
    return;
  }

  const token = getToken();
  const formData = new FormData();
  formData.append('token', token);
  formData.append('audio', blob, 'live_talk.webm');

  activeUploadController = new AbortController();
  pendingUploads += 1;

  try {
    const response = await fetch('/api/live_talk/chunk', {
      method: 'POST',
      headers: {
        'X-GCam-Token': token
      },
      body: formData,
      signal: activeUploadController.signal
    });

    const data = await response.json().catch(() => ({}));

    if (!response.ok || !data.ok) {
      throw new Error(data.error || ('HTTP ' + response.status));
    }

    if (talking && sessionId === talkSession) {
      uploadCount += 1;
      setStatus(
        `Talking live...\nChunks sent: ${uploadCount}\nDevice queue: ${data.queue_size}\nMobile queue: ${clientQueue.length}`,
        'ok'
      );
    }

  } finally {
    pendingUploads = Math.max(0, pendingUploads - 1);
    activeUploadController = null;
  }
}

function enqueueChunk(blob) {
  if ((!talking && !stoppingTalk) || !blob || blob.size <= 0) {
    return;
  }

  if (clientQueue.length >= MAX_CLIENT_QUEUE) {
    setStatus(
      `Network is slow. Holding audio...\nMobile queue: ${clientQueue.length}`,
      'bad'
    );
    return;
  }

  clientQueue.push(blob);
  runUploadLoop(talkSession);
}

async function runUploadLoop(sessionId) {
  if (uploadLoopRunning) {
    return;
  }

  uploadLoopRunning = true;

  try {
    while ((talking || stoppingTalk) && sessionId === talkSession && clientQueue.length > 0) {
      const blob = clientQueue.shift();

      try {
        await uploadChunk(blob, sessionId);
      } catch (err) {
        if (talking && sessionId === talkSession) {
          setStatus('Chunk upload failed: ' + err.message, 'bad');
        }
      }
    }
  } finally {
    uploadLoopRunning = false;

    if ((talking || stoppingTalk) && sessionId === talkSession && clientQueue.length > 0) {
      setTimeout(() => runUploadLoop(sessionId), 20);
    }
  }
}

function getSupportedMimeType() {
  const types = [
    'audio/webm;codecs=opus',
    'audio/webm',
    'audio/ogg;codecs=opus',
    'audio/ogg'
  ];

  for (const type of types) {
    if (window.MediaRecorder && MediaRecorder.isTypeSupported(type)) {
      return type;
    }
  }

  return '';
}

async function startTalk(event) {
  if (talking) {
    return;
  }

  try {
    if (event && event.pointerId !== undefined) {
      activePointerId = event.pointerId;
      try {
        talkBtn.setPointerCapture(activePointerId);
      } catch (e) {}
    }

    setStatus('Starting microphone...');

    mediaStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: false,
        noiseSuppression: false,
        autoGainControl: true,
        channelCount: 1,
        sampleRate: 48000,
        sampleSize: 16
      },
      video: false
    });

    await postJson('/api/live_talk/start');

    const mimeType = getSupportedMimeType();
    const options = mimeType
      ? {mimeType, audioBitsPerSecond: 96000}
      : {audioBitsPerSecond: 96000};

    recorder = new MediaRecorder(mediaStream, options);

    recorder.ondataavailable = event => {
      if ((!talking && !stoppingTalk) || !event.data || event.data.size <= 0) {
        return;
      }

      enqueueChunk(event.data);
    };

    recorder.onerror = event => {
      const message = event && event.error ? event.error.message : 'recorder_error';
      setStatus('Recorder error: ' + message, 'bad');
      stopTalk(true);
    };

    uploadCount = 0;
    clientQueue = [];
    pendingUploads = 0;
    stoppingTalk = false;
    talkSession += 1;
    talking = true;

    talkBtn.classList.add('talking');
    talkBtn.textContent = 'TALKING...';
    setStatus('Talking live...', 'ok');

    recorder.start(CHUNK_MS);

  } catch (err) {
    setStatus('Start failed: ' + err.message, 'bad');
    await stopTalk(false);
  }
}

async function waitForRecorderStop() {
  return new Promise(resolve => {
    if (!recorder || recorder.state === 'inactive') {
      resolve();
      return;
    }

    const oldOnStop = recorder.onstop;

    recorder.onstop = event => {
      try {
        if (typeof oldOnStop === 'function') {
          oldOnStop(event);
        }
      } catch (e) {}

      resolve();
    };

    try {
      if (recorder.state === 'recording' && recorder.requestData) {
        recorder.requestData();
      }
    } catch (e) {}

    try {
      recorder.stop();
    } catch (e) {
      resolve();
    }

    setTimeout(resolve, 1500);
  });
}

async function waitForClientDrain(sessionId, maxMs = 7000) {
  const startedAt = Date.now();

  while (sessionId === talkSession) {
    if (clientQueue.length <= 0 && !uploadLoopRunning && pendingUploads <= 0) {
      return true;
    }

    if (Date.now() - startedAt >= maxMs) {
      return false;
    }

    setStatus(
      `Finishing announcement...\nMobile queue: ${clientQueue.length}\nUploads: ${pendingUploads}`,
      'ok'
    );

    await sleep(80);
  }

  return false;
}

async function stopTalk(callServer = true, graceful = true) {
  const wasTalking = talking || stoppingTalk;

  if (!wasTalking) {
    return;
  }

  const sessionId = talkSession;

  talking = false;
  stoppingTalk = true;

  try {
    if (!graceful && activeUploadController) {
      activeUploadController.abort();
    }
  } catch (e) {}

  if (!graceful) {
    activeUploadController = null;
    clientQueue = [];
  }

  try {
    if (activePointerId !== null) {
      talkBtn.releasePointerCapture(activePointerId);
    }
  } catch (e) {}

  activePointerId = null;

  await waitForRecorderStop();
  recorder = null;

  if (mediaStream) {
    mediaStream.getTracks().forEach(track => track.stop());
    mediaStream = null;
  }

  talkBtn.classList.remove('talking');
  talkBtn.textContent = 'HOLD TO TALK';

  if (graceful) {
    await waitForClientDrain(sessionId, 7000);
  }

  stoppingTalk = false;

  if (callServer || wasTalking) {
    try {
      await postJson('/api/live_talk/stop', {
        mode: graceful ? 'graceful' : 'hard'
      });

      setStatus(
        graceful ? 'Released. Device is finishing remaining audio.' : 'Stopped.',
        'ok'
      );
    } catch (err) {
      setStatus('Stopped locally. Server stop failed: ' + err.message, 'bad');
    }
  }

  clientQueue = [];
  talkSession += 1;
}

async function forceStop() {
  await stopTalk(true, false);
}

talkBtn.addEventListener('pointerdown', event => {
  event.preventDefault();
  startTalk(event);
});

talkBtn.addEventListener('pointerup', event => {
  event.preventDefault();
  stopTalk(true);
});

talkBtn.addEventListener('pointercancel', event => {
  event.preventDefault();
  stopTalk(true);
});

window.addEventListener('beforeunload', () => {
  if (talking) {
    try {
      const formData = new FormData();
      formData.append('token', getToken());
      navigator.sendBeacon('/api/live_talk/stop', formData);
    } catch (e) {}
  }
});

if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.MediaRecorder) {
  setStatus('This browser does not support microphone recording. Use Chrome/Edge/Safari over HTTPS.', 'bad');
}
</script>
</body>
</html>
HTML


cat > "$APP_DIR/templates/index.html" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>G-Cam "RealTech Systems"</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root{
      --bg:#08111f;--card:#101a2e;--card2:#16233f;--line:#263657;--text:#eef4ff;--muted:#a7b9da;
      --accent:#39c6ff;--high:#ef4444;--med:#f59e0b;--low:#22c55e;--shadow:0 20px 45px rgba(0,0,0,.25);
    }
    *{box-sizing:border-box}
    body{margin:0;font-family:Arial,Helvetica,sans-serif;background:linear-gradient(180deg,#07101b,#0f172a 45%,#0b1320);color:var(--text)}
    .wrap{width:min(1450px,95%);margin:0 auto;padding:20px 0 40px}
    .header{display:flex;justify-content:space-between;gap:20px;align-items:center;background:rgba(16,26,46,.95);border:1px solid var(--line);border-radius:18px;box-shadow:var(--shadow);padding:20px 24px;margin-bottom:18px}
    .title h1{margin:0;font-size:34px;line-height:1.1}
    .title p{margin:8px 0 0;color:var(--muted);font-size:14px}
    .meta{color:var(--muted);text-align:right;font-size:14px;line-height:1.8}
    .sev-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-bottom:18px}
    .sev-card{background:var(--card);border:1px solid var(--line);border-radius:16px;box-shadow:var(--shadow);padding:18px;min-height:118px}
    .sev-label{color:var(--muted);font-size:14px;margin-bottom:10px;letter-spacing:.4px;text-transform:uppercase}
    .sev-value{font-size:54px;font-weight:700;line-height:1}
    .high .sev-value{color:var(--high)}
    .medium .sev-value{color:var(--med)}
    .low .sev-value{color:var(--low)}
    .tabs{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:16px}
    .tab-btn{background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:12px;padding:12px 16px;font-weight:700;cursor:pointer}
    .tab-btn.active{background:var(--accent);color:#001421;border-color:transparent}
    .panel{display:none;background:var(--card);border:1px solid var(--line);border-radius:18px;box-shadow:var(--shadow);padding:18px}
    .panel.active{display:block}
    .panel-grid{display:grid;grid-template-columns:2fr 1fr;gap:18px;align-items:start}
    .media-card,.info-box{background:var(--card2);border:1px solid var(--line);border-radius:16px}
    .media-card h3{margin:0;padding:14px 16px;border-bottom:1px solid var(--line);font-size:18px}
    .media-body{padding:14px}
    .media-body img{display:block;width:100%;border-radius:12px;border:1px solid var(--line);background:#000}
    .info{display:grid;gap:12px}
    .info-box{padding:14px}
    .info-box h4{margin:0 0 8px;color:var(--muted);font-size:13px;letter-spacing:.6px;text-transform:uppercase}
    .big{font-size:24px;font-weight:700;word-break:break-word}
    .badge{display:inline-block;padding:6px 12px;border-radius:999px;font-size:13px;font-weight:700}
    .badge.high{background:rgba(239,68,68,.15);border:1px solid rgba(239,68,68,.35);color:#ff9191}
    .badge.medium{background:rgba(245,158,11,.15);border:1px solid rgba(245,158,11,.35);color:#ffd07f}
    .badge.low{background:rgba(34,197,94,.15);border:1px solid rgba(34,197,94,.35);color:#8bffb2}
    .badge.none{background:rgba(148,163,184,.15);border:1px solid rgba(148,163,184,.35);color:#d5def0}
    .footer-note{color:var(--muted);margin-top:16px;font-size:13px}
    .link-box{word-break:break-all}
    .mono{font-family:Consolas,monospace}

    .controls-grid{
      display:grid;
      grid-template-columns:repeat(3,1fr);
      gap:14px;
      align-items:stretch;
    }
    .control-card{
      background:var(--card2);
      border:1px solid var(--line);
      border-radius:16px;
      padding:14px;
      min-height:150px;
    }
    .control-card h4{
      margin:0 0 10px;
      color:var(--muted);
      font-size:13px;
      letter-spacing:.6px;
      text-transform:uppercase;
    }
    .control-title{
      font-size:20px;
      font-weight:800;
      margin-bottom:10px;
      line-height:1.2;
    }
    .control-status{
      display:inline-block;
      padding:6px 10px;
      border-radius:999px;
      font-size:12px;
      font-weight:800;
      margin-bottom:10px;
      background:rgba(148,163,184,.15);
      border:1px solid rgba(148,163,184,.35);
      color:#d5def0;
    }
    .control-status.on{
      background:rgba(34,197,94,.15);
      border-color:rgba(34,197,94,.35);
      color:#8bffb2;
    }
    .control-status.off{
      background:rgba(239,68,68,.15);
      border-color:rgba(239,68,68,.35);
      color:#ff9b9b;
    }
    .control-actions{
      display:flex;
      flex-wrap:wrap;
      gap:8px;
      margin-top:8px;
    }
    .control-btn{
      border:1px solid var(--line);
      background:var(--card);
      color:var(--text);
      border-radius:10px;
      padding:9px 12px;
      cursor:pointer;
      font-weight:800;
      font-size:13px;
    }
    .control-btn.primary{
      background:var(--accent);
      color:#001421;
      border-color:transparent;
    }
    .control-btn.danger{
      background:rgba(239,68,68,.18);
      border-color:rgba(239,68,68,.4);
      color:#ffb4b4;
    }
    .control-btn.ok{
      background:rgba(34,197,94,.18);
      border-color:rgba(34,197,94,.4);
      color:#a7ffc1;
    }
    .control-btn:disabled{
      opacity:.55;
      cursor:not-allowed;
    }
    .control-wide{
      grid-column:span 2;
    }
    .volume-row{
      display:flex;
      gap:10px;
      align-items:center;
      flex-wrap:wrap;
      margin-top:10px;
    }
    .volume-row input[type="range"]{
      width:min(360px,100%);
    }
    .control-status-line{
      margin-top:14px;
      padding:12px;
      border-radius:12px;
      background:var(--card2);
      border:1px solid var(--line);
      color:var(--muted);
      min-height:44px;
      font-size:13px;
    }

    body.geofence-page-only{
      background:#08111f;
      overflow-x:hidden;
    }

    body.geofence-page-only .header,
    body.geofence-page-only .sev-grid,
    body.geofence-page-only .tabs,
    body.geofence-page-only .footer-note{
      display:none !important;
    }

    body.geofence-page-only .wrap{
      width:min(980px,100%);
      margin:0 auto;
      padding:6px;
    }

    body.geofence-page-only .panel{
      display:none;
      padding:12px;
      border-radius:10px;
      box-shadow:none;
      margin:0;
    }

    body.geofence-page-only #tab-geofence.panel.active,
    body.geofence-page-only #tab-person_geofence.panel.active,
    body.geofence-page-only #tab-vehicle_geofence.panel.active{
      display:block;
    }

    body.geofence-page-only #tab-geofence > div:first-child,
    body.geofence-page-only #tab-person_geofence > div:first-child,
    body.geofence-page-only #tab-vehicle_geofence > div:first-child{
      margin-bottom:10px !important;
      font-size:13px;
      line-height:1.4;
    }

    body.geofence-page-only canvas{
      max-width:100%;
      height:auto;
    }

    body.geofence-page-only .info-box{
      box-shadow:none;
    }

    @media (max-width:980px){
      .header{flex-direction:column;align-items:flex-start}
      .meta{text-align:left}
      .panel-grid{grid-template-columns:1fr}
      .sev-grid{grid-template-columns:1fr}
      .controls-grid{grid-template-columns:1fr}
      .control-wide{grid-column:span 1}

      body.geofence-page-only .wrap{
        width:100%;
        padding:4px;
      }
    }
  </style>
</head>
<body class="{{ 'geofence-page-only' if geofence_page_only else '' }}">
<div class="wrap">
  <div class="header">
    <div class="title">
      <h1>G-Cam "RealTech Systems"</h1>
      <p>Live stream, person detect, garbage detect, MQTT audio and device status</p>
    </div>
    <div class="meta">
      <div><strong>Device ID:</strong> {{ state.device_id }}</div>
      <div><strong>Camera:</strong> {{ state.camera_ip }}</div>
      <div><strong>Raspberry Pi:</strong> {{ state.raspberry_ip }}</div>
      <div><strong>Public IP:</strong> {{ state.public_ip or "Unavailable" }}</div>
      <div><strong>Last Frame:</strong> {{ state.system.last_frame_at or "Waiting..." }}</div>
    </div>
  </div>

  <div class="sev-grid">
    <div class="sev-card high"><div class="sev-label">HIGH</div><div class="sev-value" id="highCount">{{ state.counts.HIGH }}</div></div>
    <div class="sev-card medium"><div class="sev-label">MEDIUM</div><div class="sev-value" id="mediumCount">{{ state.counts.MEDIUM }}</div></div>
    <div class="sev-card low"><div class="sev-label">LOW</div><div class="sev-value" id="lowCount">{{ state.counts.LOW }}</div></div>
  </div>

  <div class="tabs">
    <button class="tab-btn active" onclick="showTab('live', this)">Live Feed</button>
    <button class="tab-btn" onclick="showTab('garbage', this)">Last Garbage Detection</button>
    <button class="tab-btn" onclick="showTab('person', this)">Last Person Detection</button>
	<button class="tab-btn" onclick="showTab('vehicle', this)">Last Vehicle Detection</button>
	<button class="tab-btn" onclick="showTab('geofence', this)">Garbage Geofence</button>
	<button class="tab-btn" onclick="showTab('person_geofence', this)">Person Geofence</button>
	<button class="tab-btn" onclick="showTab('vehicle_geofence', this)">Vehicle Geofence</button>
    <button class="tab-btn" onclick="showTab('controls', this)">Controls</button>
    <button class="tab-btn" onclick="showTab('links', this)">Links & Status</button>
    <button class="tab-btn" onclick="showTab('device', this)">Device Health</button>
  </div>

  <div id="tab-live" class="panel active">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Live RTSP Feed</h3>
        <div class="media-body"><img src="/video_feed" alt="Live Feed"></div>
      </div>
      <div class="info">
        <div class="info-box"><h4>Camera Status</h4><div class="big" id="cameraStatus">{{ "Connected" if state.system.camera_connected else "Disconnected" }}</div></div>
        <div class="info-box"><h4>Last Error</h4><div id="lastError">{{ state.system.last_error or "None" }}</div></div>
        <div class="info-box"><h4>Garbage Mode</h4><div id="garbageMode">{{ state.system.garbage_mode }}</div></div>
        <div class="info-box"><h4>Latest Garbage Severity</h4><div id="garbageSeverity">{% set sev = state.last_garbage.severity %}{% if sev == "HIGH" %}<span class="badge high">HIGH</span>{% elif sev == "MEDIUM" %}<span class="badge medium">MEDIUM</span>{% elif sev == "LOW" %}<span class="badge low">LOW</span>{% else %}<span class="badge none">NONE</span>{% endif %}</div></div>
        <div class="info-box"><h4>Permanent Live Link</h4><div class="link-box" id="permanentLiveUrl">{{ state.live_link.url or "Inactive" }}</div></div>
        <div class="info-box"><h4>Live Link Status</h4><div id="permanentLiveStatus">{{ state.live_link.expires_at or "Inactive" }}</div></div>
        <div class="info-box"><h4>Camera Config Permanent Link</h4><div class="link-box" id="cameraConfigUrl">{{ state.camera_config_url or "Unavailable" }}</div></div>
      </div>
    </div>
  </div>

  <div id="tab-garbage" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last Garbage Detection Image</h3>
        <div class="media-body">{% if state.last_garbage.image %}<img id="garbageImg" src="/media/{{ state.last_garbage.image }}?t={{ ts }}" alt="Garbage">{% else %}<img id="garbageImg" src="" alt="No Garbage">{% endif %}</div>
      </div>
      <div class="info">
        <div class="info-box"><h4>Time</h4><div id="garbageTime">{{ state.last_garbage.time or "Waiting..." }}</div></div>
        <div class="info-box"><h4>Triggered By</h4><div id="garbageTriggeredBy">{{ state.last_garbage.triggered_by or "N/A" }}</div></div>
        <div class="info-box"><h4>Severity</h4><div class="big" id="garbageSeverityText">{{ state.last_garbage.severity or "NONE" }}</div></div>
        <div class="info-box"><h4>Garbage Count</h4><div class="big" id="garbageCount">{{ state.last_garbage.blob_count or 0 }}</div></div>
        <div class="info-box"><h4>Diff Ratio</h4><div id="garbageDiffRatio">{{ state.last_garbage.diff_ratio }}</div></div>
        <div class="info-box"><h4>Edge Ratio</h4><div id="garbageEdgeRatio">{{ state.last_garbage.edge_ratio }}</div></div>
        <div class="info-box"><h4>Blob Area Ratio</h4><div id="garbageBlobAreaRatio">{{ state.last_garbage.blob_area_ratio }}</div></div>
      </div>
    </div>
  </div>

  <div id="tab-person" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last Person Detection Image</h3>
        <div class="media-body">{% if state.last_person.image %}<img id="personImg" src="/media/{{ state.last_person.image }}?t={{ ts }}" alt="Person">{% else %}<img id="personImg" src="" alt="No Person">{% endif %}</div>
      </div>
      <div class="info">
        <div class="info-box"><h4>Time</h4><div id="personTime">{{ state.last_person.time or "Waiting..." }}</div></div>
        <div class="info-box"><h4>Person Count</h4><div class="big" id="personCount">{{ state.last_person.count or 0 }}</div></div>
        <div class="info-box"><h4>Status</h4><div id="personStatus">{% if state.last_person.detected %}Detected now{% elif state.last_person.image %}Last detection saved{% else %}No recent detection{% endif %}</div></div>
        <div class="info-box"><h4>Best Confidence</h4><div id="personBestConfidence">{{ state.last_person.best_confidence or 0 }}</div></div>
      </div>
    </div>
  </div>

     <div id="tab-vehicle" class="panel">
    <div class="panel-grid">
      <div class="media-card">
        <h3>Last Vehicle Detection Image</h3>
        <div class="media-body">
          {% if state.last_vehicle.image %}
            <img id="vehicleImg" src="/media/{{ state.last_vehicle.image }}?t={{ ts }}" alt="Vehicle">
          {% else %}
            <img id="vehicleImg" src="" alt="No Vehicle">
          {% endif %}
        </div>
      </div>
      <div class="info">
        <div class="info-box"><h4>Time</h4><div id="vehicleTime">{{ state.last_vehicle.time or "Waiting..." }}</div></div>
        <div class="info-box"><h4>Vehicle Count</h4><div class="big" id="vehicleCount">{{ state.last_vehicle.count or 0 }}</div></div>
        <div class="info-box"><h4>Status</h4><div id="vehicleStatus">{% if state.last_vehicle.detected %}Detected now{% elif state.last_vehicle.image %}Last detection saved{% else %}No recent detection{% endif %}</div></div>
        <div class="info-box"><h4>Best Confidence</h4><div id="vehicleBestConfidence">{{ state.last_vehicle.best_confidence or 0 }}</div></div>
        <div class="info-box">
          <h4>Vehicle Detection Mode</h4>
          <div class="big" id="vehicleDetectionMode">{{ state.system.vehicle_detection_mode or state.last_vehicle.mode or "static" }}</div>
          <button id="vehicleModeToggle" onclick="toggleVehicleDetectionMode()"
            style="margin-top:10px;background:var(--accent);color:#001421;border:none;border-radius:10px;padding:10px 14px;font-weight:700;cursor:pointer;font-size:14px">
            🚗 Switch Mode
          </button>
          <div style="color:var(--muted);font-size:12px;margin-top:8px;line-height:1.5">
            Static detects parked/stopped vehicles. Moving detects only motion-confirmed vehicles.
          </div>
        </div>
        <div class="info-box"><h4>Vehicle Labels</h4><div id="vehicleLabels">{{ ", ".join(state.last_vehicle.labels) if state.last_vehicle.labels else "None" }}</div></div>
        <div class="info-box"><h4>Geofence Overlap</h4><div id="vehicleGeofenceOverlap">{{ state.last_vehicle.geofence_overlap or 0 }}</div></div>
        <div class="info-box"><h4>Image Upload Status</h4><div id="vehicleImageStatus">{{ state.last_vehicle.image_status or "None" }}</div></div>
        <div class="info-box"><h4>Image Uploaded Path</h4><div class="link-box" id="vehicleImagePath">{{ state.last_vehicle.image_uploaded_path or "None" }}</div></div>
      </div>
    </div>
  </div>

  <div id="tab-controls" class="panel">
    <div style="margin-bottom:14px">
      <strong>Dashboard Controls</strong> — control detection, recordings, warning audio, garbage actions, audio queue, power saving and Pi volume directly from dashboard.
    </div>

    <div class="controls-grid">
      <div class="control-card">
        <h4>Detection</h4>
        <div class="control-title">👤 Person Detection</div>
        <div id="ctrlPersonDetectionStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetFeature('person_detection_enabled', true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetFeature('person_detection_enabled', false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Detection</h4>
        <div class="control-title">🚗 Vehicle Detection</div>
        <div id="ctrlVehicleDetectionStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetFeature('vehicle_detection_enabled', true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetFeature('vehicle_detection_enabled', false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Detection</h4>
        <div class="control-title">🗑️ Garbage Detection</div>
        <div id="ctrlGarbageDetectionStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetFeature('garbage_detection_enabled', true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetFeature('garbage_detection_enabled', false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Garbage Mode</h4>
        <div class="control-title">🧠 AI / Normal Mode</div>
        <div id="ctrlGarbageModeStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn primary" onclick="controlSetGarbageMode('ai')">AI Mode</button>
          <button class="control-btn" onclick="controlSetGarbageMode('normal')">Normal Mode</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Video Recording</h4>
        <div class="control-title">🎥 Person Video</div>
        <div id="ctrlPersonVideoStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetFeature('person_video_recording_enabled', true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetFeature('person_video_recording_enabled', false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Video Recording</h4>
        <div class="control-title">🎥 Vehicle Video</div>
        <div id="ctrlVehicleVideoStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetFeature('vehicle_video_recording_enabled', true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetFeature('vehicle_video_recording_enabled', false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Warning Audio</h4>
        <div class="control-title">🔊 Warning Audio</div>
        <div id="ctrlWarningAudioStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetWarningAudio(true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetWarningAudio(false)">Disable</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Garbage Action</h4>
        <div class="control-title">🧪 Manual Detect</div>
        <div class="control-status">On Demand</div>
        <div class="control-actions">
          <button class="control-btn primary" onclick="controlManualGarbage()">🧪 Run Detection</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Garbage Action</h4>
        <div class="control-title">📷 Sample Capture</div>
        <div class="control-status">Reference</div>
        <div class="control-actions">
          <button class="control-btn primary" onclick="controlGarbageSample()">📷 Capture Sample</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Audio Queue</h4>
        <div class="control-title">🧹 Clear Queue</div>
        <div id="ctrlAudioQueueStatus" class="control-status">Ready</div>
        <div class="control-actions">
          <button class="control-btn danger" onclick="controlClearAudioQueue()">🧹 Clear Audio Queue</button>
        </div>
      </div>

      <div class="control-card">
        <h4>Power</h4>
        <div class="control-title">⚡ Power Saving</div>
        <div id="ctrlPowerSavingStatus" class="control-status">Loading...</div>
        <div class="control-actions">
          <button class="control-btn ok" onclick="controlSetPowerSaving(true)">Enable</button>
          <button class="control-btn danger" onclick="controlSetPowerSaving(false)">Disable</button>
        </div>
      </div>

      <div class="control-card control-wide">
        <h4>Volume Control</h4>
        <div class="control-title">🔉 Pi Speaker Volume</div>
        <div id="ctrlVolumeStatus" class="control-status">Loading...</div>

        <div class="volume-row">
          <input id="ctrlVolumeRange" type="range" min="0" max="100" value="70" oninput="controlVolumePreview(this.value)">
          <strong id="ctrlVolumePreview">70%</strong>
          <button class="control-btn primary" onclick="controlSetVolumeFromSlider()">Set Volume</button>
          <button class="control-btn" onclick="controlRefreshVolume()">Refresh</button>
        </div>

        <div class="control-actions">
          <button class="control-btn" onclick="controlSetVolume(10)">10%</button>
          <button class="control-btn" onclick="controlSetVolume(25)">25%</button>
          <button class="control-btn" onclick="controlSetVolume(50)">50%</button>
          <button class="control-btn" onclick="controlSetVolume(75)">75%</button>
          <button class="control-btn" onclick="controlSetVolume(100)">100%</button>
        </div>
      </div>
    </div>

    <div id="controlsStatusLine" class="control-status-line">Controls ready.</div>
  </div>

  <div id="tab-links" class="panel">
    <div class="panel-grid">
      <div class="info">
        <div class="info-box"><h4>Local Dashboard URL</h4><div class="link-box" id="dashLocal">{{ local_dashboard }}</div></div>
        <div class="info-box"><h4>Local Stream URL</h4><div class="link-box" id="streamLocal">{{ local_stream }}</div></div>
        <div class="info-box"><h4>Permanent Live URL</h4><div class="link-box" id="permanentLiveUrl2">{{ state.live_link.url or "Inactive" }}</div></div>
        <div class="info-box"><h4>Camera Config Permanent URL</h4><div class="link-box" id="cameraConfigUrl2">{{ state.camera_config_url or "Unavailable" }}</div></div>
        <div class="info-box"><h4>MQTT Command Listener</h4><div id="commandListener">{{ "Connected" if state.system.command_listener else "Disconnected" }}</div></div>
      </div>
      <div class="info">
        <div class="info-box"><h4>SFTP Base Path</h4><div class="link-box" id="sftpBase">{{ sftp_base_dir }}</div></div>
        <div class="info-box"><h4>SFTP Person Path</h4><div class="link-box" id="sftpPerson">{{ sftp_person_dir }}</div></div>
        <div class="info-box"><h4>SFTP Person Video Path</h4><div class="link-box" id="sftpPersonVideo">{{ sftp_person_video_dir }}</div></div>
        <div class="info-box"><h4>SFTP Garbage Path</h4><div class="link-box" id="sftpGarbage">{{ sftp_garbage_dir }}</div></div>
        <div class="info-box"><h4>SFTP Audio Path</h4><div class="link-box" id="sftpAudio">{{ sftp_audio_dir }}</div></div>
        <div class="info-box"><h4>Command Examples</h4><div class="link-box mono">
{"command":"Live Stream","token":"***"}<br>
{"command":"Garbage Sample Capture","token":"***"}<br>
{"command":"Garbage Detect","token":"***"}<br>
{"command":"Garbage Detection Set to 3hours","token":"***"}<br>
{"command":"Audio File Play","audio_name":"Audio20260321.mp3","token":"***"}<br>
{"command":"Warning Audio Disable","token":"***"}<br>
{"command":"Warning Audio Enable","token":"***"}<br>
{"command":"Scheduled Warning Audio","start_time":"10AM","end_time":"6PM","token":"***"}<br>
{"command":"Scheduled Warning Disabled","token":"***"}<br>
{"command":"Data Status","token":"***"}<br>
{"command":"Device Restart","token":"***"}<br>
{"command":"Close The Software","token":"***"}<br>
{"command":"Open The Software","token":"***"}<br>
{"command":"Current Pi Volume","token":"***"}<br>
{"command":"Camera Live URL","token":"***"}<br>
{"command":"Person Detection Enable","token":"***"}<br>
{"command":"Person Detection Disable","token":"***"}<br>
{"command":"Vehicle Detection Enable","token":"***"}<br>
{"command":"Vehicle Detection Disable","token":"***"}<br>
{"command":"Garbage Detection Enable","token":"***"}<br>
{"command":"Garbage Detection Disable","token":"***"}<br>
{"command":"Garbage AI Mode","token":"***"}<br>
{"command":"Garbage Normal Mode","token":"***"}<br>
{"command":"Person Video Recording Enable","token":"***"}<br>
{"command":"Person Video Recording Disable","token":"***"}<br>
{"command":"Vehicle Video Recording Enable","token":"***"}<br>
{"command":"Vehicle Video Recording Disable","token":"***"}<br>
{"command":"Pi Volume 10%","token":"***"}<br>
{"command":"Pi Volume 20%","token":"***"}<br>
{"command":"Pi Volume 30%","token":"***"}<br>
{"command":"Pi Volume 40%","token":"***"}<br>
{"command":"Pi Volume 50%","token":"***"}<br>
{"command":"Pi Volume 60%","token":"***"}<br>
{"command":"Pi Volume 70%","token":"***"}<br>
{"command":"Pi Volume 80%","token":"***"}<br>
{"command":"Pi Volume 90%","token":"***"}<br>
{"command":"Pi Volume 100%","token":"***"}<br>
{"command":"Pi Volume 75%","token":"***"}
        </div></div>
      </div>
    </div>
  </div>

  <div id="tab-device" class="panel">
    <div class="panel-grid">
      <div class="info">
        <div class="info-box"><h4>Pi Temperature</h4><div class="big" id="piTemp">{{ state.system.pi_temperature_c or "Unavailable" }}</div></div>
        <div class="info-box"><h4>RAM Usage</h4><div class="big" id="ramUsage">{{ state.system.ram_percent or "Unavailable" }}</div></div>
        <div class="info-box"><h4>RAM Used / Total</h4><div id="ramDetail">{{ state.system.ram_used_mb or "Unavailable" }} / {{ state.system.ram_total_mb or "Unavailable" }}</div></div>
      </div>
      <div class="info">
        <div class="info-box"><h4>Network Status</h4><div id="networkStatus">{{ state.system.network_status or "Unknown" }}</div></div>
        <div class="info-box"><h4>Network Interface</h4><div id="networkIface">{{ state.system.network_iface or "Unknown" }}</div></div>
        <div class="info-box"><h4>Link Speed Mbps</h4><div id="networkLinkSpeed">{{ state.system.network_link_speed_mbps or "Unknown" }}</div></div>
        <div class="info-box"><h4>Last Audio</h4><div class="link-box" id="lastAudio">{{ state.last_audio.file or "None" }}</div></div>
        <div class="info-box"><h4>Last Audio Status</h4><div id="lastAudioStatus">{{ state.last_audio.status or "None" }}</div></div>
      </div>
    </div>
  </div>
  
  <div id="tab-geofence" class="panel">
    <div style="margin-bottom:14px">
      <strong>Draw garbage detection polygon zone</strong> — click points on the image.
      Minimum 3 points, maximum 20 points. The polygon area is the only area where garbage will be detected.
    </div>

    <div style="display:flex;gap:14px;align-items:flex-start;flex-wrap:wrap">
      <div style="position:relative;display:inline-block;border:2px solid var(--accent);border-radius:12px;overflow:hidden">
        <canvas id="geoCanvas" style="display:block;cursor:crosshair;max-width:100%"></canvas>
        <div id="geoMsg" style="position:absolute;top:8px;left:10px;background:rgba(0,0,0,.6);color:#fff;padding:4px 10px;border-radius:8px;font-size:13px;pointer-events:none">
          Loading snapshot...
        </div>
      </div>

      <div style="display:grid;gap:12px;min-width:230px">
        <div class="info-box">
          <h4>Current Polygon Points</h4>
          <div id="geoRoiDisplay" style="font-family:monospace;font-size:13px;white-space:pre-wrap">—</div>
        </div>
        <div class="info-box">
          <h4>Status</h4>
          <div id="geoStatus">—</div>
        </div>
        <button onclick="geoSave()"
          style="background:var(--accent);color:#001421;border:none;border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          ✅ Save Polygon
        </button>
        <button onclick="geoUndo()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          ↩️ Undo Last Point
        </button>
        <button onclick="geoClear()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          🧹 Clear Points
        </button>
        <button onclick="geoReset()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          🔄 Reset to Default
        </button>
        <button onclick="geoRefreshSnapshot()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          📷 Refresh Snapshot
        </button>
        <div style="color:var(--muted);font-size:12px;line-height:1.6">
          Click multiple points around the required garbage area.<br>
          The final point automatically connects to the first point.<br>
          Supports 3 to 20 points.
        </div>
      </div>
    </div>
  </div>

  <div id="tab-person_geofence" class="panel">
    <div style="margin-bottom:14px">
      <strong>Draw person detection polygon zone</strong> — click points on the image.
      Only persons whose foot point falls inside this polygon will trigger detection.
    </div>

    <div style="display:flex;gap:14px;align-items:flex-start;flex-wrap:wrap">
      <div style="position:relative;display:inline-block;border:2px solid var(--accent);border-radius:12px;overflow:hidden">
        <canvas id="personGeoCanvas" style="display:block;cursor:crosshair;max-width:100%"></canvas>
        <div id="personGeoMsg" style="position:absolute;top:8px;left:10px;background:rgba(0,0,0,.6);color:#fff;padding:4px 10px;border-radius:8px;font-size:13px;pointer-events:none">
          Loading snapshot...
        </div>
      </div>

      <div style="display:grid;gap:12px;min-width:230px">
        <div class="info-box">
          <h4>Current Polygon Points</h4>
          <div id="personGeoRoiDisplay" style="font-family:monospace;font-size:13px;white-space:pre-wrap">—</div>
        </div>
        <div class="info-box">
          <h4>Status</h4>
          <div id="personGeoStatus">—</div>
        </div>
        <button onclick="personGeoSave()"
          style="background:var(--accent);color:#001421;border:none;border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          ✅ Save Polygon
        </button>
        <button onclick="personGeoUndo()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          ↩️ Undo Last Point
        </button>
        <button onclick="personGeoClear()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          🧹 Clear Points
        </button>
        <button onclick="personGeoReset()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          🔄 Reset to Default
        </button>
        <button onclick="personGeoRefreshSnapshot()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          📷 Refresh Snapshot
        </button>
        <div style="color:var(--muted);font-size:12px;line-height:1.6">
          Click multiple points around the required person area.<br>
          The final point automatically connects to the first point.<br>
          Supports 3 to 20 points.
        </div>
      </div>
    </div>
  </div>

  <div id="tab-vehicle_geofence" class="panel">
    <div style="margin-bottom:14px">
      <strong>Draw vehicle detection polygon zone</strong> — click points on the image.
      Vehicle detection triggers only when enough of the full vehicle box overlaps this polygon.
      This avoids wrong detection when only tyres or road-split bottom parts enter the zone.
    </div>

    <div style="display:flex;gap:14px;align-items:flex-start;flex-wrap:wrap">
      <div style="position:relative;display:inline-block;border:2px solid var(--accent);border-radius:12px;overflow:hidden">
        <canvas id="vehicleGeoCanvas" style="display:block;cursor:crosshair;max-width:100%"></canvas>
        <div id="vehicleGeoMsg" style="position:absolute;top:8px;left:10px;background:rgba(0,0,0,.6);color:#fff;padding:4px 10px;border-radius:8px;font-size:13px;pointer-events:none">
          Loading snapshot...
        </div>
      </div>

      <div style="display:grid;gap:12px;min-width:230px">
        <div class="info-box">
          <h4>Current Polygon Points</h4>
          <div id="vehicleGeoRoiDisplay" style="font-family:monospace;font-size:13px;white-space:pre-wrap">—</div>
        </div>
        <div class="info-box">
          <h4>Status</h4>
          <div id="vehicleGeoStatus">—</div>
        </div>
        <button onclick="vehicleGeoSave()"
          style="background:var(--accent);color:#001421;border:none;border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          ✅ Save Polygon
        </button>
        <button onclick="vehicleGeoUndo()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          ↩️ Undo Last Point
        </button>
        <button onclick="vehicleGeoClear()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          🧹 Clear Points
        </button>
        <button onclick="vehicleGeoReset()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:12px 18px;font-weight:700;cursor:pointer;font-size:15px">
          🔄 Reset to Default
        </button>
        <button onclick="vehicleGeoRefreshSnapshot()"
          style="background:var(--card2);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px 18px;cursor:pointer;font-size:13px">
          📷 Refresh Snapshot
        </button>
        <div style="color:var(--muted);font-size:12px;line-height:1.6">
          Vehicle validation uses bounding-box overlap, not only bottom/tyre point.<br>
          Default required overlap: 35%.<br>
          Supports 3 to 20 points.
        </div>
      </div>
    </div>
  </div>



  <div class="footer-note">
    Local dashboard: <strong>{{ local_dashboard }}</strong><br>
    Local stream: <strong>{{ local_stream }}</strong><br>
    Permanent live: <strong>{{ state.live_link.url or "Inactive" }}</strong><br>
    Camera config: <strong>{{ state.camera_config_url or "Unavailable" }}</strong>
  </div>
</div>

<script>
// ============ POLYGON GEOFENCE EDITOR ============
function createPolygonEditor(options) {
  const canvas = document.getElementById(options.canvasId);
  const ctx = canvas.getContext('2d');
  const messageEl = document.getElementById(options.messageId);
  const displayEl = document.getElementById(options.displayId);
  const statusEl = document.getElementById(options.statusId);

  let image = new Image();
  let points = [];
  let maxPoints = 20;
  let minPoints = 3;

  function canvasPos(e) {
    const r = canvas.getBoundingClientRect();
    const scaleX = canvas.width / r.width;
    const scaleY = canvas.height / r.height;

    return {
      x: (e.clientX - r.left) * scaleX,
      y: (e.clientY - r.top) * scaleY
    };
  }

  function polygonFromBox(box) {
    if (!box || box.length !== 4) return [];
    return [
      [box[0], box[1]],
      [box[2], box[1]],
      [box[2], box[3]],
      [box[0], box[3]]
    ];
  }

  function draw() {
    if (!image || !image.complete || !canvas.width || !canvas.height) {
      return;
    }

    ctx.drawImage(image, 0, 0, canvas.width, canvas.height);

    if (points.length > 0) {
      ctx.lineWidth = 2.5;
      ctx.strokeStyle = options.lineColor;
      ctx.fillStyle = options.fillColor;

      ctx.beginPath();
      points.forEach((point, idx) => {
        const x = point[0] * canvas.width;
        const y = point[1] * canvas.height;

        if (idx === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
      });

      if (points.length >= 3) {
        ctx.closePath();
        ctx.fill();
      }

      ctx.stroke();

      points.forEach((point, idx) => {
        const x = point[0] * canvas.width;
        const y = point[1] * canvas.height;

        ctx.beginPath();
        ctx.arc(x, y, 5, 0, Math.PI * 2);
        ctx.fillStyle = options.pointColor;
        ctx.fill();

        ctx.font = 'bold 12px Arial';
        ctx.fillStyle = options.textColor;
        ctx.fillText(String(idx + 1), x + 7, y - 7);
      });

      ctx.font = 'bold 13px Arial';
      ctx.fillStyle = options.textColor;
      ctx.fillText(options.label, 10, 22);
    }

    displayEl.textContent = points.length
      ? points.map((p, i) => `${i + 1}: x=${p[0].toFixed(4)}, y=${p[1].toFixed(4)}`).join('\n')
      : 'No points selected';
  }

  canvas.addEventListener('pointerdown', e => {
    e.preventDefault();

    if (!canvas.width || !canvas.height) {
      statusEl.textContent = '⚠️ Snapshot not loaded yet';
      return;
    }

    if (points.length >= maxPoints) {
      statusEl.textContent = `⚠️ Maximum ${maxPoints} points allowed`;
      return;
    }

    const pos = canvasPos(e);
    const xRatio = Math.max(0, Math.min(1, pos.x / canvas.width));
    const yRatio = Math.max(0, Math.min(1, pos.y / canvas.height));

    points.push([xRatio, yRatio]);
    statusEl.textContent = `Point ${points.length} added`;
    draw();
  });

  async function refreshSnapshot() {
    messageEl.textContent = 'Loading snapshot...';
    statusEl.textContent = 'Loading saved polygon...';

    image = new Image();

    image.onload = async () => {
      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;
      ctx.drawImage(image, 0, 0);
      messageEl.textContent = 'Click points to draw polygon';

      try {
        const resp = await fetch(options.getUrl + '?t=' + Date.now());
        const data = await resp.json();

        minPoints = data.min_points || 3;
        maxPoints = data.max_points || 20;

        const serverPolygon = data[options.polygonKey];
        const fallbackBox = data[options.roiKey];

        if (serverPolygon && serverPolygon.length >= minPoints) {
          points = serverPolygon;
        } else {
          points = polygonFromBox(fallbackBox);
        }

        statusEl.textContent = `Loaded ${points.length} points`;
        draw();
      } catch (e) {
        points = [];
        statusEl.textContent = '⚠️ Could not load saved polygon';
        draw();
      }
    };

    image.onerror = () => {
      messageEl.textContent = 'No frame available — camera connecting?';
      statusEl.textContent = '❌ Snapshot unavailable';
    };

    image.src = '/api/snapshot?t=' + Date.now();
  }

  async function save() {
    if (points.length < minPoints) {
      statusEl.textContent = `⚠️ Add at least ${minPoints} points`;
      return;
    }

    if (points.length > maxPoints) {
      statusEl.textContent = `⚠️ Maximum ${maxPoints} points allowed`;
      return;
    }

    statusEl.textContent = 'Saving polygon...';

    try {
      const payload = {};
      payload[options.polygonKey] = points;

      const resp = await fetch(options.postUrl, {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(payload)
      });

      const data = await resp.json();

      if (data.ok) {
        points = data[options.polygonKey] || points;
        statusEl.textContent = `✅ Saved ${points.length}-point polygon. Applied immediately.`;
        draw();
      } else {
        statusEl.textContent = '❌ Error: ' + (data.error || 'Unknown error');
      }
    } catch (e) {
      statusEl.textContent = '❌ Network error while saving';
    }
  }

  async function reset() {
    statusEl.textContent = 'Resetting polygon...';

    try {
      const resp = await fetch(options.resetUrl, {method: 'POST'});
      const data = await resp.json();

      if (data.ok) {
        points = data[options.polygonKey] || [];
        statusEl.textContent = `✅ Reset to default ${points.length}-point polygon`;
        draw();
      } else {
        statusEl.textContent = '❌ Reset failed';
      }
    } catch (e) {
      statusEl.textContent = '❌ Network error while resetting';
    }
  }

  function undo() {
    if (points.length === 0) {
      statusEl.textContent = 'No points to undo';
      return;
    }

    points.pop();
    statusEl.textContent = `Removed last point. Current points: ${points.length}`;
    draw();
  }

  function clear() {
    points = [];
    statusEl.textContent = 'Cleared all points';
    draw();
  }

  return {
    refreshSnapshot,
    save,
    reset,
    undo,
    clear
  };
}

const garbagePolygonEditor = createPolygonEditor({
  canvasId: 'geoCanvas',
  messageId: 'geoMsg',
  displayId: 'geoRoiDisplay',
  statusId: 'geoStatus',
  getUrl: '/api/geofence',
  postUrl: '/api/geofence',
  resetUrl: '/api/geofence/reset',
  polygonKey: 'garbage_polygon',
  roiKey: 'garbage_roi',
  label: 'Garbage Polygon Zone',
  lineColor: '#39c6ff',
  fillColor: 'rgba(57,198,255,0.00)',
  pointColor: '#39c6ff',
  textColor: '#ffffff'
});

const personPolygonEditor = createPolygonEditor({
  canvasId: 'personGeoCanvas',
  messageId: 'personGeoMsg',
  displayId: 'personGeoRoiDisplay',
  statusId: 'personGeoStatus',
  getUrl: '/api/person_geofence',
  postUrl: '/api/person_geofence',
  resetUrl: '/api/person_geofence/reset',
  polygonKey: 'person_polygon',
  roiKey: 'person_roi',
  label: 'Person Polygon Zone',
  lineColor: '#22c55e',
  fillColor: 'rgba(34,197,94,0.00)',
  pointColor: '#22c55e',
  textColor: '#ffffff'
});

const vehiclePolygonEditor = createPolygonEditor({
  canvasId: 'vehicleGeoCanvas',
  messageId: 'vehicleGeoMsg',
  displayId: 'vehicleGeoRoiDisplay',
  statusId: 'vehicleGeoStatus',
  getUrl: '/api/vehicle_geofence',
  postUrl: '/api/vehicle_geofence',
  resetUrl: '/api/vehicle_geofence/reset',
  polygonKey: 'vehicle_polygon',
  roiKey: 'vehicle_roi',
  label: 'Vehicle Polygon Zone',
  lineColor: '#ffc857',
  fillColor: 'rgba(255,200,87,0.00)',
  pointColor: '#ffc857',
  textColor: '#ffffff'
});

function geoRefreshSnapshot() {
  garbagePolygonEditor.refreshSnapshot();
}

function geoSave() {
  garbagePolygonEditor.save();
}

function geoReset() {
  garbagePolygonEditor.reset();
}

function geoUndo() {
  garbagePolygonEditor.undo();
}

function geoClear() {
  garbagePolygonEditor.clear();
}

function personGeoRefreshSnapshot() {
  personPolygonEditor.refreshSnapshot();
}

function personGeoSave() {
  personPolygonEditor.save();
}

function personGeoReset() {
  personPolygonEditor.reset();
}

function personGeoUndo() {
  personPolygonEditor.undo();
}

function personGeoClear() {
  personPolygonEditor.clear();
}

function vehicleGeoRefreshSnapshot() {
  vehiclePolygonEditor.refreshSnapshot();
}

function vehicleGeoSave() {
  vehiclePolygonEditor.save();
}

function vehicleGeoReset() {
  vehiclePolygonEditor.reset();
}

function vehicleGeoUndo() {
  vehiclePolygonEditor.undo();
}

function vehicleGeoClear() {
  vehiclePolygonEditor.clear();
}

let currentVehicleDetectionMode = "{{ state.system.vehicle_detection_mode or state.last_vehicle.mode or 'static' }}";

function updateVehicleModeUi(mode, threshold) {
  currentVehicleDetectionMode = (mode || 'static').toLowerCase();

  const modeEl = document.getElementById('vehicleDetectionMode');
  const toggleEl = document.getElementById('vehicleModeToggle');

  if (modeEl) {
    modeEl.textContent = currentVehicleDetectionMode.toUpperCase();
  }

  if (toggleEl) {
    if (currentVehicleDetectionMode === 'moving') {
      toggleEl.textContent = '🚗 Switch to Static Detection';
    } else {
      toggleEl.textContent = '🏃 Switch to Moving Detection';
    }
  }

  const overlapEl = document.getElementById('vehicleGeofenceOverlap');
  if (overlapEl && threshold != null) {
    const currentText = overlapEl.textContent || '0';
    if (currentText === '0' || currentText === '0.0') {
      overlapEl.textContent = 'Threshold: ' + Math.round(Number(threshold) * 100) + '%';
    }
  }
}

async function setVehicleDetectionMode(mode) {
  try {
    const resp = await fetch('/api/vehicle_detection_mode', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({mode})
    });

    const data = await resp.json();

    if (!resp.ok || !data.ok) {
      alert('Vehicle mode change failed: ' + (data.error || 'Unknown error'));
      return;
    }

    updateVehicleModeUi(data.vehicle_detection_mode, data.vehicle_geofence_overlap_threshold);
  } catch (e) {
    alert('Network error while changing vehicle mode');
  }
}

function toggleVehicleDetectionMode() {
  const nextMode = currentVehicleDetectionMode === 'moving' ? 'static' : 'moving';
  setVehicleDetectionMode(nextMode);
}

// ============ DASHBOARD CONTROLS ============

const CONTROL_STATUS_IDS = {
  person_detection_enabled: 'ctrlPersonDetectionStatus',
  vehicle_detection_enabled: 'ctrlVehicleDetectionStatus',
  garbage_detection_enabled: 'ctrlGarbageDetectionStatus',
  person_video_recording_enabled: 'ctrlPersonVideoStatus',
  vehicle_video_recording_enabled: 'ctrlVehicleVideoStatus'
};

function controlSetText(id, text) {
  const el = document.getElementById(id);
  if (el) el.textContent = text;
}

function controlSetStatusPill(id, enabled, onText = 'ENABLED', offText = 'DISABLED') {
  const el = document.getElementById(id);
  if (!el) return;

  el.textContent = enabled ? onText : offText;
  el.classList.remove('on', 'off');
  el.classList.add(enabled ? 'on' : 'off');
}

function prettyGarbageMode(mode) {
  const normalized = String(mode || 'ai').trim().toLowerCase();
  return normalized === 'normal' ? 'Normal Mode' : 'AI Mode';
}

function controlSetGarbageModePill(mode) {
  const el = document.getElementById('ctrlGarbageModeStatus');
  if (!el) return;

  const normalized = String(mode || 'ai').trim().toLowerCase();
  el.textContent = prettyGarbageMode(normalized).toUpperCase();
  el.classList.remove('on', 'off');

  if (normalized === 'ai') {
    el.classList.add('on');
  }
}

function controlStatus(message, good = true) {
  const el = document.getElementById('controlsStatusLine');
  if (!el) return;

  el.textContent = message;
  el.style.color = good ? 'var(--muted)' : '#ff9b9b';
}

function controlVolumePreview(value) {
  controlSetText('ctrlVolumePreview', String(value) + '%');
}

function updateControlsFromState(s) {
  if (!s) return;

  const flags = s.feature_flags || {};
  Object.keys(CONTROL_STATUS_IDS).forEach(flagName => {
    if (flags[flagName] !== undefined) {
      controlSetStatusPill(CONTROL_STATUS_IDS[flagName], !!flags[flagName]);
    }
  });

  const garbageMode =
    s.garbage_detection_mode ||
    (s.system && s.system.garbage_detection_mode) ||
    'ai';

  controlSetGarbageModePill(garbageMode);

  if (s.warning_audio) {
    controlSetStatusPill(
      'ctrlWarningAudioStatus',
      !!s.warning_audio.enabled,
      s.warning_audio.schedule_enabled ? 'SCHEDULED' : 'ENABLED',
      'DISABLED'
    );
  }

  const powerSavingEnabled =
    (s.power_saving && s.power_saving.enabled !== undefined)
      ? !!s.power_saving.enabled
      : !!(s.power_saving_enabled || (s.system && s.system.power_saving_active));

  controlSetStatusPill('ctrlPowerSavingStatus', powerSavingEnabled, 'ENABLED', 'DISABLED');

  if (s.audio_queue_size !== undefined && s.audio_queue_size !== null) {
    controlSetText('ctrlAudioQueueStatus', 'Queue: ' + s.audio_queue_size);
  }
}

function updateControlsPanel(data) {
  updateControlsFromState(data);

  if (data && data.volume) {
    if (data.volume.ok) {
      const volume = Number(data.volume.volume_percent || 0);
      const slider = document.getElementById('ctrlVolumeRange');

      if (slider) slider.value = volume;

      controlVolumePreview(volume);
      controlSetStatusPill('ctrlVolumeStatus', true, 'CURRENT: ' + volume + '%', 'VOLUME ERROR');
    } else {
      controlSetText('ctrlVolumeStatus', 'Volume Error');
      const el = document.getElementById('ctrlVolumeStatus');
      if (el) {
        el.classList.remove('on');
        el.classList.add('off');
      }
    }
  }
}

async function controlFetchJson(url, options = {}) {
  const resp = await fetch(url, options);
  const data = await resp.json().catch(() => ({}));

  if (!resp.ok || data.ok === false) {
    throw new Error(data.error || data.message || ('HTTP ' + resp.status));
  }

  return data;
}

async function loadControlsState() {
  try {
    const data = await controlFetchJson('/api/controls/state?t=' + Date.now());
    updateControlsPanel(data);
    controlStatus('Controls refreshed.');
  } catch (e) {
    controlStatus('Controls refresh failed: ' + e.message, false);
  }
}

async function controlSetFeature(flag, enabled) {
  try {
    controlStatus('Updating control...');
    const data = await controlFetchJson('/api/controls/feature', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({flag, enabled})
    });

    updateControlsPanel(data);
    controlStatus(flag + ' set to ' + (enabled ? 'enabled' : 'disabled') + '.');
  } catch (e) {
    controlStatus('Feature update failed: ' + e.message, false);
  }
}

async function controlSetGarbageMode(mode) {
  try {
    const normalized = String(mode || 'ai').trim().toLowerCase();
    controlStatus('Changing garbage mode to ' + prettyGarbageMode(normalized) + '...');

    const data = await controlFetchJson('/api/controls/garbage/mode', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({mode: normalized})
    });

    updateControlsPanel(data);
    controlStatus('Garbage mode changed to ' + prettyGarbageMode(data.garbage_detection_mode) + '.');
    refreshState();
  } catch (e) {
    controlStatus('Garbage mode update failed: ' + e.message, false);
  }
}

async function controlSetWarningAudio(enabled) {
  try {
    controlStatus('Updating warning audio...');
    const data = await controlFetchJson('/api/controls/warning_audio', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({enabled})
    });

    updateControlsPanel(data);
    controlStatus('Warning audio ' + (enabled ? 'enabled' : 'disabled') + '.');
  } catch (e) {
    controlStatus('Warning audio update failed: ' + e.message, false);
  }
}

async function controlManualGarbage() {
  try {
    controlStatus('Running manual garbage detection. Please wait...');
    const data = await controlFetchJson('/api/controls/garbage/manual_detect', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({})
    });

    updateControlsPanel(data);
    controlStatus('Manual garbage detection completed.');
    refreshState();
  } catch (e) {
    controlStatus('Manual garbage detection failed: ' + e.message, false);
  }
}

async function controlGarbageSample() {
  try {
    controlStatus('Capturing garbage sample/reference...');
    const data = await controlFetchJson('/api/controls/garbage/sample_capture', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({})
    });

    updateControlsPanel(data);
    controlStatus('Garbage sample captured successfully.');
    refreshState();
  } catch (e) {
    controlStatus('Garbage sample capture failed: ' + e.message, false);
  }
}

async function controlClearAudioQueue() {
  try {
    controlStatus('Clearing audio queue...');
    const data = await controlFetchJson('/api/controls/audio/clear_queue', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({})
    });

    updateControlsPanel(data);
    controlStatus('Audio queue cleared. Cleared count: ' + (data.cleared_count ?? 0));
    refreshState();
  } catch (e) {
    controlStatus('Clear audio queue failed: ' + e.message, false);
  }
}

async function controlSetPowerSaving(enabled) {
  try {
    controlStatus('Updating power saving mode...');
    const data = await controlFetchJson('/api/controls/power_saving', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({enabled})
    });

    updateControlsPanel(data);
    controlStatus('Power saving ' + (enabled ? 'enabled' : 'disabled') + '.');
    refreshState();
  } catch (e) {
    controlStatus('Power saving update failed: ' + e.message, false);
  }
}

async function controlRefreshVolume() {
  try {
    controlStatus('Reading current Pi volume...');
    const data = await controlFetchJson('/api/controls/volume?t=' + Date.now());
    updateControlsPanel({volume: data});
    controlStatus('Current Pi volume: ' + data.volume_percent + '%.');
  } catch (e) {
    controlStatus('Volume read failed: ' + e.message, false);
  }
}

async function controlSetVolume(percent) {
  try {
    percent = Math.max(0, Math.min(100, Number(percent || 0)));
    controlStatus('Setting Pi volume to ' + percent + '%...');

    const data = await controlFetchJson('/api/controls/volume', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({volume_percent: percent})
    });

    updateControlsPanel({volume: data});
    controlStatus('Pi volume set to ' + data.volume_percent + '%.');
    refreshState();
  } catch (e) {
    controlStatus('Volume set failed: ' + e.message, false);
  }
}

function controlSetVolumeFromSlider() {
  const slider = document.getElementById('ctrlVolumeRange');
  controlSetVolume(slider ? slider.value : 70);
}

// ============ END DASHBOARD CONTROLS ============
// ============ END POLYGON GEOFENCE EDITOR ============


function showTab(name, btn){
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));

  const panel = document.getElementById('tab-' + name);
  if (panel) {
    panel.classList.add('active');
  }

  if (btn) {
    btn.classList.add('active');
  } else {
    document.querySelectorAll('.tab-btn').forEach(button => {
      const clickAttr = button.getAttribute('onclick') || '';
      if (clickAttr.includes("'" + name + "'") || clickAttr.includes('"' + name + '"')) {
        button.classList.add('active');
      }
    });
  }

  if (name === 'geofence') geoRefreshSnapshot();
  if (name === 'person_geofence') personGeoRefreshSnapshot();
  if (name === 'vehicle_geofence') vehicleGeoRefreshSnapshot();
  if (name === 'controls') loadControlsState();
}

document.addEventListener('DOMContentLoaded', () => {
  const initialTab = "{{ initial_tab | default('live') }}";

  if (initialTab === "geofence") {
    showTab("geofence", null);
  } else if (initialTab === "person_geofence") {
    showTab("person_geofence", null);
  } else if (initialTab === "vehicle_geofence") {
    showTab("vehicle_geofence", null);
  } else if (initialTab === "controls") {
    showTab("controls", null);
  } else {
    showTab("live", null);
  }

  updateVehicleModeUi(currentVehicleDetectionMode, null);
});

function severityBadge(sev){
  if(sev === 'HIGH') return '<span class="badge high">HIGH</span>';
  if(sev === 'MEDIUM') return '<span class="badge medium">MEDIUM</span>';
  if(sev === 'LOW') return '<span class="badge low">LOW</span>';
  return '<span class="badge none">NONE</span>';
}
async function refreshState(){
  try{
    const r = await fetch('/api/state?t=' + Date.now());
    const s = await r.json();

    document.getElementById('highCount').textContent = s.counts.HIGH;
    document.getElementById('mediumCount').textContent = s.counts.MEDIUM;
    document.getElementById('lowCount').textContent = s.counts.LOW;

    document.getElementById('cameraStatus').textContent = s.system.camera_connected ? 'Connected' : 'Disconnected';
    document.getElementById('lastError').textContent = s.system.last_error || 'None';

    const currentGarbageMode =
      s.garbage_detection_mode ||
      (s.system && s.system.garbage_detection_mode) ||
      'ai';

    document.getElementById('garbageMode').textContent = prettyGarbageMode(currentGarbageMode);
    document.getElementById('garbageSeverity').innerHTML = severityBadge((s.last_garbage && s.last_garbage.severity) ? s.last_garbage.severity : 'NONE');

    document.getElementById('permanentLiveUrl').textContent = (s.live_link && s.live_link.url) ? s.live_link.url : 'Inactive';
    document.getElementById('permanentLiveStatus').textContent = (s.live_link && s.live_link.expires_at) ? s.live_link.expires_at : 'Inactive';
    document.getElementById('cameraConfigUrl').textContent = s.camera_config_url || 'Unavailable';

    document.getElementById('garbageTime').textContent = s.last_garbage.time || 'Waiting...';
    document.getElementById('garbageTriggeredBy').textContent = s.last_garbage.triggered_by || 'N/A';
    document.getElementById('garbageSeverityText').textContent = s.last_garbage.severity || 'NONE';
    document.getElementById('garbageCount').textContent = s.last_garbage.blob_count ?? 0;
    document.getElementById('garbageDiffRatio').textContent = s.last_garbage.diff_ratio ?? 0;
    document.getElementById('garbageEdgeRatio').textContent = s.last_garbage.edge_ratio ?? 0;
    document.getElementById('garbageBlobAreaRatio').textContent = s.last_garbage.blob_area_ratio ?? 0;

    document.getElementById('personTime').textContent = s.last_person.time || 'Waiting...';
    document.getElementById('personCount').textContent = s.last_person.count ?? 0;
    document.getElementById('personStatus').textContent = s.last_person.detected ? 'Detected now' : 'Last detection saved';
    document.getElementById('personBestConfidence').textContent = s.last_person.best_confidence ?? 0;
	
	document.getElementById('vehicleTime').textContent = s.last_vehicle.time || 'Waiting...';
    document.getElementById('vehicleCount').textContent = s.last_vehicle.count ?? 0;
    document.getElementById('vehicleStatus').textContent = s.last_vehicle.detected ? 'Detected now' : (s.last_vehicle.image ? 'Last detection saved' : 'No recent detection');
    document.getElementById('vehicleBestConfidence').textContent = s.last_vehicle.best_confidence ?? 0;

    const vehicleMode = s.vehicle_detection_mode || s.system.vehicle_detection_mode || s.last_vehicle.mode || 'static';
    updateVehicleModeUi(vehicleMode, s.vehicle_geofence_overlap_threshold || s.system.vehicle_geofence_overlap_threshold);

    document.getElementById('vehicleLabels').textContent = (s.last_vehicle.labels && s.last_vehicle.labels.length) ? s.last_vehicle.labels.join(', ') : 'None';

    const vehicleOverlap = Number(s.last_vehicle.geofence_overlap || 0);
    document.getElementById('vehicleGeofenceOverlap').textContent =
      vehicleOverlap > 0
        ? (Math.round(vehicleOverlap * 100) + '% inside polygon')
        : ('Threshold: ' + Math.round(Number(s.vehicle_geofence_overlap_threshold || s.system.vehicle_geofence_overlap_threshold || 0.35) * 100) + '%');

    document.getElementById('vehicleImageStatus').textContent = s.last_vehicle.image_status || 'None';
    document.getElementById('vehicleImagePath').textContent = s.last_vehicle.image_uploaded_path || 'None';

    if (s.last_vehicle.image) {
      document.getElementById('vehicleImg').src = '/media/' + s.last_vehicle.image + '?t=' + Date.now();
    }

    document.getElementById('dashLocal').textContent = s.links.dashboard_local || 'Unavailable';
    document.getElementById('streamLocal').textContent = s.links.stream_local || 'Unavailable';
    document.getElementById('permanentLiveUrl2').textContent = (s.live_link && s.live_link.url) ? s.live_link.url : 'Inactive';
    document.getElementById('cameraConfigUrl2').textContent = s.camera_config_url || 'Unavailable';
    document.getElementById('sftpBase').textContent = s.sftp_base_dir || '';
    document.getElementById('sftpPerson').textContent = s.sftp_person_dir || '';
    document.getElementById('sftpPersonVideo').textContent = s.sftp_person_video_dir || '';
    document.getElementById('sftpGarbage').textContent = s.sftp_garbage_dir || '';
    document.getElementById('sftpAudio').textContent = s.sftp_audio_dir || '';
    document.getElementById('commandListener').textContent = s.system.command_listener ? 'Connected' : 'Disconnected';

    document.getElementById('piTemp').textContent = s.system.pi_temperature_c || 'Unavailable';
    document.getElementById('ramUsage').textContent = s.system.ram_percent != null ? (s.system.ram_percent + ' %') : 'Unavailable';
    document.getElementById('ramDetail').textContent =
      ((s.system.ram_used_mb != null ? s.system.ram_used_mb + ' MB' : 'Unavailable') + ' / ' +
      (s.system.ram_total_mb != null ? s.system.ram_total_mb + ' MB' : 'Unavailable'));
    document.getElementById('networkStatus').textContent = s.system.network_status || 'Unknown';
    document.getElementById('networkIface').textContent = s.system.network_iface || 'Unknown';
    document.getElementById('networkLinkSpeed').textContent = s.system.network_link_speed_mbps ?? 'Unknown';
    document.getElementById('lastAudio').textContent = (s.last_audio && s.last_audio.file) ? s.last_audio.file : 'None';
    document.getElementById('lastAudioStatus').textContent = (s.last_audio && s.last_audio.status) ? s.last_audio.status : 'None';

    updateControlsFromState(s);

    if (s.last_garbage.image) document.getElementById('garbageImg').src = '/media/' + s.last_garbage.image + '?t=' + Date.now();
    if (s.last_person.image) document.getElementById('personImg').src = '/media/' + s.last_person.image + '?t=' + Date.now();
  } catch(e) {
    console.log(e);
  }
}
setInterval(refreshState, 5000);
</script>
</body>
</html>
HTML

echo "[15/17] Write app.py..."
cat > "$APP_DIR/app.py" <<'PY'
import atexit
import datetime as dt
import ipaddress
import json
import queue
import re
import shlex
import signal
import socket
import subprocess
import threading
import time
from collections import deque
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import cv2

try:
    cv2.setNumThreads(2)
    cv2.ocl.setUseOpenCL(False)
except Exception:
    pass

import numpy as np
import paho.mqtt.client as mqtt
import paramiko
import psutil
import requests
from flask import Flask, Response, jsonify, render_template, send_from_directory, request
from waitress import serve

APP_DIR = Path("/opt/gcam")
DATA_DIR = APP_DIR / "data"
MODEL_DIR = APP_DIR / "models"
ASSET_DIR = APP_DIR / "assets"
FILES_DIR = APP_DIR / "files"
GARBAGE_DIR = FILES_DIR / "garbage"
AUDIO_DIR = FILES_DIR / "audio"
DEFAULT_AUDIO_DIR = FILES_DIR / "default_audio"
VIDEO_DIR = FILES_DIR / "video"
TMP_DIR = APP_DIR / "tmp"
TEMPLATE_DIR = APP_DIR / "templates"

CONFIG_FILE = DATA_DIR / "config.json"
STATE_FILE = DATA_DIR / "state.json"
GARBAGE_REF_FILE = DATA_DIR / "garbage_reference.jpg"

HOST = "0.0.0.0"
PORT = 8080

# Pi 1GB optimized stream size.
# 640 gives much lower CPU/RAM pressure than 720.
FRAME_WIDTH = 640
MJPEG_QUALITY = 60

# Reduce video-buffer CPU/RAM pressure.
ROLLING_BUFFER_TARGET_FPS = 5.0
PERSON_FULL_SCAN_INTERVAL_SEC = 1.25

ROUTER_REBOOT_GPIO = 17
ROUTER_REBOOT_GPIOCHIP = "gpiochip0"
ROUTER_REBOOT_ACTIVE_VALUE = 1
ROUTER_REBOOT_INACTIVE_VALUE = 0
ROUTER_REBOOT_PULSE_SEC = 1
ROUTER_REBOOT_STAGE_GAP_SEC = 5
ROUTER_REBOOT_COMMAND = "trigger router reboot"

# ── Person detection tuning ──────────────────────────────────────────
# Pi 1GB balanced working settings.
# IMPORTANT: Keep PERSON_DETECT_EVERY_N_FRAMES = 1.
# Changing it to 2 can break confirmation/detection in this script flow.
PERSON_CONF = 0.60
PERSON_COOLDOWN_SEC = 4
AUDIO_COOLDOWN_SEC = 3
PERSON_CONFIRMATION_FRAMES = 1

EVENT_PRE_RECORD_SEC = 6
EVENT_POST_RECORD_SEC = 8
EVENT_CLIP_DURATION_SEC = EVENT_PRE_RECORD_SEC + EVENT_POST_RECORD_SEC
EVENT_ROLLING_BUFFER_SEC = 18
EVENT_BUFFER_JPEG_QUALITY = 65
EVENT_MIN_FPS = 4.0
EVENT_MAX_FPS = 8.0
EVENT_DEFAULT_FPS = 5.0
EVENT_TEMP_VIDEO_EXTENSION = "avi"

PERSON_VIDEO_DURATION_SEC = EVENT_CLIP_DURATION_SEC
PERSON_VIDEO_SUFFIX = "Straps"
PERSON_VIDEO_EXTENSION = "mp4"
PERSON_DETECT_EVERY_N_FRAMES = 1
PERSON_UPSCALE_FACTOR = 1.12
PERSON_NMS_THRESHOLD = 0.24
PERSON_LOST_TIMEOUT_SEC = 1.2
PERSON_MIN_AREA = 1800
PERSON_TILE_STEP_RATIO = 0.34
PERSON_TILE_MIN_CONF = 0.58
PERSON_HOG_ENABLE = False
PERSON_ALLOWED_ROI = (0.01, 0.03, 0.99, 0.995)
DEFAULT_PERSON_ROI = (0.01, 0.03, 0.99, 0.995)

PERSON_MIN_BOX_HEIGHT_RATIO = 0.11
PERSON_MIN_BOX_WIDTH_RATIO = 0.025
PERSON_MIN_ASPECT_RATIO = 1.20
PERSON_MAX_ASPECT_RATIO = 4.50
PERSON_MAX_BOX_WIDTH_RATIO = 0.38

# Extra anti-pole / anti-stone / anti-tree filters.
PERSON_POLE_MAX_WIDTH_RATIO = 0.075
PERSON_POLE_ASPECT_RATIO = 2.85
PERSON_MIN_EDGE_DENSITY = 0.018
PERSON_MAX_EDGE_DENSITY = 0.24
PERSON_MIN_TEXTURE_VARIANCE = 38.0
PERSON_EXCLUSION_ZONES: List[Tuple[float, float, float, float]] = []

# Vehicle detection using MobileNetSSD vehicle classes.
# Static mode detects parked/stopped vehicles.
# Moving mode is still available from dashboard/API/MQTT.
# Vehicle geofence uses bbox-vs-polygon overlap, not only tyre/bottom point.
VEHICLE_CLASSES = {"car", "bus", "motorbike"}
VEHICLE_CLASS_LABELS = {
    "car": "Car",
    "bus": "Bus",
    "motorbike": "Motorbike",
}

# Higher confidence + stable-time confirmation avoids wrong vehicle judgements.
VEHICLE_CONF = 0.68
VEHICLE_NMS_THRESHOLD = 0.30

# Keep CPU controlled. Do not use every frame on 1GB Pi.
VEHICLE_DETECT_EVERY_N_FRAMES = 2

VEHICLE_MIN_AREA = 2800
VEHICLE_MIN_BOX_HEIGHT_RATIO = 0.08
VEHICLE_MIN_BOX_WIDTH_RATIO = 0.08
VEHICLE_MAX_BOX_WIDTH_RATIO = 0.82
VEHICLE_MAX_BOX_HEIGHT_RATIO = 0.82

# Prevent duplicate image/video events for same parked vehicle.
VEHICLE_COOLDOWN_SEC = 20

# Kept for compatibility, but event confirmation is now time-based.
VEHICLE_CONFIRMATION_FRAMES = 1

# Vehicle must disappear/outside marking for this time before next vehicle can trigger.
VEHICLE_LOST_TIMEOUT_SEC = 2.0

# Main rule: vehicle must stay inside geofence/marking for 5 seconds.
VEHICLE_STABLE_INSIDE_SEC = 5.0

# Allows one or two missed detections without resetting the 5-second timer.
# Useful because Pi can skip frames under load.
VEHICLE_STABLE_GRACE_SEC = 0.80

# Minimum good detections during the 5-second stable window.
VEHICLE_MIN_STABLE_HITS = 3

# Event capture needs this confidence or higher.
VEHICLE_EVENT_MIN_CONF = 0.70

VEHICLE_VIDEO_DURATION_SEC = EVENT_CLIP_DURATION_SEC
VEHICLE_VIDEO_EXTENSION = "mp4"
VEHICLE_VIDEO_SUFFIX = "Vehicle"

DEFAULT_GARBAGE_DETECTION_MODE = "ai"
GARBAGE_DETECTION_MODES = {"ai", "normal"}

DEFAULT_VEHICLE_DETECTION_MODE = "static"
VEHICLE_DETECTION_MODES = {"static", "moving"}

DEFAULT_VEHICLE_ROI = (0.00, 0.08, 1.00, 1.00)
DEFAULT_VEHICLE_POLYGON = (
    (DEFAULT_VEHICLE_ROI[0], DEFAULT_VEHICLE_ROI[1]),
    (DEFAULT_VEHICLE_ROI[2], DEFAULT_VEHICLE_ROI[1]),
    (DEFAULT_VEHICLE_ROI[2], DEFAULT_VEHICLE_ROI[3]),
    (DEFAULT_VEHICLE_ROI[0], DEFAULT_VEHICLE_ROI[3]),
)

# At least 60% of detected vehicle box must be inside marking.
# This avoids wrong judgement when only tyre/front edge enters polygon.
VEHICLE_GEOFENCE_MIN_OVERLAP_RATIO = 0.60

VEHICLE_EXCLUSION_ZONES: List[Tuple[float, float, float, float]] = []
VEHICLE_MOTION_THRESHOLD = 24
VEHICLE_MOTION_MIN_PIXELS = 90
VEHICLE_MOTION_MIN_RATIO = 0.020
VEHICLE_MOTION_DILATE_ITER = 2

# ── Garbage object detection constants ───────────────────────────────
GARBAGE_EXCLUSION_ZONES: List[Tuple[float, float, float, float]] = []

GARBAGE_CAPTURE_SAMPLES = 3
GARBAGE_SAMPLE_GAP_SEC = 1.2
DEFAULT_GARBAGE_INTERVAL_SEC = 3600
GARBAGE_INTERVAL_SEC = DEFAULT_GARBAGE_INTERVAL_SEC
GARBAGE_START_DELAY_SEC = 12
REFERENCE_UPDATE_STABLE_COUNT = 5

COUNT_LOW_MIN = 1
COUNT_MEDIUM_MIN = 3
COUNT_HIGH_MIN = 6

# Pi 5 1GB-safe garbage settings.
# These skip tiny paper/noise and count only meaningful garbage patches.
GARBAGE_OBJ_INPUT_SIZE = 320
GARBAGE_OBJ_CONF = 0.28
GARBAGE_OBJ_NMS = 0.45

GARBAGE_MIN_BOX_AREA_PX = 900
GARBAGE_MIN_BOX_AREA_RATIO = 0.0015
GARBAGE_MIN_BOX_WIDTH_RATIO = 0.030
GARBAGE_MIN_BOX_HEIGHT_RATIO = 0.030
GARBAGE_MAX_BOX_AREA_RATIO = 0.50

GARBAGE_CHANGE_MIN_AREA_PX = 1200
GARBAGE_CHANGE_MIN_AREA_RATIO = 0.0022
GARBAGE_CHANGE_MIN_WIDTH_RATIO = 0.035
GARBAGE_CHANGE_MIN_HEIGHT_RATIO = 0.030
GARBAGE_CHANGE_MERGE_GAP_RATIO = 0.030

GARBAGE_MIN_TEXTURE_VARIANCE = 32.0
GARBAGE_MIN_SATURATION_MEAN = 14.0

GARBAGE_TARGET_LABELS = {
    "bottle",
    "cup",
    "bowl",
    "book",
    "handbag",
    "backpack",
    "teddy bear"
}

GARBAGE_LABEL_ALIAS = {
    "bottle": "Bottle",
    "cup": "Cup",
    "bowl": "Container",
    "book": "Notebook",
    "handbag": "Bag",
    "backpack": "Bag",
    "teddy bear": "Cloth"
}

RETENTION_SECONDS = 86400
CLEANUP_INTERVAL_SEC = 3600

# Dynamic polygon geofence support.
# Old rectangle ROI is still supported for backward compatibility.
DEFAULT_GARBAGE_ROI = (0.08, 0.28, 0.92, 0.90)
DEFAULT_GARBAGE_POLYGON = (
    (0.08, 0.28),
    (0.92, 0.28),
    (0.92, 0.90),
    (0.08, 0.90),
)

DEFAULT_PERSON_POLYGON = (
    (DEFAULT_PERSON_ROI[0], DEFAULT_PERSON_ROI[1]),
    (DEFAULT_PERSON_ROI[2], DEFAULT_PERSON_ROI[1]),
    (DEFAULT_PERSON_ROI[2], DEFAULT_PERSON_ROI[3]),
    (DEFAULT_PERSON_ROI[0], DEFAULT_PERSON_ROI[3]),
)

POLYGON_MIN_POINTS = 3
POLYGON_MAX_POINTS = 20
POLYGON_MIN_AREA_RATIO = 0.0001


def ratio_box_to_polygon(box: Tuple[float, float, float, float]) -> List[Tuple[float, float]]:
    x1, y1, x2, y2 = box
    return [
        (float(x1), float(y1)),
        (float(x2), float(y1)),
        (float(x2), float(y2)),
        (float(x1), float(y2)),
    ]


def ratio_polygon_to_box(points: List[Tuple[float, float]]) -> Tuple[float, float, float, float]:
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return (
        max(0.0, min(xs)),
        max(0.0, min(ys)),
        min(1.0, max(xs)),
        min(1.0, max(ys)),
    )


def ratio_polygon_area(points: List[Tuple[float, float]]) -> float:
    if len(points) < 3:
        return 0.0

    area = 0.0
    total = len(points)

    for idx in range(total):
        x1, y1 = points[idx]
        x2, y2 = points[(idx + 1) % total]
        area += (x1 * y2) - (x2 * y1)

    return abs(area) / 2.0


def sanitize_ratio_polygon(value: Any) -> Optional[List[Tuple[float, float]]]:
    try:
        if not isinstance(value, list):
            return None

        if len(value) < POLYGON_MIN_POINTS:
            return None

        if len(value) > POLYGON_MAX_POINTS:
            return None

        points: List[Tuple[float, float]] = []

        for point in value:
            if not isinstance(point, (list, tuple)) or len(point) != 2:
                return None

            x = float(point[0])
            y = float(point[1])

            if not (0.0 <= x <= 1.0 and 0.0 <= y <= 1.0):
                return None

            points.append((x, y))

        if ratio_polygon_area(points) < POLYGON_MIN_AREA_RATIO:
            return None

        return points

    except Exception:
        return None


def round_ratio_polygon(points: List[Tuple[float, float]]) -> List[List[float]]:
    return [[round(float(x), 4), round(float(y), 4)] for x, y in points]


def _valid_ratio_box(value: Any) -> Optional[Tuple[float, float, float, float]]:
    try:
        if not value or len(value) != 4:
            return None

        x1, y1, x2, y2 = [float(v) for v in value]

        if 0.0 <= x1 < x2 <= 1.0 and 0.0 <= y1 < y2 <= 1.0:
            return (x1, y1, x2, y2)

    except Exception:
        pass

    return None


def load_ratio_polygon_from_config(
    polygon_key: str,
    roi_key: str,
    default_polygon: Tuple[Tuple[float, float], ...]
) -> List[Tuple[float, float]]:
    try:
        cfg = load_config()

        polygon = sanitize_ratio_polygon(cfg.get(polygon_key))
        if polygon is not None:
            return polygon

        old_box = _valid_ratio_box(cfg.get(roi_key))
        if old_box is not None:
            return ratio_box_to_polygon(old_box)

    except Exception:
        pass

    return [(float(x), float(y)) for x, y in default_polygon]


def load_garbage_polygon() -> List[Tuple[float, float]]:
    return load_ratio_polygon_from_config(
        "garbage_polygon",
        "garbage_roi",
        DEFAULT_GARBAGE_POLYGON
    )


def load_person_polygon() -> List[Tuple[float, float]]:
    return load_ratio_polygon_from_config(
        "person_polygon",
        "person_roi",
        DEFAULT_PERSON_POLYGON
    )


def load_vehicle_polygon() -> List[Tuple[float, float]]:
    return load_ratio_polygon_from_config(
        "vehicle_polygon",
        "vehicle_roi",
        DEFAULT_VEHICLE_POLYGON
    )


def ratio_polygon_to_pixels(
    frame: np.ndarray,
    points: List[Tuple[float, float]]
) -> List[Tuple[int, int]]:
    h, w = frame.shape[:2]

    pixel_points: List[Tuple[int, int]] = []

    for x_ratio, y_ratio in points:
        x = int(round(float(x_ratio) * float(max(w - 1, 1))))
        y = int(round(float(y_ratio) * float(max(h - 1, 1))))

        x = max(0, min(w - 1, x))
        y = max(0, min(h - 1, y))

        pixel_points.append((x, y))

    return pixel_points


def polygon_pixels_to_box(
    points: List[Tuple[int, int]],
    frame: np.ndarray
) -> Tuple[int, int, int, int]:
    h, w = frame.shape[:2]

    xs = [p[0] for p in points]
    ys = [p[1] for p in points]

    x1 = max(0, min(xs))
    y1 = max(0, min(ys))
    x2 = min(w, max(xs) + 1)
    y2 = min(h, max(ys) + 1)

    if x2 <= x1:
        x2 = min(w, x1 + 1)

    if y2 <= y1:
        y2 = min(h, y1 + 1)

    return (x1, y1, x2, y2)


def make_polygon_masked_crop(
    frame: np.ndarray,
    points_px: List[Tuple[int, int]]
) -> Tuple[np.ndarray, Tuple[int, int, int, int]]:
    roi_box = polygon_pixels_to_box(points_px, frame)
    x1, y1, x2, y2 = roi_box

    crop = frame[y1:y2, x1:x2].copy()

    if len(points_px) < 3:
        return crop, roi_box

    mask = np.zeros(crop.shape[:2], dtype=np.uint8)
    relative_points = np.array(
        [[x - x1, y - y1] for x, y in points_px],
        dtype=np.int32
    )

    cv2.fillPoly(mask, [relative_points], 255)

    masked_crop = cv2.bitwise_and(crop, crop, mask=mask)
    return masked_crop, roi_box


def get_garbage_polygon_pixels(frame: np.ndarray) -> List[Tuple[int, int]]:
    return ratio_polygon_to_pixels(frame, load_garbage_polygon())


def get_person_polygon_pixels(frame: np.ndarray) -> List[Tuple[int, int]]:
    return ratio_polygon_to_pixels(frame, load_person_polygon())


def get_garbage_roi(frame: np.ndarray) -> Tuple[np.ndarray, Tuple[int, int, int, int]]:
    points_px = get_garbage_polygon_pixels(frame)
    return make_polygon_masked_crop(frame, points_px)


def load_garbage_roi() -> Tuple[float, float, float, float]:
    return ratio_polygon_to_box(load_garbage_polygon())


def load_person_roi() -> Tuple[float, float, float, float]:
    return ratio_polygon_to_box(load_person_polygon())


def get_person_roi_pixels(frame: np.ndarray) -> Tuple[int, int, int, int]:
    points_px = get_person_polygon_pixels(frame)
    return polygon_pixels_to_box(points_px, frame)


def point_inside_person_polygon(frame: np.ndarray, x: int, y: int) -> bool:
    points_px = get_person_polygon_pixels(frame)

    if len(points_px) < 3:
        return False

    polygon_np = np.array(points_px, dtype=np.int32)
    return cv2.pointPolygonTest(polygon_np, (float(x), float(y)), False) >= 0


def draw_polygon_outline(
    image: np.ndarray,
    points_px: List[Tuple[int, int]],
    label: str,
    color: Tuple[int, int, int],
    alpha: float = 0.0
) -> None:
    if len(points_px) < 3:
        return

    polygon_np = np.array(points_px, dtype=np.int32)

    # Transparent inside ROI:
    # Do not fill polygon. Only draw border and label.
    cv2.polylines(image, [polygon_np], True, color, 2, cv2.LINE_AA)

    x_label = max(8, min(p[0] for p in points_px) + 6)
    y_label = max(22, min(p[1] for p in points_px) - 8)

    cv2.putText(
        image,
        label,
        (x_label, y_label),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.6,
        color,
        2,
        cv2.LINE_AA
    )


def load_ratio_boxes_from_config(
    key: str,
    fallback: Optional[List[Tuple[float, float, float, float]]] = None
) -> List[Tuple[float, float, float, float]]:
    boxes: List[Tuple[float, float, float, float]] = []

    try:
        cfg = load_config()
        raw_boxes = cfg.get(key, [])
        if isinstance(raw_boxes, list):
            for item in raw_boxes:
                box = _valid_ratio_box(item)
                if box is not None:
                    boxes.append(box)
    except Exception:
        pass

    if not boxes and fallback:
        boxes.extend(fallback)

    return boxes


def get_vehicle_polygon_pixels(frame: np.ndarray) -> List[Tuple[int, int]]:
    return ratio_polygon_to_pixels(frame, load_vehicle_polygon())


def load_vehicle_roi() -> Tuple[float, float, float, float]:
    return ratio_polygon_to_box(load_vehicle_polygon())


def get_vehicle_roi_pixels(frame: np.ndarray) -> Tuple[int, int, int, int]:
    points_px = get_vehicle_polygon_pixels(frame)
    return polygon_pixels_to_box(points_px, frame)


def vehicle_box_geofence_overlap_ratio(
    frame: np.ndarray,
    box: Tuple[int, int, int, int]
) -> float:
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box

    x1 = max(0, min(w - 1, int(x1)))
    y1 = max(0, min(h - 1, int(y1)))
    x2 = max(0, min(w, int(x2)))
    y2 = max(0, min(h, int(y2)))

    if x2 <= x1 or y2 <= y1:
        return 0.0

    polygon_points = get_vehicle_polygon_pixels(frame)
    if len(polygon_points) < 3:
        return 0.0

    crop_h = y2 - y1
    crop_w = x2 - x1
    mask = np.zeros((crop_h, crop_w), dtype=np.uint8)

    relative_points = np.array(
        [[px - x1, py - y1] for px, py in polygon_points],
        dtype=np.int32
    )

    cv2.fillPoly(mask, [relative_points], 255)

    inside_pixels = int(np.count_nonzero(mask))
    total_pixels = int(max(crop_h * crop_w, 1))
    return float(inside_pixels) / float(total_pixels)


def vehicle_box_inside_geofence(
    frame: np.ndarray,
    box: Tuple[int, int, int, int]
) -> Tuple[bool, float]:
    overlap_ratio = vehicle_box_geofence_overlap_ratio(frame, box)
    threshold = load_vehicle_geofence_overlap_threshold()
    return overlap_ratio >= threshold, overlap_ratio


def point_inside_box(px: int, py: int, box: Tuple[int, int, int, int]) -> bool:
    x1, y1, x2, y2 = box
    return x1 <= px <= x2 and y1 <= py <= y2


def ratio_box_to_pixels(
    ratio_box: Tuple[float, float, float, float],
    frame: np.ndarray
) -> Tuple[int, int, int, int]:
    h, w = frame.shape[:2]
    rx1, ry1, rx2, ry2 = ratio_box
    return (
        int(w * rx1),
        int(h * ry1),
        int(w * rx2),
        int(h * ry2),
    )


def point_inside_any_ratio_box(
    px: int,
    py: int,
    frame: np.ndarray,
    ratio_boxes: List[Tuple[float, float, float, float]]
) -> bool:
    for ratio_box in ratio_boxes:
        if point_inside_box(px, py, ratio_box_to_pixels(ratio_box, frame)):
            return True
    return False


LIVE_LINK_DURATION_SEC = 180
LIVE_LINK_COMMAND = "live stream"
MANUAL_GARBAGE_COMMAND = "garbage detect"
GARBAGE_SAMPLE_CAPTURE_COMMAND = "garbage sample capture"
GARBAGE_AI_MODE_COMMAND = "garbage ai mode"
GARBAGE_NORMAL_MODE_COMMAND = "garbage normal mode"

AUDIO_PLAY_COMMAND = "audio file play"
IMPORT_DEFAULT_AUDIO_COMMAND = "import default audio"
ERASE_DEFAULT_AUDIO_IN_PI_COMMAND = "erase default audio in pi"
PLAY_DEFAULT_AUDIO_COMMAND_PREFIX = "play default audio - "
DATA_STATUS_COMMAND = "data status"
SOFTWARE_VERSION_COMMAND = "software version"
DEVICE_RESTART_COMMAND = "device restart"
WARNING_AUDIO_DISABLE_COMMAND = "warning audio disable"
WARNING_AUDIO_ENABLE_COMMAND = "warning audio enable"
WARNING_AUDIO_SCHEDULE_COMMAND = "scheduled warning audio"
WARNING_AUDIO_SCHEDULE_DISABLE_COMMAND = "scheduled warning disabled"
QUEUE_CLEAR_COMMAND = "queue clear"

PERSON_DETECTION_ENABLE_COMMAND = "person detection enable"
PERSON_DETECTION_DISABLE_COMMAND = "person detection disable"
VEHICLE_DETECTION_ENABLE_COMMAND = "vehicle detection enable"
VEHICLE_DETECTION_DISABLE_COMMAND = "vehicle detection disable"
GARBAGE_DETECTION_ENABLE_COMMAND = "garbage detection enable"
GARBAGE_DETECTION_DISABLE_COMMAND = "garbage detection disable"

PERSON_VIDEO_RECORDING_ENABLE_COMMAND = "person video recording enable"
PERSON_VIDEO_RECORDING_DISABLE_COMMAND = "person video recording disable"
VEHICLE_VIDEO_RECORDING_ENABLE_COMMAND = "vehicle video recording enable"
VEHICLE_VIDEO_RECORDING_DISABLE_COMMAND = "vehicle video recording disable"

GARBAGE_MODE_COMMANDS = {
    GARBAGE_AI_MODE_COMMAND,
    GARBAGE_NORMAL_MODE_COMMAND,
}

FEATURE_TOGGLE_COMMANDS = {
    PERSON_DETECTION_ENABLE_COMMAND,
    PERSON_DETECTION_DISABLE_COMMAND,
    VEHICLE_DETECTION_ENABLE_COMMAND,
    VEHICLE_DETECTION_DISABLE_COMMAND,
    GARBAGE_DETECTION_ENABLE_COMMAND,
    GARBAGE_DETECTION_DISABLE_COMMAND,
    PERSON_VIDEO_RECORDING_ENABLE_COMMAND,
    PERSON_VIDEO_RECORDING_DISABLE_COMMAND,
    VEHICLE_VIDEO_RECORDING_ENABLE_COMMAND,
    VEHICLE_VIDEO_RECORDING_DISABLE_COMMAND,
}

POWER_SAVING_ENABLE_COMMAND = "power saving enable"
POWER_SAVING_DISABLE_COMMAND = "power saving disable"
POWER_SAVING_STATUS_COMMAND = "power saving status"
POWER_SAVING_SCHEDULE_COMMAND = "scheduled power saving"
POWER_SAVING_SCHEDULE_DISABLE_COMMAND = "scheduled power saving disabled"

POWER_SAVING_ALLOWED_WHEN_ACTIVE = {
    DATA_STATUS_COMMAND,
    POWER_SAVING_ENABLE_COMMAND,
    POWER_SAVING_DISABLE_COMMAND,
    POWER_SAVING_STATUS_COMMAND,
    POWER_SAVING_SCHEDULE_COMMAND,
    POWER_SAVING_SCHEDULE_DISABLE_COMMAND,
    *FEATURE_TOGGLE_COMMANDS,
    *GARBAGE_MODE_COMMANDS,
}

POWER_SAVING_SCHEDULE_CHECK_SEC = 15

CLOUDFLARED_BIN = "/usr/bin/cloudflared"

STATE_FLUSH_INTERVAL_SEC = 10
HEARTBEAT_INTERVAL_SEC = 20

PUBLIC_IP_CACHE: Optional[str] = None
LAST_PUBLIC_IP_CHECK_TS = 0.0

GEO_LOCATION_CACHE: Optional[str] = None
LAST_GEO_CHECK_TS = 0.0

last_net_rx: Optional[int] = None
last_net_tx: Optional[int] = None
last_net_ts: Optional[float] = None

NETWORK_AUDIO_CHECK_INTERVAL_SEC = 3
NETWORK_AUDIO_TCP_TIMEOUT_SEC = 3
NETWORK_AUDIO_DEFAULT_RESTORE_VOLUME_PERCENT = 70

# Real internet test targets.
# Do not depend only on router/default route.
# If router is alive but WAN internet is down, these checks will fail and audio will mute.
NETWORK_AUDIO_INTERNET_TEST_TARGETS = (
    ("1.1.1.1", 443),   # Cloudflare HTTPS
    ("8.8.8.8", 53),    # Google DNS TCP
    ("9.9.9.9", 53),    # Quad9 DNS TCP
)

network_audio_last_online: Optional[bool] = None
network_audio_restore_volume: Optional[int] = None

LIVE_TALK_DIR = TMP_DIR / "live_talk"

LIVE_TALK_CHUNK_SECONDS = 0.75
LIVE_TALK_MAX_QUEUE_SIZE = 80
LIVE_TALK_MAX_CHUNK_BYTES = 2 * 1024 * 1024
LIVE_TALK_STOP_TIMEOUT_SEC = 4
LIVE_TALK_GRACEFUL_DRAIN_TIMEOUT_SEC = 8
LIVE_TALK_GRACEFUL_IDLE_DELAY_SEC = 2.0
LIVE_TALK_VOLUME_FILTER = "volume=3.0,alimiter=limit=0.95"

live_talk_queue: "queue.Queue[Dict[str, Any]]" = queue.Queue(maxsize=LIVE_TALK_MAX_QUEUE_SIZE)
live_talk_active = False
live_talk_stopping = False
live_talk_lock = threading.Lock()
live_talk_ffmpeg_process: Optional[subprocess.Popen] = None
live_talk_aplay_process: Optional[subprocess.Popen] = None

MOBILENET_CLASSES = [
    "background", "aeroplane", "bicycle", "bird", "boat",
    "bottle", "bus", "car", "cat", "chair", "cow", "diningtable",
    "dog", "horse", "motorbike", "person", "pottedplant", "sheep",
    "sofa", "train", "tvmonitor"
]

GARBAGE_REFERENCE_ITEMS = [
    "Dust", "Bags", "Covers", "Carry bags", "Box",
    "Leaf", "Slippers", "Shoes", "Paper", "Notebooks",
    "Cloths", "Dress"
]

app = Flask(__name__, template_folder=str(TEMPLATE_DIR))

state_lock = threading.RLock()
frame_lock = threading.Lock()
audio_lock = threading.Lock()
person_dnn_lock = threading.Lock()
stop_event = threading.Event()

latest_frame: Optional[np.ndarray] = None
latest_frame_ts: float = 0.0

rolling_video_lock = threading.Lock()
rolling_video_buffer = deque()

person_net = None
hog_detector = None
garbage_net = None
garbage_output_names: List[str] = []
garbage_clean_streak = 0

state_cache: Optional[Dict[str, Any]] = None
state_dirty = False
last_state_flush_ts = 0.0

cloudflare_lock = threading.Lock()
cloudflare_proc: Optional[subprocess.Popen] = None
cloudflare_url: Optional[str] = None
cloudflare_expiry_ts: float = 0.0
cloudflare_stop_timer: Optional[threading.Timer] = None

current_audio_proc: Optional[subprocess.Popen] = None
audio_queue: "queue.Queue[Dict[str, Any]]" = queue.Queue()
router_reboot_lock = threading.Lock()
last_router_reboot_schedule_key: Optional[str] = None

vehicle_event_lock = threading.Lock()
vehicle_event_results: Dict[str, Dict[str, Any]] = {}


def now_dt() -> dt.datetime:
    return dt.datetime.now()


def now_str() -> str:
    return now_dt().strftime("%Y-%m-%d %H:%M:%S")


def safe_write_json(path: Path, payload: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
    tmp.replace(path)


def load_config() -> Dict[str, Any]:
    with open(CONFIG_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


CONFIG = load_config()
GARBAGE_INTERVAL_SEC = max(60, int(CONFIG.get("garbage_interval_sec", DEFAULT_GARBAGE_INTERVAL_SEC)))


def normalize_garbage_detection_mode(raw: Any = None) -> str:
    mode = str(raw or DEFAULT_GARBAGE_DETECTION_MODE).strip().lower()

    if mode in {"ai", "ai mode", "aimode"}:
        return "ai"

    if mode in {"normal", "normal mode", "normalmode"}:
        return "normal"

    if mode not in GARBAGE_DETECTION_MODES:
        return DEFAULT_GARBAGE_DETECTION_MODE

    return mode


def garbage_detection_mode_label(mode: Any = None) -> str:
    normalized_mode = normalize_garbage_detection_mode(mode)
    return "AI Mode" if normalized_mode == "ai" else "Normal Mode"


def load_garbage_detection_mode() -> str:
    try:
        cfg = load_config()
        return normalize_garbage_detection_mode(cfg.get("garbage_detection_mode"))
    except Exception:
        return normalize_garbage_detection_mode(CONFIG.get("garbage_detection_mode"))


def set_garbage_detection_mode(mode: str, source: str = "dashboard") -> Dict[str, Any]:
    global CONFIG

    normalized_mode = normalize_garbage_detection_mode(mode)

    cfg = load_config()
    cfg["garbage_detection_mode"] = normalized_mode
    safe_write_json(CONFIG_FILE, cfg)
    CONFIG = cfg

    changed_at = now_str()
    label = garbage_detection_mode_label(normalized_mode)

    def mutate(state):
        state.setdefault("system", {})
        state["system"]["garbage_detection_mode"] = normalized_mode
        state["system"]["garbage_mode"] = label
        state["system"]["garbage_detection_mode_changed_at"] = changed_at
        state["system"]["garbage_detection_mode_changed_by"] = source

        state.setdefault("last_garbage", {})
        state["last_garbage"]["mode"] = normalized_mode

    update_state(mutate, force_flush=True)

    add_event("garbage_detection_mode_changed", {
        "mode": normalized_mode,
        "label": label,
        "source": source,
    })

    return {
        "garbage_detection_mode": normalized_mode,
        "garbage_detection_mode_label": label,
        "changed_at": changed_at,
    }


def garbage_detection_mode_payload() -> Dict[str, Any]:
    mode = load_garbage_detection_mode()
    return {
        "garbage_detection_mode": mode,
        "garbage_detection_mode_label": garbage_detection_mode_label(mode),
        "garbage_detection_modes": sorted(GARBAGE_DETECTION_MODES),
    }


def normalize_vehicle_detection_mode(raw: Any = None) -> str:
    mode = str(raw or DEFAULT_VEHICLE_DETECTION_MODE).strip().lower()
    if mode not in VEHICLE_DETECTION_MODES:
        return DEFAULT_VEHICLE_DETECTION_MODE
    return mode


def load_vehicle_detection_mode() -> str:
    try:
        cfg = load_config()
        return normalize_vehicle_detection_mode(cfg.get("vehicle_detection_mode"))
    except Exception:
        return normalize_vehicle_detection_mode(CONFIG.get("vehicle_detection_mode"))


def load_vehicle_geofence_overlap_threshold() -> float:
    try:
        cfg = load_config()
        value = float(cfg.get("vehicle_geofence_overlap_threshold", VEHICLE_GEOFENCE_MIN_OVERLAP_RATIO))
    except Exception:
        try:
            value = float(CONFIG.get("vehicle_geofence_overlap_threshold", VEHICLE_GEOFENCE_MIN_OVERLAP_RATIO))
        except Exception:
            value = VEHICLE_GEOFENCE_MIN_OVERLAP_RATIO

    return max(0.05, min(0.95, value))


def set_vehicle_detection_mode(mode: str, source: str = "dashboard") -> Dict[str, Any]:
    global CONFIG

    normalized_mode = normalize_vehicle_detection_mode(mode)
    cfg = load_config()
    cfg["vehicle_detection_mode"] = normalized_mode
    safe_write_json(CONFIG_FILE, cfg)
    CONFIG = cfg

    changed_at = now_str()

    def mutate(state):
        state.setdefault("system", {})
        state["system"]["vehicle_detection_mode"] = normalized_mode
        state["system"]["vehicle_detection_mode_changed_at"] = changed_at
        state["system"]["vehicle_detection_mode_changed_by"] = source
        state.setdefault("last_vehicle", {})
        state["last_vehicle"]["mode"] = normalized_mode
        state["last_vehicle"]["detected"] = False

    update_state(mutate, force_flush=True)

    add_event("vehicle_detection_mode_changed", {
        "mode": normalized_mode,
        "source": source,
    })

    return {
        "vehicle_detection_mode": normalized_mode,
        "vehicle_geofence_overlap_threshold": load_vehicle_geofence_overlap_threshold(),
        "changed_at": changed_at,
    }


def vehicle_detection_mode_payload() -> Dict[str, Any]:
    return {
        "vehicle_detection_mode": load_vehicle_detection_mode(),
        "vehicle_detection_modes": sorted(VEHICLE_DETECTION_MODES),
        "vehicle_geofence_overlap_threshold": load_vehicle_geofence_overlap_threshold(),
    }


DEFAULT_FEATURE_FLAGS: Dict[str, bool] = {
    "person_detection_enabled": True,
    "vehicle_detection_enabled": True,
    "garbage_detection_enabled": True,
    "person_video_recording_enabled": True,
    "vehicle_video_recording_enabled": True,
}


def normalize_feature_flags(raw: Any = None) -> Dict[str, bool]:
    flags = dict(DEFAULT_FEATURE_FLAGS)

    if isinstance(raw, dict):
        for key in DEFAULT_FEATURE_FLAGS:
            if key in raw:
                flags[key] = bool(raw[key])

    return flags


def load_feature_flags() -> Dict[str, bool]:
    try:
        cfg = load_config()
        return normalize_feature_flags(cfg.get("feature_flags", {}))
    except Exception:
        return normalize_feature_flags(CONFIG.get("feature_flags", {}))


def is_feature_enabled(flag_name: str) -> bool:
    return bool(load_feature_flags().get(flag_name, True))


def feature_flags_payload() -> Dict[str, Any]:
    flags = load_feature_flags()
    return {
        "feature_flags": flags,
        **flags,
    }


def set_feature_flag(flag_name: str, enabled: bool, source: str = "mqtt command") -> Dict[str, bool]:
    global CONFIG

    if flag_name not in DEFAULT_FEATURE_FLAGS:
        raise ValueError(f"unknown feature flag: {flag_name}")

    cfg = load_config()
    flags = normalize_feature_flags(cfg.get("feature_flags", {}))
    flags[flag_name] = bool(enabled)

    cfg["feature_flags"] = flags
    safe_write_json(CONFIG_FILE, cfg)
    CONFIG = cfg

    changed_at = now_str()

    def mutate(state):
        state["feature_flags"] = flags
        state.setdefault("system", {})
        state["system"]["feature_flags_last_changed_at"] = changed_at
        state["system"]["feature_flags_last_changed_by"] = source

        if flag_name == "person_detection_enabled" and not enabled and "last_person" in state:
            state["last_person"]["detected"] = False

        if flag_name == "vehicle_detection_enabled" and not enabled and "last_vehicle" in state:
            state["last_vehicle"]["detected"] = False

    update_state(mutate, force_flush=True)

    add_event("feature_flag_changed", {
        "flag": flag_name,
        "enabled": bool(enabled),
        "source": source,
    })

    return flags


RTSP_URL = CONFIG["rtsp_url"]
CLOUDFLARE_LIVE_RTSP_URL = CONFIG.get("cloudflare_live_rtsp_url", RTSP_URL)
PI_IP = CONFIG["pi_ip"]
ROUTER_IP = CONFIG["router_ip"]
CAMERA_IP = CONFIG["camera_ip"]
CAMERA_NAME = CONFIG["camera_name"]
DEVICE_ID = CONFIG["device_id"]
CAMERA_CONFIG_URL = CONFIG.get("camera_config_url", f"https://{CAMERA_IP}:443")

ROUTER_REBOOT_SCHEDULE = CONFIG.get("router_reboot_schedule", {})
ROUTER_REBOOT_TIME_1 = str(ROUTER_REBOOT_SCHEDULE.get("time_1", "03:25")).strip()
ROUTER_REBOOT_TIME_2 = str(ROUTER_REBOOT_SCHEDULE.get("time_2", "15:25")).strip()

def is_valid_hhmm(value: str) -> bool:
    return bool(re.fullmatch(r"(?:[01]\d|2[0-3]):[0-5]\d", value))

ROUTER_REBOOT_SCHEDULE_TIMES = {
    t for t in {ROUTER_REBOOT_TIME_1, ROUTER_REBOOT_TIME_2}
    if t and is_valid_hhmm(t)
}

SUBDOMAIN_NAME = str(CONFIG.get("subdomain_name", "")).strip().lower()
BASE_DOMAIN = str(CONFIG.get("base_domain", "")).strip().lower()
PERMANENT_LIVE_BASE_URL = str(CONFIG.get("permanent_live_base_url", "")).strip().rstrip("/")

if not PERMANENT_LIVE_BASE_URL and SUBDOMAIN_NAME and BASE_DOMAIN:
    PERMANENT_LIVE_BASE_URL = f"https://{SUBDOMAIN_NAME}.{BASE_DOMAIN}"

PERMANENT_LIVE_URL = f"{PERMANENT_LIVE_BASE_URL}/live" if PERMANENT_LIVE_BASE_URL else None
PERMANENT_DASHBOARD_URL = PERMANENT_LIVE_BASE_URL if PERMANENT_LIVE_BASE_URL else None

SOFTWARE_VERSION = str(CONFIG.get("software_version", "Version 6.5")).strip()
SOFTWARE_FEATURES = str(
    CONFIG.get(
        "software_features",
                "AI Mode/Normal Mode Interface for Garbage Detection"
    )
).strip()
TAILSCALE_MAIL_ID = str(CONFIG.get("tailscale_mail_id", "")).strip()

MQTT_HOST = CONFIG["mqtt"]["host"]
MQTT_PORT = int(CONFIG["mqtt"]["port"])
MQTT_USERNAME = CONFIG["mqtt"]["username"]
MQTT_PASSWORD = CONFIG["mqtt"]["password"]
MQTT_GARBAGE_TOPIC = CONFIG["mqtt"]["garbage_topic"]
MQTT_CAMERA_EVENT_TOPIC = CONFIG["mqtt"]["camera_event_topic"]
MQTT_COMMAND_TOPIC = CONFIG["mqtt"].get("command_topic", "G-Cam-RnD/command")
MQTT_RESPONSE_TOPIC = CONFIG["mqtt"].get("response_topic", "G-Cam-RnD/device_response")
MQTT_COMMAND_TOKEN = CONFIG["mqtt"].get("command_token", "")

SFTP_HOST = CONFIG["sftp"]["host"]
SFTP_PORT = int(CONFIG["sftp"].get("port", 22))
SFTP_USERNAME = CONFIG["sftp"]["username"]
SFTP_PASSWORD = CONFIG["sftp"]["password"]
SFTP_BASE_DIR = CONFIG["sftp"]["base_dir"]
SFTP_PERSON_DIR = CONFIG["sftp"].get("person_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Person")
SFTP_PERSON_VIDEO_DIR = CONFIG["sftp"].get("person_video_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Video/Person_Video")
SFTP_VEHICLE_DIR = CONFIG["sftp"].get("vehicle_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Vehicle")
SFTP_VEHICLE_VIDEO_DIR = CONFIG["sftp"].get("vehicle_video_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Video/Vehicle_Video")
SFTP_GARBAGE_DIR = CONFIG["sftp"].get("garbage_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Garbage")
SFTP_AUDIO_DIR = CONFIG["sftp"].get("audio_dir", f"{SFTP_BASE_DIR.rstrip('/')}/Audio")
SFTP_DEFAULT_AUDIO_DIR = CONFIG["sftp"].get("default_audio_dir", "/home/ftpuser/ftp/files/Default_Audio")
BATTERY_MODE = str(CONFIG.get("battery", {}).get("mode", "disabled")).strip().lower()
BATTERY_SYSFS_PATH = str(CONFIG.get("battery", {}).get("sysfs_path", "")).strip()
BATTERY_COMMAND = str(CONFIG.get("battery", {}).get("command", "")).strip()
BATTERY_DIVIDER_RATIO = float(CONFIG.get("battery", {}).get("divider_ratio", 1.0))


def detect_local_ip() -> str:
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.connect(("8.8.8.8", 80))
        ip = sock.getsockname()[0]
        sock.close()
        if ip and ip != "127.0.0.1":
            return ip
    except Exception:
        pass
    return PI_IP


def detect_public_ip(force: bool = False) -> Optional[str]:
    global PUBLIC_IP_CACHE, LAST_PUBLIC_IP_CHECK_TS

    if not force and PUBLIC_IP_CACHE and (time.time() - LAST_PUBLIC_IP_CHECK_TS) < 900:
        return PUBLIC_IP_CACHE

    for url in ["https://api.ipify.org", "https://checkip.amazonaws.com"]:
        try:
            r = requests.get(url, timeout=3)
            ip = r.text.strip()
            ipaddress.ip_address(ip)
            PUBLIC_IP_CACHE = ip
            LAST_PUBLIC_IP_CHECK_TS = time.time()
            return ip
        except Exception:
            continue

    LAST_PUBLIC_IP_CHECK_TS = time.time()
    return PUBLIC_IP_CACHE


def detect_geo_location(force: bool = False) -> Optional[str]:
    global GEO_LOCATION_CACHE, LAST_GEO_CHECK_TS

    if not force and GEO_LOCATION_CACHE and (time.time() - LAST_GEO_CHECK_TS) < 1800:
        return GEO_LOCATION_CACHE

    try:
        r = requests.get("https://ipapi.co/json/", timeout=4)
        data = r.json()
        city = str(data.get("city", "")).strip()
        region = str(data.get("region", "")).strip()
        country = str(data.get("country_name", "")).strip()
        lat = data.get("latitude")
        lon = data.get("longitude")
        parts = [p for p in [city, region, country] if p]
        location = ", ".join(parts)
        if lat is not None and lon is not None:
            location = f"{location} ({lat}, {lon})" if location else f"{lat}, {lon}"
        GEO_LOCATION_CACHE = location or None
        LAST_GEO_CHECK_TS = time.time()
        return GEO_LOCATION_CACHE
    except Exception:
        LAST_GEO_CHECK_TS = time.time()
        return GEO_LOCATION_CACHE


def local_dashboard_url() -> str:
    return f"http://{detect_local_ip()}:{PORT}"


def local_stream_url() -> str:
    return f"{local_dashboard_url()}/video_feed"


def default_state() -> Dict[str, Any]:
    return {
        "app_name": 'G-Cam "RealTech Systems"',
        "started_at": now_str(),
        "camera_ip": CAMERA_IP,
        "router_ip": ROUTER_IP,
        "raspberry_ip": detect_local_ip(),
        "device_id": DEVICE_ID,
        "camera_name": CAMERA_NAME,
        "camera_config_url": CAMERA_CONFIG_URL,
        "public_ip": detect_public_ip(),
        "system": {
            "camera_connected": False,
            "last_frame_at": None,
            "last_error": None,
            "sftp_last_ok": None,
            "sftp_last_error": None,
            "mqtt_last_ok": None,
            "mqtt_last_error": None,
            "command_listener": False,
            "garbage_mode": garbage_detection_mode_label(load_garbage_detection_mode()),
            "garbage_detection_mode": load_garbage_detection_mode(),
            "garbage_detection_mode_changed_at": None,
            "garbage_detection_mode_changed_by": None,
            "pi_temperature_c": None,
            "network_status": None,
            "network_iface": None,
            "network_link_speed_mbps": None,
            "network_rx_bps": 0,
            "network_tx_bps": 0,
            "network_audio_muted": False,
            "network_audio_mute_reason": None,
            "network_audio_last_changed_at": None,
            "network_audio_last_check_at": None,
            "network_audio_iface": None,
            "network_audio_saved_volume": None,
            "live_talk_active": False,
            "live_talk_draining": False,
            "live_talk_last_started_at": None,
            "live_talk_last_stopped_at": None,
            "live_talk_last_stop_requested_at": None,
            "live_talk_last_chunk_at": None,
            "live_talk_last_error": None,
            "live_talk_queue_size": 0,
            "battery_voltage": None,
            "battery_current_a": None,
            "battery_current_ma": None,
            "battery_status": None,
            "ram_total_mb": None,
            "ram_used_mb": None,
            "ram_percent": None,
            "geo_location": None,
            "last_restart_request_at": None,
            "garbage_interval_sec": GARBAGE_INTERVAL_SEC,
            "power_saving_active": False,
            "power_saving_source": None,
            "power_saving_reason": None,
            "power_saving_last_changed_at": None,
            "feature_flags_last_changed_at": None,
            "feature_flags_last_changed_by": None,
            "vehicle_detection_mode": load_vehicle_detection_mode(),
            "vehicle_geofence_overlap_threshold": load_vehicle_geofence_overlap_threshold(),
            "vehicle_detection_mode_changed_at": None,
            "vehicle_detection_mode_changed_by": None
        },
        "links": {
            "dashboard_local": local_dashboard_url(),
            "stream_local": local_stream_url()
        },
        "live_link": {
            "active": False,
            "url": None,
            "expires_at": None
        },
        "counts": {"HIGH": 0, "MEDIUM": 0, "LOW": 0},
        "feature_flags": load_feature_flags(),
        "last_person": {
            "detected": False,
            "time": None,
            "image": None,
            "count": 0,
            "best_confidence": 0.0
        },
        "last_garbage": {
            "detected": False,
            "time": None,
            "image": None,
            "severity": None,
            "diff_ratio": 0.0,
            "edge_ratio": 0.0,
            "blob_area_ratio": 0.0,
            "blob_count": 0,
            "triggered_by": None,
            "reference_items": [],
            "mode": load_garbage_detection_mode()
        },
        "last_audio": {
            "file": None,
            "source_path": None,
            "time": None,
            "status": None
        },
        "warning_audio": {
            "enabled": True,
            "schedule_enabled": False,
            "start_time": "10:00",
            "end_time": "18:00",
            "last_status": None
        },
        "power_saving": {
            "enabled": False,
            "source": None,
            "reason": None,
            "last_changed_at": None,
            "schedule_enabled": False,
            "start_time": "22:00",
            "end_time": "06:00",
            "last_schedule_check": None
        },
        "last_video": {
            "file": None,
            "time": None,
            "status": None,
            "duration_sec": 0,
            "person_count": 0,
            "best_confidence": 0.0
        },
        "last_vehicle": {
            "detected": False,
            "time": None,
            "image": None,
            "count": 0,
            "best_confidence": 0.0,
            "labels": [],
            "image_status": None,
            "image_file": None,
            "image_uploaded_path": None,
            "mode": load_vehicle_detection_mode(),
            "geofence_overlap": 0.0
        },
        "last_vehicle_video": {
            "file": None,
            "time": None,
            "status": None,
            "duration_sec": 0,
            "vehicle_count": 0,
            "best_confidence": 0.0,
            "labels": []
        },
        "events": []
    }


def flush_state(force: bool = False) -> None:
    global state_dirty, last_state_flush_ts
    with state_lock:
        if state_cache is None:
            return
        now_ts = time.time()
        if not force and not state_dirty:
            return
        if not force and (now_ts - last_state_flush_ts) < STATE_FLUSH_INTERVAL_SEC:
            return
        safe_write_json(STATE_FILE, state_cache)
        state_dirty = False
        last_state_flush_ts = now_ts


def load_state() -> Dict[str, Any]:
    global state_cache, last_state_flush_ts
    with state_lock:
        if state_cache is not None:
            return json.loads(json.dumps(state_cache))

        if not STATE_FILE.exists():
            state_cache = default_state()
            safe_write_json(STATE_FILE, state_cache)
            last_state_flush_ts = time.time()
            return json.loads(json.dumps(state_cache))

        try:
            with open(STATE_FILE, "r", encoding="utf-8") as f:
                raw = f.read().strip()
            state_cache = json.loads(raw) if raw else default_state()
        except Exception:
            state_cache = default_state()
            safe_write_json(STATE_FILE, state_cache)

        if "system" not in state_cache:
            state_cache = default_state()

        state_cache["system"].setdefault("ram_total_mb", None)
        state_cache["system"].setdefault("ram_used_mb", None)
        state_cache["system"].setdefault("ram_percent", None)

        current_garbage_mode = load_garbage_detection_mode()
        state_cache["system"]["garbage_detection_mode"] = current_garbage_mode
        state_cache["system"]["garbage_mode"] = garbage_detection_mode_label(current_garbage_mode)
        state_cache["system"].setdefault("garbage_detection_mode_changed_at", None)
        state_cache["system"].setdefault("garbage_detection_mode_changed_by", None)

        if "last_garbage" not in state_cache:
            state_cache["last_garbage"] = default_state()["last_garbage"]
        state_cache["last_garbage"].setdefault("reference_items", [])
        state_cache["last_garbage"].setdefault("mode", current_garbage_mode)

        if "warning_audio" not in state_cache:
            state_cache["warning_audio"] = default_state()["warning_audio"]
        state_cache["warning_audio"].setdefault("enabled", True)
        state_cache["warning_audio"].setdefault("schedule_enabled", False)
        state_cache["warning_audio"].setdefault("start_time", "10:00")
        state_cache["warning_audio"].setdefault("end_time", "18:00")
        state_cache["warning_audio"].setdefault("last_status", None)

        if "power_saving" not in state_cache:
            state_cache["power_saving"] = default_state()["power_saving"]

        state_cache["power_saving"].setdefault("enabled", False)
        state_cache["power_saving"].setdefault("source", None)
        state_cache["power_saving"].setdefault("reason", None)
        state_cache["power_saving"].setdefault("last_changed_at", None)
        state_cache["power_saving"].setdefault("schedule_enabled", False)
        state_cache["power_saving"].setdefault("start_time", "22:00")
        state_cache["power_saving"].setdefault("end_time", "06:00")
        state_cache["power_saving"].setdefault("last_schedule_check", None)

        state_cache["system"].setdefault("power_saving_active", False)
        state_cache["system"].setdefault("power_saving_source", None)
        state_cache["system"].setdefault("power_saving_reason", None)
        state_cache["system"].setdefault("power_saving_last_changed_at", None)

        state_cache["system"]["power_saving_active"] = bool(state_cache["power_saving"].get("enabled", False))
        state_cache["system"]["power_saving_source"] = state_cache["power_saving"].get("source")
        state_cache["system"]["power_saving_reason"] = state_cache["power_saving"].get("reason")
        state_cache["system"]["power_saving_last_changed_at"] = state_cache["power_saving"].get("last_changed_at")

        state_cache["system"].setdefault("network_audio_muted", False)
        state_cache["system"].setdefault("network_audio_mute_reason", None)
        state_cache["system"].setdefault("network_audio_last_changed_at", None)
        state_cache["system"].setdefault("network_audio_last_check_at", None)
        state_cache["system"].setdefault("network_audio_iface", None)
        state_cache["system"].setdefault("network_audio_saved_volume", None)
        state_cache["system"].setdefault("live_talk_active", False)
        state_cache["system"].setdefault("live_talk_draining", False)
        state_cache["system"].setdefault("live_talk_last_started_at", None)
        state_cache["system"].setdefault("live_talk_last_stopped_at", None)
        state_cache["system"].setdefault("live_talk_last_stop_requested_at", None)
        state_cache["system"].setdefault("live_talk_last_chunk_at", None)
        state_cache["system"].setdefault("live_talk_last_error", None)
        state_cache["system"].setdefault("live_talk_queue_size", 0)

        state_cache["system"].setdefault("feature_flags_last_changed_at", None)
        state_cache["system"].setdefault("feature_flags_last_changed_by", None)
        state_cache["system"]["vehicle_detection_mode"] = load_vehicle_detection_mode()
        state_cache["system"]["vehicle_geofence_overlap_threshold"] = load_vehicle_geofence_overlap_threshold()
        state_cache["system"].setdefault("vehicle_detection_mode_changed_at", None)
        state_cache["system"].setdefault("vehicle_detection_mode_changed_by", None)
        state_cache["feature_flags"] = load_feature_flags()

        if "last_vehicle" not in state_cache:
            state_cache["last_vehicle"] = default_state()["last_vehicle"]
        state_cache["last_vehicle"].setdefault("detected", False)
        state_cache["last_vehicle"].setdefault("time", None)
        state_cache["last_vehicle"].setdefault("image", None)
        state_cache["last_vehicle"].setdefault("count", 0)
        state_cache["last_vehicle"].setdefault("best_confidence", 0.0)
        state_cache["last_vehicle"].setdefault("mode", load_vehicle_detection_mode())
        state_cache["last_vehicle"].setdefault("geofence_overlap", 0.0)
        state_cache["last_vehicle"].setdefault("labels", [])
        state_cache["last_vehicle"].setdefault("image_status", None)
        state_cache["last_vehicle"].setdefault("image_file", None)
        state_cache["last_vehicle"].setdefault("image_uploaded_path", None)

        if "last_vehicle_video" not in state_cache:
            state_cache["last_vehicle_video"] = default_state()["last_vehicle_video"]
        state_cache["last_vehicle_video"].setdefault("file", None)
        state_cache["last_vehicle_video"].setdefault("time", None)
        state_cache["last_vehicle_video"].setdefault("status", None)
        state_cache["last_vehicle_video"].setdefault("duration_sec", 0)
        state_cache["last_vehicle_video"].setdefault("vehicle_count", 0)
        state_cache["last_vehicle_video"].setdefault("best_confidence", 0.0)
        state_cache["last_vehicle_video"].setdefault("labels", [])

        last_state_flush_ts = time.time()
        return json.loads(json.dumps(state_cache))


def update_state(mutator, force_flush: bool = False) -> None:
    global state_cache, state_dirty
    with state_lock:
        if state_cache is None:
            state_cache = default_state()
        mutator(state_cache)
        state_dirty = True
    flush_state(force=force_flush)


def add_event(event_type: str, details: Dict[str, Any]) -> None:
    def mutate(state):
        state["events"].insert(0, {"type": event_type, "time": now_str(), **details})
        state["events"] = state["events"][:120]
    update_state(mutate)


def add_event(event_type: str, details: Dict[str, Any]) -> None:
    def mutate(state):
        state["events"].insert(0, {"type": event_type, "time": now_str(), **details})
        state["events"] = state["events"][:120]
    update_state(mutate)


def is_power_saving_active() -> bool:
    try:
        state = load_state()
        return bool(state.get("power_saving", {}).get("enabled", False))
    except Exception:
        return False


def clear_latest_frame() -> None:
    global latest_frame, latest_frame_ts

    with frame_lock:
        latest_frame = None
        latest_frame_ts = 0.0


def power_saving_status_payload() -> Dict[str, Any]:
    state = load_state()
    ps = state.get("power_saving", {})

    return {
        "power_saving_enabled": bool(ps.get("enabled", False)),
        "power_saving_source": ps.get("source"),
        "power_saving_reason": ps.get("reason"),
        "power_saving_last_changed_at": ps.get("last_changed_at"),
        "power_saving_schedule_enabled": bool(ps.get("schedule_enabled", False)),
        "power_saving_schedule_start_time": ps.get("start_time"),
        "power_saving_schedule_end_time": ps.get("end_time"),
        "power_saving_last_schedule_check": ps.get("last_schedule_check")
    }


def set_power_saving_enabled(
    enabled: bool,
    reason: str,
    source: str,
    disable_schedule: bool = False
) -> Dict[str, Any]:
    changed_at = now_str()

    def mutate(state):
        state.setdefault("power_saving", default_state()["power_saving"])
        state["power_saving"]["enabled"] = bool(enabled)
        state["power_saving"]["source"] = source
        state["power_saving"]["reason"] = reason
        state["power_saving"]["last_changed_at"] = changed_at

        if disable_schedule:
            state["power_saving"]["schedule_enabled"] = False

        state["system"]["power_saving_active"] = bool(enabled)
        state["system"]["power_saving_source"] = source
        state["system"]["power_saving_reason"] = reason
        state["system"]["power_saving_last_changed_at"] = changed_at

        if enabled:
            state["system"]["camera_connected"] = False
            state["system"]["last_error"] = "Power saving mode active"
            state["last_person"]["detected"] = False
            state["last_vehicle"]["detected"] = False
            state["live_link"] = {
                "active": False,
                "url": None,
                "expires_at": None
            }
            state["last_audio"] = {
                "file": None,
                "source_path": None,
                "time": changed_at,
                "status": "stopped_power_saving"
            }
        else:
            state["system"]["last_error"] = None

    update_state(mutate, force_flush=True)

    if enabled:
        clear_latest_frame()

        try:
            stop_current_audio_process()
        except Exception:
            pass

        try:
            clear_audio_queue()
        except Exception:
            pass

        try:
            stop_live_tunnel()
        except Exception:
            pass

    add_event("power_saving_changed", {
        "enabled": bool(enabled),
        "source": source,
        "reason": reason
    })

    return power_saving_status_payload()


def is_time_inside_range(now_time: dt.time, start_time: dt.time, end_time: dt.time) -> bool:
    if start_time == end_time:
        return True

    if start_time < end_time:
        return start_time <= now_time < end_time

    return now_time >= start_time or now_time < end_time


def parse_power_saving_schedule(payload: Dict[str, Any]) -> Tuple[Optional[str], Optional[str]]:
    start_raw = (
        payload.get("start_time")
        or payload.get("start")
        or payload.get("from")
        or payload.get("startTime")
    )
    end_raw = (
        payload.get("end_time")
        or payload.get("end")
        or payload.get("to")
        or payload.get("endTime")
    )

    return (
        str(start_raw).strip() if start_raw is not None else None,
        str(end_raw).strip() if end_raw is not None else None
    )


def build_power_saving_blocked_response(cmd: str) -> Dict[str, Any]:
    return {
        "device_id": DEVICE_ID,
        "event": "command_blocked_power_saving",
        "command": cmd,
        "status": "blocked",
        "reason": "Power saving mode active. Only Data Status and Power Saving commands are allowed.",
        **power_saving_status_payload(),
        "time": now_str()
    }


def power_saving_command_allowed(cmd: str) -> bool:
    return cmd in POWER_SAVING_ALLOWED_WHEN_ACTIVE


def refresh_runtime_links_in_state() -> None:
    def mutate(state):
        state["raspberry_ip"] = detect_local_ip()
        state["public_ip"] = detect_public_ip()
        state["links"]["dashboard_local"] = local_dashboard_url()
        state["links"]["stream_local"] = local_stream_url()
    update_state(mutate)


def init_models() -> None:
    global person_net, hog_detector, garbage_net, garbage_output_names

    # Person model (unchanged)
    proto = MODEL_DIR / "MobileNetSSD_deploy.prototxt"
    model = MODEL_DIR / "MobileNetSSD_deploy.caffemodel"

    if not proto.exists():
        raise FileNotFoundError(f"Missing model file: {proto}")
    if not model.exists():
        raise FileNotFoundError(f"Missing model file: {model}")

    person_net = cv2.dnn.readNetFromCaffe(str(proto), str(model))

    hog_detector = cv2.HOGDescriptor()
    hog_detector.setSVMDetector(cv2.HOGDescriptor_getDefaultPeopleDetector())

    # Garbage object detector (new)
    garbage_model = MODEL_DIR / "garbage_yolo.onnx"
    if not garbage_model.exists():
        raise FileNotFoundError(f"Missing model file: {garbage_model}")

    garbage_net = cv2.dnn.readNetFromONNX(str(garbage_model))
    garbage_net.setPreferableBackend(cv2.dnn.DNN_BACKEND_OPENCV)
    garbage_net.setPreferableTarget(cv2.dnn.DNN_TARGET_CPU)

    try:
        layer_names = garbage_net.getLayerNames()
        out_layers = garbage_net.getUnconnectedOutLayers()
        garbage_output_names = [layer_names[i - 1] for i in out_layers.flatten()]
    except Exception:
        garbage_output_names = []


def get_frame_copy() -> Optional[np.ndarray]:
    with frame_lock:
        if latest_frame is None:
            return None
        return latest_frame.copy()


def get_frame_copy_with_ts() -> Tuple[Optional[np.ndarray], float]:
    with frame_lock:
        if latest_frame is None:
            return None, 0.0
        return latest_frame.copy(), latest_frame_ts


def get_garbage_label_display(label: str) -> str:
    label = str(label or "").strip().lower()
    if not label:
        return "Object"
    return GARBAGE_LABEL_ALIAS.get(label, label.title())


def box_iou_xyxy(a: Tuple[int, int, int, int], b: Tuple[int, int, int, int]) -> float:
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b

    inter_x1 = max(ax1, bx1)
    inter_y1 = max(ay1, by1)
    inter_x2 = min(ax2, bx2)
    inter_y2 = min(ay2, by2)

    iw = max(0, inter_x2 - inter_x1)
    ih = max(0, inter_y2 - inter_y1)
    inter = iw * ih

    if inter <= 0:
        return 0.0

    area_a = max(1, (ax2 - ax1) * (ay2 - ay1))
    area_b = max(1, (bx2 - bx1) * (by2 - by1))
    union = area_a + area_b - inter
    return float(inter) / float(max(union, 1))


def dedupe_garbage_detections(
    detections: List[Dict[str, Any]],
    iou_threshold: float = 0.45
) -> List[Dict[str, Any]]:
    if not detections:
        return []

    detections = sorted(detections, key=lambda x: x["confidence"], reverse=True)
    kept: List[Dict[str, Any]] = []

    for det in detections:
        should_keep = True
        for prev in kept:
            if det["label"] == prev["label"] and box_iou_xyxy(det["box"], prev["box"]) >= iou_threshold:
                should_keep = False
                break
        if should_keep:
            kept.append(det)

    return kept


def _normalize_yolo_output(raw_outputs: Any) -> np.ndarray:
    """
    Normalizes YOLO ONNX outputs to shape: (N, C)
    Supports common layouts:
      - (1, 84, 2100)  -> transpose to (2100, 84)   [YOLOv8]
      - (1, 2100, 84)  -> squeeze to   (2100, 84)
      - (2100, 84)     -> keep as-is
    """
    if isinstance(raw_outputs, (list, tuple)):
        if len(raw_outputs) == 1:
            raw_outputs = raw_outputs[0]
        else:
            parts = []
            for out in raw_outputs:
                arr = np.asarray(out)
                if arr.ndim == 3 and arr.shape[0] == 1:
                    arr = arr[0]
                if arr.ndim == 2:
                    if arr.shape[0] in (84, 85) and arr.shape[1] > arr.shape[0]:
                        arr = arr.T
                    parts.append(arr)
            if not parts:
                return np.empty((0, 0), dtype=np.float32)
            return np.concatenate(parts, axis=0)

    arr = np.asarray(raw_outputs)

    if arr.ndim == 3 and arr.shape[0] == 1:
        arr = arr[0]

    if arr.ndim != 2:
        return np.empty((0, 0), dtype=np.float32)

    # YOLOv8 export often gives (84, N)
    if arr.shape[0] in (84, 85) and arr.shape[1] > arr.shape[0]:
        arr = arr.T

    return arr


def detect_garbage_objects(roi: np.ndarray) -> List[Dict[str, Any]]:
    if garbage_net is None or roi is None or roi.size == 0:
        return []

    roi_h, roi_w = roi.shape[:2]
    roi_area = float(max(roi_h * roi_w, 1))

    min_box_area = max(
        int(GARBAGE_MIN_BOX_AREA_PX),
        int(roi_area * GARBAGE_MIN_BOX_AREA_RATIO)
    )
    min_box_w = max(18, int(roi_w * GARBAGE_MIN_BOX_WIDTH_RATIO))
    min_box_h = max(18, int(roi_h * GARBAGE_MIN_BOX_HEIGHT_RATIO))

    blob = cv2.dnn.blobFromImage(
        roi,
        scalefactor=1.0 / 255.0,
        size=(GARBAGE_OBJ_INPUT_SIZE, GARBAGE_OBJ_INPUT_SIZE),
        swapRB=True,
        crop=False
    )

    garbage_net.setInput(blob)

    if garbage_output_names:
        raw_outputs = garbage_net.forward(garbage_output_names)
    else:
        raw_outputs = garbage_net.forward()

    data = _normalize_yolo_output(raw_outputs)
    if data.size == 0:
        return []

    boxes_xywh: List[List[int]] = []
    confidences: List[float] = []
    class_ids: List[int] = []

    for row in data:
        row = np.asarray(row).reshape(-1)

        if row.shape[0] < 6:
            continue

        if row.shape[0] == 84:
            class_scores = row[4:]
            if class_scores.size == 0:
                continue

            class_id = int(np.argmax(class_scores))
            conf = float(class_scores[class_id])
        else:
            obj_conf = float(row[4])
            class_scores = row[5:]
            if class_scores.size == 0:
                continue

            class_id = int(np.argmax(class_scores))
            class_conf = float(class_scores[class_id])
            conf = obj_conf * class_conf

        if conf < GARBAGE_OBJ_CONF:
            continue

        cx = float(row[0])
        cy = float(row[1])
        bw = float(row[2])
        bh = float(row[3])

        if 0.0 <= cx <= 1.5 and 0.0 <= cy <= 1.5 and 0.0 <= bw <= 1.5 and 0.0 <= bh <= 1.5:
            cx *= roi_w
            cy *= roi_h
            bw *= roi_w
            bh *= roi_h

        x = int(cx - (bw / 2.0))
        y = int(cy - (bh / 2.0))
        w = int(bw)
        h = int(bh)

        x = max(0, x)
        y = max(0, y)
        w = min(w, roi_w - x)
        h = min(h, roi_h - y)

        if w <= 0 or h <= 0:
            continue

        box_area = w * h
        if box_area < min_box_area:
            continue
        if w < min_box_w or h < min_box_h:
            continue
        if (box_area / roi_area) > GARBAGE_MAX_BOX_AREA_RATIO:
            continue

        cx_box = x + (w // 2)
        cy_box = y + (h // 2)
        excluded = False
        for rx1, ry1, rx2, ry2 in GARBAGE_EXCLUSION_ZONES:
            ex1 = int(roi_w * rx1)
            ey1 = int(roi_h * ry1)
            ex2 = int(roi_w * rx2)
            ey2 = int(roi_h * ry2)
            if ex1 <= cx_box <= ex2 and ey1 <= cy_box <= ey2:
                excluded = True
                break
        if excluded:
            continue

        boxes_xywh.append([x, y, w, h])
        confidences.append(conf)
        class_ids.append(class_id)

    if not boxes_xywh:
        return []

    idxs = cv2.dnn.NMSBoxes(
        boxes_xywh,
        confidences,
        GARBAGE_OBJ_CONF,
        GARBAGE_OBJ_NMS
    )
    if idxs is None or len(idxs) == 0:
        return []

    coco_labels = [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    ]

    detections: List[Dict[str, Any]] = []

    for idx in np.array(idxs).reshape(-1):
        class_id = int(class_ids[idx])
        if class_id < 0 or class_id >= len(coco_labels):
            continue

        label = coco_labels[class_id]
        if label not in GARBAGE_TARGET_LABELS:
            continue

        x, y, w, h = boxes_xywh[idx]
        detections.append({
            "label": label,
            "display_label": get_garbage_label_display(label),
            "confidence": round(float(confidences[idx]), 4),
            "box": (x, y, x + w, y + h)
        })

    return dedupe_garbage_detections(detections, iou_threshold=0.40)


def save_capture(frame: np.ndarray, filename: str, garbage: bool = False) -> Tuple[str, Path]:
    folder = GARBAGE_DIR if garbage else FILES_DIR
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / filename

    if frame is None or frame.size == 0:
        raise RuntimeError(f"Cannot save empty frame: {filename}")

    ok = cv2.imwrite(str(path), frame, [int(cv2.IMWRITE_JPEG_QUALITY), 90])
    if not ok:
        raise RuntimeError(f"Failed to save image: {path}")

    if not path.exists():
        raise RuntimeError(f"Saved image missing after write: {path}")

    size = path.stat().st_size
    if size <= 1024:
        raise RuntimeError(f"Saved image is too small/empty: {path}, size={size}")

    verify_img = cv2.imread(str(path))
    if verify_img is None or verify_img.size == 0:
        raise RuntimeError(f"Saved image cannot be read back: {path}, size={size}")

    rel = f"garbage/{filename}" if garbage else filename
    return rel, path


def sanitize_filename_part(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_-]+", "-", str(value).strip())
    cleaned = re.sub(r"-{2,}", "-", cleaned).strip("-")
    return cleaned or "NA"


def build_person_video_name(event_time: dt.datetime) -> str:
    device_part = sanitize_filename_part(DEVICE_ID)
    suffix_part = sanitize_filename_part(PERSON_VIDEO_SUFFIX)
    return (
        f"{device_part}-Person_"
        f"{event_time.strftime('%Y%m%d_%H%M%S')}_"
        f"{suffix_part}.{PERSON_VIDEO_EXTENSION}"
    )


last_rolling_buffer_ts = 0.0


def add_frame_to_rolling_buffer(frame: np.ndarray, frame_ts: float) -> None:
    global last_rolling_buffer_ts

    min_gap = 1.0 / max(ROLLING_BUFFER_TARGET_FPS, 1.0)
    if (frame_ts - last_rolling_buffer_ts) < min_gap:
        return

    last_rolling_buffer_ts = frame_ts

    ok, jpg = cv2.imencode(
        ".jpg",
        frame,
        [int(cv2.IMWRITE_JPEG_QUALITY), EVENT_BUFFER_JPEG_QUALITY]
    )
    if not ok:
        return

    jpg_bytes = jpg.tobytes()

    with rolling_video_lock:
        rolling_video_buffer.append((frame_ts, jpg_bytes))

        cutoff_ts = frame_ts - EVENT_ROLLING_BUFFER_SEC
        while rolling_video_buffer and rolling_video_buffer[0][0] < cutoff_ts:
            rolling_video_buffer.popleft()


def get_buffered_clip_items(start_ts: float, end_ts: float) -> List[Tuple[float, bytes]]:
    with rolling_video_lock:
        return [
            (ts, jpg_bytes)
            for ts, jpg_bytes in rolling_video_buffer
            if start_ts <= ts <= end_ts
        ]


def estimate_clip_fps(items: List[Tuple[float, bytes]]) -> float:
    if len(items) < 2:
        return EVENT_DEFAULT_FPS

    duration = items[-1][0] - items[0][0]
    if duration <= 0:
        return EVENT_DEFAULT_FPS

    fps = len(items) / duration
    fps = max(EVENT_MIN_FPS, min(EVENT_MAX_FPS, fps))
    return float(fps)


def transcode_video_to_h264_mp4(input_path: Path, output_path: Path, fps: float) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        "ffmpeg",
        "-y",
        "-i", str(input_path),
        "-an",
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-preset", "veryfast",
        "-movflags", "+faststart",
        "-r", f"{fps:.3f}",
        str(output_path),
    ]

    proc = subprocess.run(
        cmd,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
        timeout=180
    )

    if proc.returncode != 0:
        raise RuntimeError(f"FFmpeg H.264 transcode failed: {proc.stderr.strip()[:800]}")

    if not output_path.exists() or output_path.stat().st_size <= 0:
        raise RuntimeError("H.264 output file missing or empty after transcode")


def write_buffered_event_video(
    output_path: Path,
    event_ts: float,
    pre_sec: int = EVENT_PRE_RECORD_SEC,
    post_sec: int = EVENT_POST_RECORD_SEC
) -> float:
    output_path.parent.mkdir(parents=True, exist_ok=True)

    clip_start_ts = event_ts - pre_sec
    clip_end_ts = event_ts + post_sec

    wait_sec = clip_end_ts - time.time()
    if wait_sec > 0:
        time.sleep(wait_sec)

    items = get_buffered_clip_items(clip_start_ts, clip_end_ts)
    if len(items) < 2:
        raise RuntimeError("Not enough buffered frames available for event clip")

    first_frame = cv2.imdecode(
        np.frombuffer(items[0][1], dtype=np.uint8),
        cv2.IMREAD_COLOR
    )
    if first_frame is None:
        raise RuntimeError("Failed to decode first buffered frame")

    height, width = first_frame.shape[:2]
    fps = estimate_clip_fps(items)

    temp_output_path = output_path.with_suffix(f".{EVENT_TEMP_VIDEO_EXTENSION}")
    if temp_output_path.exists():
        temp_output_path.unlink()

    if output_path.exists():
        output_path.unlink()

    fourcc = cv2.VideoWriter_fourcc(*"MJPG")
    writer = cv2.VideoWriter(str(temp_output_path), fourcc, fps, (width, height))
    if not writer.isOpened():
        raise RuntimeError(f"Failed to open temporary video writer for {temp_output_path}")

    written = 0
    try:
        for _, jpg_bytes in items:
            frame = cv2.imdecode(
                np.frombuffer(jpg_bytes, dtype=np.uint8),
                cv2.IMREAD_COLOR
            )
            if frame is None:
                continue

            if frame.shape[1] != width or frame.shape[0] != height:
                frame = cv2.resize(frame, (width, height))

            writer.write(frame)
            written += 1
    finally:
        writer.release()

    if written < 2 or not temp_output_path.exists() or temp_output_path.stat().st_size <= 0:
        if temp_output_path.exists():
            temp_output_path.unlink()
        raise RuntimeError("Temporary buffered event video write failed")

    try:
        transcode_video_to_h264_mp4(temp_output_path, output_path, fps)
    finally:
        if temp_output_path.exists():
            temp_output_path.unlink()

    if not output_path.exists() or output_path.stat().st_size <= 0:
        raise RuntimeError("Final H.264 MP4 file missing or empty")

    return round(max(0.0, items[-1][0] - items[0][0]), 2)


def async_upload_person_video(
    local_path: Path,
    remote_dir: str,
    remote_name: str,
    event_time: dt.datetime,
    person_count: int,
    best_confidence: float,
    image_file: str,
    image_path: str
) -> None:
    def worker():
        remote_path = f"{remote_dir.rstrip('/')}/{remote_name}"
        try:
            sftp_upload(local_path, remote_dir, remote_name)

            deleted_local = False
            delete_error = None
            try:
                if local_path.exists():
                    local_path.unlink()
                    deleted_local = True
            except Exception as exc:
                delete_error = str(exc)

            response_payload = {
                "date": event_time.strftime("%Y-%m-%d"),
                "time": event_time.strftime("%H:%M:%S"),
                "event": "person_detected",
                "device_id": DEVICE_ID,
                "image_file": image_file,
                "video_file": remote_name,
                "image_path": image_path,
                "video_path": remote_path,
                "camera_name": CAMERA_NAME,
                "duration_sec": PERSON_VIDEO_DURATION_SEC,
                "person_count": person_count,
                "video_status": "success",
                "video_message": "Video Uploaded Successfully",
                "best_confidence": best_confidence,
                "video_uploaded_at": dt.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
                **build_common_links_payload(),
                "local_deleted": deleted_local,
                "local_delete_error": delete_error,
            }

            update_state(lambda s: s["system"].update({
                "sftp_last_ok": now_str(),
                "sftp_last_error": None,
            }))
            update_state(lambda s: s.update({
                "last_video": {
                    "file": remote_name,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "status": "uploaded_deleted_local" if deleted_local else "uploaded_delete_failed",
                    "duration_sec": PERSON_VIDEO_DURATION_SEC,
                    "person_count": person_count,
                    "best_confidence": best_confidence,
                }
            }))
            add_event("person_video_uploaded", {
                "file": remote_name,
                "image_file": image_file,
                "image_path": image_path,
                "duration_sec": PERSON_VIDEO_DURATION_SEC,
                "person_count": person_count,
                "best_confidence": best_confidence,
                "local_deleted": deleted_local,
                "local_delete_error": delete_error,
            })

            time.sleep(1)
            mqtt_publish(MQTT_CAMERA_EVENT_TOPIC, response_payload)
            update_state(lambda s: s["system"].update({
                "mqtt_last_ok": now_str(),
                "mqtt_last_error": None,
            }))

        except Exception as exc:
            error_text = str(exc).strip() or repr(exc)

            update_state(lambda s: s["system"].update({
                "sftp_last_error": error_text,
            }))
            update_state(lambda s: s.update({
                "last_video": {
                    "file": remote_name,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "status": f"upload_failed: {error_text}",
                    "duration_sec": PERSON_VIDEO_DURATION_SEC,
                    "person_count": person_count,
                    "best_confidence": best_confidence,
                }
            }))

            try:
                time.sleep(1)
                mqtt_publish(MQTT_CAMERA_EVENT_TOPIC, {
                    "date": event_time.strftime("%Y-%m-%d"),
                    "time": event_time.strftime("%H:%M:%S"),
                    "event": "person_detected",
                    "device_id": DEVICE_ID,
                    "image_file": image_file,
                    "video_file": remote_name,
                    "image_path": image_path,
                    "video_path": f"{remote_dir.rstrip('/')}/{remote_name}",
                    "camera_name": CAMERA_NAME,
                    "duration_sec": PERSON_VIDEO_DURATION_SEC,
                    "person_count": person_count,
                    "video_status": "failed",
                    "video_message": f"Video upload failed: {error_text}",
                    "best_confidence": best_confidence,
                    "video_uploaded_at": dt.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
                    **build_common_links_payload(),
                })
                update_state(lambda s: s["system"].update({
                    "mqtt_last_ok": now_str(),
                    "mqtt_last_error": None,
                }))
            except Exception as mqtt_exc:
                update_state(lambda s: s["system"].update({
                    "mqtt_last_error": str(mqtt_exc),
                }))

    threading.Thread(target=worker, daemon=True).start()


VOLUME_COMMAND_PREFIX = "pi volume"
CURRENT_VOLUME_COMMAND = "current pi volume"

# Preferred playback controls in order
VOLUME_CONTROL_CANDIDATES = (
    "Speaker",
    "Master",
    "Headphone",
    "PCM",
    "Digital",
)

# Optional manual override. Set to an int like 0 if you want to force a card.
USB_AUDIO_CARD_INDEX: Optional[int] = None


def extract_volume_percent(command_text: str) -> Optional[int]:
    raw = str(command_text or "").strip()
    match = re.fullmatch(r"pi\s+volume\s+(\d{1,3})\s*%", raw, flags=re.IGNORECASE)
    if not match:
        return None

    value = int(match.group(1))
    if value < 0 or value > 100:
        return None

    return value


def extract_current_volume_percent(amixer_output: str) -> Optional[int]:
    """
    Parse amixer output like:
      Front Left: Playback 48 [75%] [-16.50dB] [on]
    and return 75
    """
    if not amixer_output:
        return None

    match = re.search(r"\[(\d{1,3})%\]", amixer_output)
    if not match:
        return None

    value = int(match.group(1))
    return max(0, min(100, value))


def run_amixer(args: List[str]) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["amixer", *args],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )


def list_alsa_cards() -> List[Tuple[int, str]]:
    """
    Parse `aplay -l` and return [(card_index, card_name), ...]
    Example line:
      card 0: Device [USB PnP Sound Device], device 0: USB Audio [USB Audio]
    """
    try:
        proc = subprocess.run(
            ["aplay", "-l"],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
    except Exception:
        return []

    cards: List[Tuple[int, str]] = []
    for line in proc.stdout.splitlines():
        match = re.search(r"card\s+(\d+):\s*([^\[]+)", line, flags=re.IGNORECASE)
        if match:
            card_index = int(match.group(1))
            card_name = match.group(2).strip()
            cards.append((card_index, card_name))
    return cards


def list_card_controls(card_index: int) -> List[str]:
    try:
        proc = run_amixer(["-c", str(card_index), "scontrols"])
    except Exception:
        return []

    controls: List[str] = []
    for line in proc.stdout.splitlines():
        match = re.search(r"Simple mixer control '([^']+)'", line)
        if match:
            controls.append(match.group(1))
    return controls


def detect_volume_card_index() -> int:
    """
    Find the most likely playback card.
    Priority:
    1. Explicit override
    2. USB audio card
    3. First card exposing a known playback control
    4. Fallback to card 0
    """
    if USB_AUDIO_CARD_INDEX is not None:
        return int(USB_AUDIO_CARD_INDEX)

    cards = list_alsa_cards()
    if not cards:
        return 0

    # Prefer USB card first
    for card_index, card_name in cards:
        name = card_name.lower()
        if "usb" in name or "sound device" in name or "audio" in name:
            controls = list_card_controls(card_index)
            if any(control in controls for control in VOLUME_CONTROL_CANDIDATES):
                return card_index

    # Otherwise return first card having a usable playback control
    for card_index, _card_name in cards:
        controls = list_card_controls(card_index)
        if any(control in controls for control in VOLUME_CONTROL_CANDIDATES):
            return card_index

    return 0


def detect_volume_control(card_index: int) -> Tuple[str, List[str]]:
    controls = list_card_controls(card_index)
    if not controls:
        raise RuntimeError(f"No mixer controls found on ALSA card {card_index}")

    for control_name in VOLUME_CONTROL_CANDIDATES:
        if control_name in controls:
            return control_name, controls

    raise RuntimeError(
        f"No supported playback control found on ALSA card {card_index}. "
        f"Available controls: {', '.join(controls)}"
    )


def get_current_pi_volume() -> Dict[str, Any]:
    card_index = detect_volume_card_index()
    control_name, available_controls = detect_volume_control(card_index)

    proc = run_amixer(["-c", str(card_index), "get", control_name])
    volume_percent = extract_current_volume_percent(proc.stdout)

    if volume_percent is None:
        raise RuntimeError(
            f"Unable to parse current volume from ALSA card {card_index}, control '{control_name}'"
        )

    return {
        "volume_percent": volume_percent,
        "card_index": card_index,
        "control": control_name,
        "checked_controls": list(VOLUME_CONTROL_CANDIDATES),
        "available_controls": available_controls,
        "failed_controls": [c for c in VOLUME_CONTROL_CANDIDATES if c != control_name]
    }


def set_pi_volume(percent: int) -> Dict[str, Any]:
    percent = max(0, min(100, int(percent)))

    card_index = detect_volume_card_index()
    control_name, available_controls = detect_volume_control(card_index)

    run_amixer(["-c", str(card_index), "set", control_name, f"{percent}%"])

    verify_proc = run_amixer(["-c", str(card_index), "get", control_name])
    verified_percent = extract_current_volume_percent(verify_proc.stdout)
    if verified_percent is None:
        verified_percent = percent

    return {
        "volume_percent": verified_percent,
        "requested_volume_percent": percent,
        "card_index": card_index,
        "updated_controls": [control_name],
        "control": control_name,
        "available_controls": available_controls,
        "failed_controls": [c for c in VOLUME_CONTROL_CANDIDATES if c != control_name]
    }


def handle_pi_volume_command(payload: Dict[str, Any]) -> None:
    command_text = str(payload.get("command", "")).strip()
    volume_percent = extract_volume_percent(command_text)

    if volume_percent is None:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "pi_volume_set_failed",
            "message": "Command format: Pi Volume 10% to Pi Volume 100%"
        })
        return

    try:
        result = set_pi_volume(volume_percent)

        update_state(lambda s: s.update({
            "last_audio": {
                "file": None,
                "source_path": None,
                "time": now_str(),
                "status": f"pi_volume_set_to_{result['volume_percent']}%"
            }
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "pi_volume_set",
            "volume_percent": result["volume_percent"],
            "requested_volume_percent": result["requested_volume_percent"],
            "card_index": result["card_index"],
            "control": result["control"],
            "updated_controls": result["updated_controls"],
            "available_controls": result["available_controls"],
            "failed_controls": result["failed_controls"],
            "message": (
                f"Pi volume set to {result['volume_percent']}% "
                f"using ALSA card {result['card_index']} control '{result['control']}'"
            )
        })

        add_event("pi_volume_set", {
            "volume_percent": result["volume_percent"],
            "updated_controls": result["updated_controls"],
            "failed_controls": result["failed_controls"]
        })

    except Exception as exc:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "pi_volume_set_failed",
            "volume_percent": volume_percent,
            "message": str(exc)
        })

        add_event("pi_volume_set_failed", {
            "volume_percent": volume_percent,
            "message": str(exc)
        })

def handle_current_pi_volume_command(payload: Dict[str, Any]) -> None:
    try:
        result = get_current_pi_volume()

        update_state(lambda s: s.update({
            "last_audio": {
                "file": None,
                "source_path": None,
                "time": now_str(),
                "status": f"current_pi_volume_{result['volume_percent']}%"
            }
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "current_pi_volume",
            "volume_percent": result["volume_percent"],
            "card_index": result["card_index"],
            "control": result["control"],
            "checked_controls": result["checked_controls"],
            "available_controls": result["available_controls"],
            "failed_controls": result["failed_controls"],
            "message": (
                f"Current Pi volume is {result['volume_percent']}% "
                f"on ALSA card {result['card_index']} control '{result['control']}'"
            )
        })

        add_event("current_pi_volume", {
            "volume_percent": result["volume_percent"],
            "control": result["control"],
            "checked_controls": result["checked_controls"],
            "failed_controls": result["failed_controls"]
        })

    except Exception as exc:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "current_pi_volume_failed",
            "message": str(exc)
        })

        add_event("current_pi_volume_failed", {
            "message": str(exc)
        })


def handle_person_video_event(
    event_time: dt.datetime,
    event_ts: float,
    person_count: int,
    best_confidence: float,
    image_file: str,
    image_path: str
) -> None:
    video_name = build_person_video_name(event_time)
    local_video_path = VIDEO_DIR / video_name

    update_state(lambda s: s.update({
        "last_video": {
            "file": video_name,
            "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
            "status": "buffering_post_event",
            "duration_sec": PERSON_VIDEO_DURATION_SEC,
            "person_count": person_count,
            "best_confidence": best_confidence,
        }
    }))

    try:
        actual_duration_sec = write_buffered_event_video(
            output_path=local_video_path,
            event_ts=event_ts,
            pre_sec=EVENT_PRE_RECORD_SEC,
            post_sec=EVENT_POST_RECORD_SEC,
        )

        update_state(lambda s: s.update({
            "last_video": {
                "file": video_name,
                "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                "status": "recorded",
                "duration_sec": actual_duration_sec,
                "person_count": person_count,
                "best_confidence": best_confidence,
            }
        }))

        async_upload_person_video(
            local_path=local_video_path,
            remote_dir=SFTP_PERSON_VIDEO_DIR,
            remote_name=video_name,
            event_time=event_time,
            person_count=person_count,
            best_confidence=best_confidence,
            image_file=image_file,
            image_path=image_path,
        )

    except Exception as video_exc:
        error_text = str(video_exc).strip() or repr(video_exc)

        update_state(lambda s: s.update({
            "last_video": {
                "file": video_name,
                "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                "status": f"record_failed: {error_text}",
                "duration_sec": PERSON_VIDEO_DURATION_SEC,
                "person_count": person_count,
                "best_confidence": best_confidence,
            }
        }))

        try:
            mqtt_publish(MQTT_CAMERA_EVENT_TOPIC, {
                "date": event_time.strftime("%Y-%m-%d"),
                "time": event_time.strftime("%H:%M:%S"),
                "event": "person_detected",
                "device_id": DEVICE_ID,
                "image_file": image_file,
                "video_file": video_name,
                "image_path": image_path,
                "video_path": f"{SFTP_PERSON_VIDEO_DIR.rstrip('/')}/{video_name}",
                "camera_name": CAMERA_NAME,
                "duration_sec": PERSON_VIDEO_DURATION_SEC,
                "person_count": person_count,
                "video_status": "failed",
                "video_message": f"Video record failed: {error_text}",
                "best_confidence": best_confidence,
                "video_uploaded_at": dt.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
                **build_common_links_payload(),
            })
        except Exception as mqtt_exc:
            update_state(lambda s: s["system"].update({
                "mqtt_last_error": str(mqtt_exc),
            }))

def open_rtsp() -> cv2.VideoCapture:
    cap = cv2.VideoCapture(RTSP_URL, cv2.CAP_FFMPEG)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    return cap


def open_cloudflare_live_rtsp() -> cv2.VideoCapture:
    cap = cv2.VideoCapture(CLOUDFLARE_LIVE_RTSP_URL, cv2.CAP_FFMPEG)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    return cap


def mjpeg_stream_from_rtsp(rtsp_url: str):
    cap: Optional[cv2.VideoCapture] = None

    try:
        while not stop_event.is_set():
            if is_power_saving_active():
                if cap is not None:
                    try:
                        cap.release()
                    except Exception:
                        pass
                    cap = None
                time.sleep(1)
                continue

            if cap is None or not cap.isOpened():
                cap = cv2.VideoCapture(rtsp_url, cv2.CAP_FFMPEG)
                cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

                if not cap.isOpened():
                    time.sleep(1.5)
                    continue

            ok, frame = cap.read()
            if not ok or frame is None:
                try:
                    cap.release()
                except Exception:
                    pass
                cap = None
                time.sleep(0.5)
                continue

            h, w = frame.shape[:2]
            if w > FRAME_WIDTH:
                new_h = int(h * (FRAME_WIDTH / float(w)))
                frame = cv2.resize(frame, (FRAME_WIDTH, new_h))

            ok, jpg = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), MJPEG_QUALITY])
            if ok:
                yield (
                    b"--frame\r\n"
                    b"Content-Type: image/jpeg\r\n\r\n" + jpg.tobytes() + b"\r\n"
                )

            time.sleep(0.06)
    finally:
        if cap is not None:
            try:
                cap.release()
            except Exception:
                pass


def enhance_for_person_detection(frame: np.ndarray) -> np.ndarray:
    lab = cv2.cvtColor(frame, cv2.COLOR_BGR2LAB)
    l, a, b = cv2.split(lab)
    clahe = cv2.createCLAHE(clipLimit=2.2, tileGridSize=(8, 8))
    l2 = clahe.apply(l)
    out = cv2.merge((l2, a, b))
    return cv2.cvtColor(out, cv2.COLOR_LAB2BGR)


def detect_persons_single_pass(frame: np.ndarray, scale_tag: str, min_conf: float) -> List[Dict[str, Any]]:
    if person_net is None:
        return []

    h, w = frame.shape[:2]
    inp = cv2.resize(frame, (300, 300))
    blob = cv2.dnn.blobFromImage(inp, 0.007843, (300, 300), 127.5)

    with person_dnn_lock:
        person_net.setInput(blob)
        detections = person_net.forward()

    results: List[Dict[str, Any]] = []
    for i in range(detections.shape[2]):
        conf = float(detections[0, 0, i, 2])
        if conf < max(min_conf, PERSON_CONF):
            continue

        cls_idx = int(detections[0, 0, i, 1])
        if cls_idx < 0 or cls_idx >= len(MOBILENET_CLASSES):
            continue
        if MOBILENET_CLASSES[cls_idx] != "person":
            continue

        box = detections[0, 0, i, 3:7] * np.array([w, h, w, h])
        x1, y1, x2, y2 = box.astype("int")
        x1 = max(0, x1)
        y1 = max(0, y1)
        x2 = min(w - 1, x2)
        y2 = min(h - 1, y2)

        if x2 <= x1 or y2 <= y1:
            continue

        area = (x2 - x1) * (y2 - y1)
        if area < PERSON_MIN_AREA:
            continue

        bw = x2 - x1
        bh = y2 - y1
        aspect_ratio = bh / max(bw, 1)
        if aspect_ratio < PERSON_MIN_ASPECT_RATIO:
            continue

        results.append({
            "label": "person",
            "confidence": conf,
            "box": (x1, y1, x2, y2),
            "scale_tag": scale_tag
        })

    return results


def upscale_and_detect(frame: np.ndarray, factor: float, min_conf: float, scale_tag: str = "upscaled") -> List[Dict[str, Any]]:
    h, w = frame.shape[:2]
    up = cv2.resize(frame, (int(w * factor), int(h * factor)), interpolation=cv2.INTER_CUBIC)
    raw = detect_persons_single_pass(up, scale_tag, min_conf)

    mapped: List[Dict[str, Any]] = []
    for d in raw:
        x1, y1, x2, y2 = d["box"]
        x1 = int(x1 / factor)
        y1 = int(y1 / factor)
        x2 = int(x2 / factor)
        y2 = int(y2 / factor)
        x1 = max(0, min(w - 1, x1))
        y1 = max(0, min(h - 1, y1))
        x2 = max(0, min(w - 1, x2))
        y2 = max(0, min(h - 1, y2))
        if x2 <= x1 or y2 <= y1:
            continue
        mapped.append({
            "label": "person",
            "confidence": d["confidence"],
            "box": (x1, y1, x2, y2),
            "scale_tag": scale_tag
        })
    return mapped


def detect_persons_tiled(frame: np.ndarray) -> List[Dict[str, Any]]:
    h, w = frame.shape[:2]
    tile_w = max(320, min(420, int(w * 0.42)))
    tile_h = max(240, min(340, int(h * 0.48)))
    step_x = max(90, int(tile_w * PERSON_TILE_STEP_RATIO))
    step_y = max(70, int(tile_h * PERSON_TILE_STEP_RATIO))

    detections: List[Dict[str, Any]] = []
    y_limit = max(tile_h, int(h * 0.82))

    for y in range(0, max(1, y_limit - tile_h + 1), step_y):
        for x in range(0, max(1, w - tile_w + 1), step_x):
            tile = frame[y:y + tile_h, x:x + tile_w]
            if tile.size == 0:
                continue

            tile_up = cv2.resize(
                tile,
                (int(tile.shape[1] * 1.35), int(tile.shape[0] * 1.35)),
                interpolation=cv2.INTER_CUBIC
            )
            tile_dets = detect_persons_single_pass(tile_up, "tile", PERSON_TILE_MIN_CONF)

            for d in tile_dets:
                x1, y1, x2, y2 = d["box"]
                x1 = int(x + (x1 / 1.35))
                y1 = int(y + (y1 / 1.35))
                x2 = int(x + (x2 / 1.35))
                y2 = int(y + (y2 / 1.35))
                x1 = max(0, min(w - 1, x1))
                y1 = max(0, min(h - 1, y1))
                x2 = max(0, min(w - 1, x2))
                y2 = max(0, min(h - 1, y2))
                if x2 <= x1 or y2 <= y1:
                    continue
                if (x2 - x1) * (y2 - y1) < PERSON_MIN_AREA:
                    continue
                detections.append({
                    "label": "person",
                    "confidence": float(d["confidence"]),
                    "box": (x1, y1, x2, y2),
                    "scale_tag": "tile"
                })

    return detections


def detect_persons_hog(frame: np.ndarray) -> List[Dict[str, Any]]:
    if not PERSON_HOG_ENABLE or hog_detector is None:
        return []

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    gray = cv2.equalizeHist(gray)

    rects, weights = hog_detector.detectMultiScale(
        gray,
        winStride=(4, 4),
        padding=(8, 8),
        scale=1.03
    )

    out: List[Dict[str, Any]] = []
    for (x, y, w, h), score in zip(rects, weights):
        if w * h < 500:
            continue
        out.append({
            "label": "person",
            "confidence": float(min(0.90, 0.30 + float(score) / 5.0)),
            "box": (int(x), int(y), int(x + w), int(y + h)),
            "scale_tag": "hog"
        })
    return out


def nms_merge_persons(candidates: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    if not candidates:
        return []

    boxes = []
    scores = []
    for d in candidates:
        x1, y1, x2, y2 = d["box"]
        boxes.append([x1, y1, max(1, x2 - x1), max(1, y2 - y1)])
        scores.append(float(d["confidence"]))

    kept = cv2.dnn.NMSBoxes(boxes, scores, 0.22, PERSON_NMS_THRESHOLD)
    if kept is None or len(kept) == 0:
        return []

    out: List[Dict[str, Any]] = []
    for idx in kept:
        i = int(idx[0] if isinstance(idx, (list, tuple, np.ndarray)) else idx)
        out.append(candidates[i])

    out.sort(key=lambda item: item["confidence"], reverse=True)
    return out


def _crop_box_with_padding(
    frame: np.ndarray,
    box: Tuple[int, int, int, int],
    pad_ratio: float = 0.03
) -> np.ndarray:
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box
    bw = max(1, x2 - x1)
    bh = max(1, y2 - y1)
    pad_x = int(bw * pad_ratio)
    pad_y = int(bh * pad_ratio)

    cx1 = max(0, x1 - pad_x)
    cy1 = max(0, y1 - pad_y)
    cx2 = min(w - 1, x2 + pad_x)
    cy2 = min(h - 1, y2 + pad_y)

    if cx2 <= cx1 or cy2 <= cy1:
        return frame[0:0, 0:0]

    return frame[cy1:cy2, cx1:cx2]


def person_patch_allowed(
    frame: np.ndarray,
    box: Tuple[int, int, int, int],
    confidence: float
) -> bool:
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box
    bw = x2 - x1
    bh = y2 - y1
    aspect_ratio = bh / max(bw, 1)

    patch = _crop_box_with_padding(frame, box)
    if patch is None or patch.size == 0:
        return False

    gray = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY)
    hsv = cv2.cvtColor(patch, cv2.COLOR_BGR2HSV)

    variance = float(gray.var())
    saturation_mean = float(hsv[:, :, 1].mean())

    edges = cv2.Canny(gray, 50, 140)
    edge_density = float(np.count_nonzero(edges)) / float(max(edges.size, 1))

    # Reject plain vertical stone / plain pole / plain wall-like object.
    if variance < PERSON_MIN_TEXTURE_VARIANCE and saturation_mean < 22.0 and confidence < 0.88:
        return False

    # Reject very thin high-aspect pole/tree/barricade shapes.
    if aspect_ratio >= PERSON_POLE_ASPECT_RATIO and (bw / max(w, 1)) <= PERSON_POLE_MAX_WIDTH_RATIO:
        if confidence < 0.90:
            return False
        if edge_density < 0.055:
            return False

    # Too few edges usually means pole/plain object.
    if edge_density < PERSON_MIN_EDGE_DENSITY and confidence < 0.90:
        return False

    # Too many edges usually means tree/bush/fence clutter.
    if edge_density > PERSON_MAX_EDGE_DENSITY and confidence < 0.88:
        return False

    return True


def person_box_allowed(frame: np.ndarray, box: Tuple[int, int, int, int], confidence: float) -> bool:
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box

    bw = x2 - x1
    bh = y2 - y1
    if bw <= 0 or bh <= 0:
        return False

    area = bw * bh
    aspect_ratio = bh / max(bw, 1)

    if confidence < PERSON_CONF:
        return False

    if area < PERSON_MIN_AREA:
        return False

    if bw < int(w * PERSON_MIN_BOX_WIDTH_RATIO):
        return False

    if aspect_ratio < PERSON_MIN_ASPECT_RATIO:
        return False

    if aspect_ratio > PERSON_MAX_ASPECT_RATIO:
        return False

    if bh < int(h * PERSON_MIN_BOX_HEIGHT_RATIO):
        return False

    if (bw / max(w, 1)) > PERSON_MAX_BOX_WIDTH_RATIO:
        return False

    if aspect_ratio < 1.45 and confidence < 0.78:
        return False

    # Polygon geofence check.
    # Foot point is better than center point for person detection because
    # it confirms that the person is standing inside the geofence.
    foot_x = int((x1 + x2) / 2)
    foot_y = int(y2)

    if not point_inside_person_polygon(frame, foot_x, foot_y):
        return False

    person_exclusion_zones = load_ratio_boxes_from_config(
        "person_exclusion_zones",
        PERSON_EXCLUSION_ZONES
    )
    if point_inside_any_ratio_box(foot_x, foot_y, frame, person_exclusion_zones):
        return False

    if not person_patch_allowed(frame, box, confidence):
        return False

    return True


last_person_full_scan_ts = 0.0


def detect_persons(frame: np.ndarray) -> List[Dict[str, Any]]:
    global last_person_full_scan_ts

    enhanced = enhance_for_person_detection(frame)

    candidates: List[Dict[str, Any]] = []

    candidates.extend(
        detect_persons_single_pass(
            enhanced,
            "normal",
            PERSON_CONF
        )
    )

    now_ts = time.time()
    run_full_scan = (now_ts - last_person_full_scan_ts) >= PERSON_FULL_SCAN_INTERVAL_SEC

    if run_full_scan:
        last_person_full_scan_ts = now_ts

        candidates.extend(
            detect_persons_tiled(enhanced)
        )

        if len(candidates) == 0:
            candidates.extend(
                upscale_and_detect(
                    enhanced,
                    PERSON_UPSCALE_FACTOR,
                    max(0.64, PERSON_CONF - 0.04),
                    "fallback_up"
                )
            )

    merged = nms_merge_persons(candidates)

    filtered: List[Dict[str, Any]] = []

    for d in merged:
        conf = float(d["confidence"])
        if not person_box_allowed(frame, d["box"], conf):
            continue
        filtered.append(d)

    filtered.sort(key=lambda item: item["confidence"], reverse=True)
    return filtered


def draw_person_annotations(frame: np.ndarray, persons: List[Dict[str, Any]]) -> np.ndarray:
    out = frame.copy()
    for idx, d in enumerate(persons, start=1):
        x1, y1, x2, y2 = d["box"]
        label = f'Person {idx} {d["confidence"]:.2f}'
        cv2.rectangle(out, (x1, y1), (x2, y2), (0, 255, 0), 2)
        cv2.putText(out, label, (x1, max(25, y1 - 8)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 0), 2, cv2.LINE_AA)
    return out


def nms_merge_boxes(
    detections: List[Dict[str, Any]],
    conf_threshold: float,
    nms_threshold: float
) -> List[Dict[str, Any]]:
    if not detections:
        return []

    boxes: List[List[int]] = []
    scores: List[float] = []

    for d in detections:
        x1, y1, x2, y2 = d["box"]
        boxes.append([x1, y1, max(1, x2 - x1), max(1, y2 - y1)])
        scores.append(float(d["confidence"]))

    kept = cv2.dnn.NMSBoxes(boxes, scores, conf_threshold, nms_threshold)
    if kept is None or len(kept) == 0:
        return []

    out: List[Dict[str, Any]] = []
    for idx in kept:
        i = int(idx[0] if isinstance(idx, (list, tuple, np.ndarray)) else idx)
        out.append(detections[i])

    out.sort(key=lambda item: item["confidence"], reverse=True)
    return out


def create_motion_mask(
    previous_frame: Optional[np.ndarray],
    current_frame: np.ndarray
) -> Optional[np.ndarray]:
    if previous_frame is None or current_frame is None or current_frame.size == 0:
        return None

    if previous_frame.shape[:2] != current_frame.shape[:2]:
        previous_frame = cv2.resize(
            previous_frame,
            (current_frame.shape[1], current_frame.shape[0]),
            interpolation=cv2.INTER_LINEAR
        )

    prev_gray = cv2.cvtColor(previous_frame, cv2.COLOR_BGR2GRAY)
    curr_gray = cv2.cvtColor(current_frame, cv2.COLOR_BGR2GRAY)

    prev_gray = cv2.GaussianBlur(prev_gray, (5, 5), 0)
    curr_gray = cv2.GaussianBlur(curr_gray, (5, 5), 0)

    diff = cv2.absdiff(prev_gray, curr_gray)
    _, mask = cv2.threshold(diff, VEHICLE_MOTION_THRESHOLD, 255, cv2.THRESH_BINARY)

    kernel3 = np.ones((3, 3), np.uint8)
    kernel5 = np.ones((5, 5), np.uint8)

    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel3, iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel5, iterations=1)
    mask = cv2.dilate(mask, kernel3, iterations=VEHICLE_MOTION_DILATE_ITER)

    return mask


def box_motion_ratio(
    motion_mask: Optional[np.ndarray],
    box: Tuple[int, int, int, int]
) -> Tuple[float, int]:
    if motion_mask is None or motion_mask.size == 0:
        return 0.0, 0

    mh, mw = motion_mask.shape[:2]
    x1, y1, x2, y2 = box

    x1 = max(0, min(mw - 1, x1))
    y1 = max(0, min(mh - 1, y1))
    x2 = max(0, min(mw - 1, x2))
    y2 = max(0, min(mh - 1, y2))

    if x2 <= x1 or y2 <= y1:
        return 0.0, 0

    crop = motion_mask[y1:y2, x1:x2]
    active_pixels = int(np.count_nonzero(crop))
    ratio = float(active_pixels) / float(max(crop.size, 1))
    return ratio, active_pixels


def vehicle_motion_allowed(
    motion_mask: Optional[np.ndarray],
    box: Tuple[int, int, int, int]
) -> bool:
    motion_ratio, active_pixels = box_motion_ratio(motion_mask, box)

    if active_pixels < VEHICLE_MOTION_MIN_PIXELS:
        return False

    if motion_ratio < VEHICLE_MOTION_MIN_RATIO:
        return False

    return True


def vehicle_box_allowed(
    frame: np.ndarray,
    box: Tuple[int, int, int, int],
    confidence: float,
    motion_mask: Optional[np.ndarray],
    detection_mode: str
) -> Tuple[bool, float]:
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box

    bw = x2 - x1
    bh = y2 - y1
    if bw <= 0 or bh <= 0:
        return False, 0.0

    if confidence < VEHICLE_CONF:
        return False, 0.0

    area = bw * bh
    if area < VEHICLE_MIN_AREA:
        return False, 0.0

    if bw < int(w * VEHICLE_MIN_BOX_WIDTH_RATIO):
        return False, 0.0

    if bh < int(h * VEHICLE_MIN_BOX_HEIGHT_RATIO):
        return False, 0.0

    if bw > int(w * VEHICLE_MAX_BOX_WIDTH_RATIO):
        return False, 0.0

    if bh > int(h * VEHICLE_MAX_BOX_HEIGHT_RATIO):
        return False, 0.0

    cx = int((x1 + x2) / 2)
    cy = int((y1 + y2) / 2)

    vehicle_exclusion_zones = load_ratio_boxes_from_config(
        "vehicle_exclusion_zones",
        VEHICLE_EXCLUSION_ZONES
    )
    if point_inside_any_ratio_box(cx, cy, frame, vehicle_exclusion_zones):
        return False, 0.0

    inside_geofence, geofence_overlap = vehicle_box_inside_geofence(frame, box)
    if not inside_geofence:
        return False, geofence_overlap

    mode = normalize_vehicle_detection_mode(detection_mode)
    if mode == "moving" and not vehicle_motion_allowed(motion_mask, box):
        return False, geofence_overlap

    return True, geofence_overlap


def detect_vehicles(
    frame: np.ndarray,
    motion_mask: Optional[np.ndarray] = None,
    detection_mode: Optional[str] = None
) -> List[Dict[str, Any]]:
    if person_net is None:
        return []

    mode = normalize_vehicle_detection_mode(detection_mode or load_vehicle_detection_mode())

    h, w = frame.shape[:2]
    blob = cv2.dnn.blobFromImage(
        cv2.resize(frame, (300, 300)),
        scalefactor=0.007843,
        size=(300, 300),
        mean=127.5
    )

    with person_dnn_lock:
        person_net.setInput(blob)
        detections = person_net.forward()

    candidates: List[Dict[str, Any]] = []

    for i in range(detections.shape[2]):
        confidence = float(detections[0, 0, i, 2])
        if confidence < max(0.45, VEHICLE_CONF - 0.10):
            continue

        class_index = int(detections[0, 0, i, 1])
        if class_index < 0 or class_index >= len(MOBILENET_CLASSES):
            continue

        label = MOBILENET_CLASSES[class_index]
        if label not in VEHICLE_CLASSES:
            continue

        box = detections[0, 0, i, 3:7] * np.array([w, h, w, h])
        x1, y1, x2, y2 = box.astype("int").tolist()

        x1 = max(0, min(w - 1, x1))
        y1 = max(0, min(h - 1, y1))
        x2 = max(0, min(w - 1, x2))
        y2 = max(0, min(h - 1, y2))

        if x2 <= x1 or y2 <= y1:
            continue

        allowed, geofence_overlap = vehicle_box_allowed(
            frame,
            (x1, y1, x2, y2),
            confidence,
            motion_mask,
            mode
        )
        if not allowed:
            continue

        motion_ratio, motion_pixels = box_motion_ratio(motion_mask, (x1, y1, x2, y2))

        candidates.append({
            "label": label,
            "display_label": VEHICLE_CLASS_LABELS.get(label, label.title()),
            "confidence": round(confidence, 4),
            "box": (x1, y1, x2, y2),
            "motion_ratio": round(motion_ratio, 4),
            "motion_pixels": motion_pixels,
            "mode": mode,
            "geofence_overlap": round(geofence_overlap, 4),
        })

    merged = nms_merge_boxes(candidates, VEHICLE_CONF, VEHICLE_NMS_THRESHOLD)
    merged.sort(key=lambda item: item["confidence"], reverse=True)
    return merged


def draw_vehicle_annotations(frame: np.ndarray, vehicles: List[Dict[str, Any]]) -> np.ndarray:
    out = frame.copy()
    vehicle_polygon = get_vehicle_polygon_pixels(out)
    draw_polygon_outline(out, vehicle_polygon, "Vehicle Polygon Zone", (255, 200, 0), 0.0)

    for idx, d in enumerate(vehicles, start=1):
        x1, y1, x2, y2 = d["box"]
        mode = str(d.get("mode", load_vehicle_detection_mode())).upper()
        overlap_percent = float(d.get("geofence_overlap", 0.0)) * 100.0
        label = f'{d["display_label"]} {idx} {d["confidence"]:.2f} {mode} G:{overlap_percent:.0f}%'

        cv2.rectangle(out, (x1, y1), (x2, y2), (255, 200, 0), 2)
        cv2.putText(
            out,
            label,
            (x1, max(25, y1 - 8)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.55,
            (255, 200, 0),
            2,
            cv2.LINE_AA
        )

    return out


def build_vehicle_video_name(event_time: dt.datetime) -> str:
    device_part = sanitize_filename_part(DEVICE_ID)
    suffix_part = sanitize_filename_part(VEHICLE_VIDEO_SUFFIX)
    return (
        f"{device_part}-Vehicle_"
        f"{event_time.strftime('%Y%m%d_%H%M%S')}_"
        f"{suffix_part}.{VEHICLE_VIDEO_EXTENSION}"
    )


def async_upload_vehicle_video(
    local_path: Path,
    remote_dir: str,
    remote_name: str,
    event_time: dt.datetime,
    vehicle_count: int,
    best_confidence: float,
    vehicle_labels: List[str]
) -> None:
    def worker():
        remote_path = f"{remote_dir.rstrip('/')}/{remote_name}"
        try:
            sftp_upload(local_path, remote_dir, remote_name)

            deleted_local = False
            delete_error = None
            try:
                if local_path.exists():
                    local_path.unlink()
                    deleted_local = True
            except Exception as exc:
                delete_error = str(exc)

            update_state(lambda s: s["system"].update({
                "sftp_last_ok": now_str(),
                "sftp_last_error": None,
            }))

            update_state(lambda s: s.update({
                "last_vehicle_video": {
                    "file": remote_name,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "status": "uploaded_deleted_local" if deleted_local else "uploaded_delete_failed",
                    "duration_sec": VEHICLE_VIDEO_DURATION_SEC,
                    "vehicle_count": vehicle_count,
                    "best_confidence": best_confidence,
                    "labels": vehicle_labels,
                }
            }))

            update_vehicle_event_result(
                build_vehicle_event_key(event_time),
                video_done=True,
                video_ok=True,
                video_file=remote_name,
                video_path=remote_path,
                video_status="success",
                video_message="Vehicle video uploaded successfully",
                duration_sec=VEHICLE_VIDEO_DURATION_SEC,
            )
            maybe_publish_vehicle_combined_response(build_vehicle_event_key(event_time))

            add_event("vehicle_video_uploaded", {
                "file": remote_name,
                "video_path": remote_path,
                "duration_sec": VEHICLE_VIDEO_DURATION_SEC,
                "vehicle_count": vehicle_count,
                "vehicle_labels": vehicle_labels,
                "best_confidence": best_confidence,
                "local_deleted": deleted_local,
                "local_delete_error": delete_error,
            })

        except Exception as exc:
            error_text = str(exc).strip() or repr(exc)

            update_state(lambda s: s["system"].update({
                "sftp_last_error": error_text,
            }))
            update_state(lambda s: s.update({
                "last_vehicle_video": {
                    "file": remote_name,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "status": f"upload_failed: {error_text}",
                    "duration_sec": VEHICLE_VIDEO_DURATION_SEC,
                    "vehicle_count": vehicle_count,
                    "best_confidence": best_confidence,
                    "labels": vehicle_labels,
                }
            }))

            update_vehicle_event_result(
                build_vehicle_event_key(event_time),
                video_done=True,
                video_ok=False,
                video_file=remote_name,
                video_path=remote_path,
                video_status="failed",
                video_message=f"Vehicle video upload failed: {error_text}",
                duration_sec=VEHICLE_VIDEO_DURATION_SEC,
            )
            maybe_publish_vehicle_combined_response(build_vehicle_event_key(event_time))

    threading.Thread(target=worker, daemon=True).start()


def build_vehicle_event_key(event_time: dt.datetime) -> str:
    return event_time.strftime("%Y%m%d_%H%M%S")


def init_vehicle_event_result(
    event_time: dt.datetime,
    vehicle_count: int,
    best_confidence: float,
    vehicle_labels: List[str]
) -> str:
    event_key = build_vehicle_event_key(event_time)
    with vehicle_event_lock:
        vehicle_event_results[event_key] = {
            "event_time": event_time,
            "vehicle_count": vehicle_count,
            "best_confidence": best_confidence,
            "vehicle_labels": list(vehicle_labels),

            "image_done": False,
            "image_ok": False,
            "image_file": None,
            "image_path": None,
            "image_status": None,
            "image_message": None,

            "video_done": False,
            "video_ok": False,
            "video_file": None,
            "video_path": None,
            "video_status": None,
            "video_message": None,
            "duration_sec": VEHICLE_VIDEO_DURATION_SEC,

            "published": False,
        }
    return event_key


def update_vehicle_event_result(event_key: str, **kwargs: Any) -> Optional[Dict[str, Any]]:
    with vehicle_event_lock:
        item = vehicle_event_results.get(event_key)
        if not item:
            return None
        item.update(kwargs)
        return dict(item)


def maybe_publish_vehicle_combined_response(event_key: str) -> None:
    with vehicle_event_lock:
        item = vehicle_event_results.get(event_key)
        if not item:
            return

        if item.get("published"):
            return

        if not (item.get("image_done") and item.get("video_done")):
            return

        event_time: dt.datetime = item["event_time"]
        image_path = item.get("image_path")
        video_path = item.get("video_path")

        payload = {
            "date": event_time.strftime("%Y-%m-%d"),
            "time": event_time.strftime("%H:%M:%S"),
            "event": "vehicle_detected",
            "device_id": DEVICE_ID,
            "camera_name": CAMERA_NAME,

            "image_file": item.get("image_file"),
            "image_path": image_path,
            "image_status": item.get("image_status") or ("success" if item.get("image_ok") else "failed"),
            "image_message": item.get("image_message"),

            "video_file": item.get("video_file"),
            "video_path": video_path,
            "video_status": item.get("video_status") or ("success" if item.get("video_ok") else "failed"),
            "video_message": item.get("video_message"),

            "dir": image_path,
            "video_dir": video_path,

            "vehicle_count": item.get("vehicle_count", 0),
            "vehicle_labels": item.get("vehicle_labels", []),
            "best_confidence": item.get("best_confidence", 0.0),
            "duration_sec": item.get("duration_sec", VEHICLE_VIDEO_DURATION_SEC),
            **build_common_links_payload(),
        }

        item["published"] = True

    try:
        mqtt_publish(MQTT_RESPONSE_TOPIC, payload)
        update_state(lambda s: s["system"].update({
            "mqtt_last_ok": now_str(),
            "mqtt_last_error": None,
        }))
    except Exception as mqtt_exc:
        update_state(lambda s: s["system"].update({
            "mqtt_last_error": str(mqtt_exc),
        }))
    finally:
        with vehicle_event_lock:
            vehicle_event_results.pop(event_key, None)


def async_upload_vehicle_image(
    local_path: Path,
    remote_dir: str,
    remote_name: str,
    event_time: dt.datetime,
    vehicle_count: int,
    best_confidence: float,
    vehicle_labels: List[str],
    rel_img: str,
    vehicle_mode: str,
    geofence_overlap: float
) -> None:
    def worker():
        remote_path = f"{remote_dir.rstrip('/')}/{remote_name}"
        try:
            sftp_upload(local_path, remote_dir, remote_name)

            update_state(lambda s: s["system"].update({
                "sftp_last_ok": now_str(),
                "sftp_last_error": None,
            }))

            update_state(lambda s: s.update({
                "last_vehicle": {
                    "detected": True,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "image": rel_img,
                    "count": vehicle_count,
                    "best_confidence": best_confidence,
                    "labels": vehicle_labels,
                    "image_status": "uploaded",
                    "image_file": remote_name,
                    "image_uploaded_path": remote_path,
                    "mode": vehicle_mode,
                    "geofence_overlap": geofence_overlap,
                }
            }))

            update_vehicle_event_result(
                build_vehicle_event_key(event_time),
                image_done=True,
                image_ok=True,
                image_file=remote_name,
                image_path=remote_path,
                image_status="success",
                image_message="Vehicle image uploaded successfully",
            )
            maybe_publish_vehicle_combined_response(build_vehicle_event_key(event_time))

            add_event("vehicle_image_uploaded", {
                "file": remote_name,
                "image_path": remote_path,
                "vehicle_count": vehicle_count,
                "vehicle_labels": vehicle_labels,
                "best_confidence": best_confidence,
            })

        except Exception as exc:
            error_text = str(exc).strip() or repr(exc)

            update_state(lambda s: s["system"].update({
                "sftp_last_error": error_text,
            }))

            update_state(lambda s: s.update({
                "last_vehicle": {
                    "detected": True,
                    "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                    "image": rel_img,
                    "count": vehicle_count,
                    "best_confidence": best_confidence,
                    "labels": vehicle_labels,
                    "image_status": f"upload_failed: {error_text}",
                    "image_file": remote_name,
                    "image_uploaded_path": remote_path,
                    "mode": vehicle_mode,
                    "geofence_overlap": geofence_overlap,
                }
            }))

            update_vehicle_event_result(
                build_vehicle_event_key(event_time),
                image_done=True,
                image_ok=False,
                image_file=remote_name,
                image_path=remote_path,
                image_status="failed",
                image_message=f"Vehicle image upload failed: {error_text}",
            )
            maybe_publish_vehicle_combined_response(build_vehicle_event_key(event_time))

    threading.Thread(target=worker, daemon=True).start()


def handle_vehicle_video_event(
    event_time: dt.datetime,
    event_ts: float,
    vehicle_count: int,
    best_confidence: float,
    vehicle_labels: List[str]
) -> None:
    video_name = build_vehicle_video_name(event_time)
    local_video_path = VIDEO_DIR / video_name
    event_key = build_vehicle_event_key(event_time)

    update_state(lambda s: s.update({
        "last_vehicle_video": {
            "file": video_name,
            "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
            "status": "buffering_post_event",
            "duration_sec": VEHICLE_VIDEO_DURATION_SEC,
            "vehicle_count": vehicle_count,
            "best_confidence": best_confidence,
            "labels": vehicle_labels,
        }
    }))

    try:
        actual_duration_sec = write_buffered_event_video(
            output_path=local_video_path,
            event_ts=event_ts,
            pre_sec=EVENT_PRE_RECORD_SEC,
            post_sec=EVENT_POST_RECORD_SEC,
        )

        update_state(lambda s: s.update({
            "last_vehicle_video": {
                "file": video_name,
                "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                "status": "recorded",
                "duration_sec": actual_duration_sec,
                "vehicle_count": vehicle_count,
                "best_confidence": best_confidence,
                "labels": vehicle_labels,
            }
        }))

        async_upload_vehicle_video(
            local_path=local_video_path,
            remote_dir=SFTP_VEHICLE_VIDEO_DIR,
            remote_name=video_name,
            event_time=event_time,
            vehicle_count=vehicle_count,
            best_confidence=best_confidence,
            vehicle_labels=vehicle_labels,
        )

    except Exception as video_exc:
        remote_path = f"{SFTP_VEHICLE_VIDEO_DIR.rstrip('/')}/{video_name}"

        update_state(lambda s: s.update({
            "last_vehicle_video": {
                "file": video_name,
                "time": event_time.strftime("%Y-%m-%d %H:%M:%S"),
                "status": f"record_failed: {video_exc}",
                "duration_sec": VEHICLE_VIDEO_DURATION_SEC,
                "vehicle_count": vehicle_count,
                "best_confidence": best_confidence,
                "labels": vehicle_labels,
            }
        }))

        update_vehicle_event_result(
            event_key,
            video_done=True,
            video_ok=False,
            video_file=video_name,
            video_path=remote_path,
            video_status="failed",
            video_message=f"Vehicle video record failed: {video_exc}",
            duration_sec=VEHICLE_VIDEO_DURATION_SEC,
        )
        maybe_publish_vehicle_combined_response(event_key)



def mqtt_publish(topic: str, payload: Dict[str, Any]) -> None:
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)
    client.connect(MQTT_HOST, MQTT_PORT, 20)
    msg_info = client.publish(topic, json.dumps(payload), qos=1, retain=False)
    msg_info.wait_for_publish(timeout=5)
    client.disconnect()


def sftp_connect() -> Tuple[paramiko.SSHClient, paramiko.SFTPClient]:
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    ssh.connect(
        hostname=SFTP_HOST,
        port=SFTP_PORT,
        username=SFTP_USERNAME,
        password=SFTP_PASSWORD,
        timeout=20,
        banner_timeout=20,
        auth_timeout=20,
        look_for_keys=False,
        allow_agent=False
    )
    sftp = ssh.open_sftp()
    return ssh, sftp


def sftp_mkdirs(sftp: paramiko.SFTPClient, remote_dir: str) -> None:
    current = "/"
    for part in [p for p in remote_dir.strip("/").split("/") if p]:
        current = f"{current.rstrip('/')}/{part}"
        try:
            sftp.stat(current)
        except FileNotFoundError:
            sftp.mkdir(current)


def wait_for_file_stable(
    path: Path,
    checks: int = 4,
    gap_sec: float = 0.5,
    min_size_bytes: int = 1024
) -> Tuple[bool, int]:
    last_size = -1
    stable_count = 0

    for _ in range(max(1, checks * 4)):
        if not path.exists():
            time.sleep(gap_sec)
            continue

        try:
            current_size = int(path.stat().st_size)
        except Exception:
            current_size = 0

        if current_size >= min_size_bytes and current_size == last_size:
            stable_count += 1
            if stable_count >= checks:
                return True, current_size
        else:
            stable_count = 0

        last_size = current_size
        time.sleep(gap_sec)

    final_size = int(path.stat().st_size) if path.exists() else 0
    return final_size >= min_size_bytes, final_size


def sftp_upload(local_path: Path, remote_dir: str, remote_name: str) -> None:
    local_path = Path(local_path)
    remote_dir = str(remote_dir).rstrip("/")

    if not local_path.exists():
        raise FileNotFoundError(f"Local upload file missing: {local_path}")

    stable_ok, local_size = wait_for_file_stable(local_path)
    if not stable_ok or local_size <= 1024:
        raise RuntimeError(f"Local file not ready or empty: {local_path}, size={local_size}")

    remote_final_path = f"{remote_dir}/{remote_name}"
    remote_temp_path = f"{remote_final_path}.part"

    last_error: Optional[Exception] = None

    for attempt in range(1, 5):
        ssh = None
        sftp = None

        try:
            ssh, sftp = sftp_connect()
            sftp.get_channel().settimeout(90)

            sftp_mkdirs(sftp, remote_dir)

            try:
                sftp.remove(remote_temp_path)
            except Exception:
                pass

            # Upload to temporary file first.
            # confirm=False avoids Paramiko's early false size-check issue.
            sftp.put(str(local_path), remote_temp_path, confirm=False)

            time.sleep(0.8)

            temp_size = int(sftp.stat(remote_temp_path).st_size)
            if temp_size != local_size:
                raise RuntimeError(
                    f"SFTP temp size mismatch attempt={attempt}: "
                    f"remote={temp_size}, local={local_size}, path={remote_temp_path}"
                )

            try:
                sftp.remove(remote_final_path)
            except Exception:
                pass

            sftp.rename(remote_temp_path, remote_final_path)

            time.sleep(0.5)

            final_size = int(sftp.stat(remote_final_path).st_size)
            if final_size != local_size:
                raise RuntimeError(
                    f"SFTP final size mismatch attempt={attempt}: "
                    f"remote={final_size}, local={local_size}, path={remote_final_path}"
                )

            update_state(lambda s: s["system"].update({
                "sftp_last_ok": now_str(),
                "sftp_last_error": None,
                "sftp_last_file": remote_final_path,
                "sftp_last_size": final_size
            }))

            return

        except Exception as exc:
            last_error = exc

            try:
                if sftp is not None:
                    sftp.remove(remote_temp_path)
            except Exception:
                pass

            time.sleep(1.5 * attempt)

        finally:
            try:
                if sftp is not None:
                    sftp.close()
            except Exception:
                pass

            try:
                if ssh is not None:
                    ssh.close()
            except Exception:
                pass

    raise RuntimeError(f"SFTP upload failed after retries: {last_error}")


def sftp_download(remote_path: str, local_path: Path) -> None:
    ssh = None
    sftp = None
    try:
        ssh, sftp = sftp_connect()
        local_path.parent.mkdir(parents=True, exist_ok=True)
        sftp.get(remote_path, str(local_path))
    finally:
        if sftp is not None:
            sftp.close()
        if ssh is not None:
            ssh.close()

def async_send(
    local_path: Path,
    remote_dir: str,
    remote_name: str,
    mqtt_topic: Optional[str],
    mqtt_payload: Optional[Dict[str, Any]]
) -> None:
    def worker():
        if is_power_saving_active():
            update_state(lambda s: s["system"].update({
                "sftp_last_error": "upload skipped: power saving mode active"
            }))
            return

        upload_ok = False
        error_text = None

        try:
            sftp_upload(local_path, remote_dir, remote_name)
            upload_ok = True
            update_state(lambda s: s["system"].update({
                "sftp_last_ok": now_str(),
                "sftp_last_error": None
            }))
        except Exception as exc:
            error_text = str(exc)
            update_state(lambda s: s["system"].update({
                "sftp_last_error": error_text
            }))

        if not upload_ok:
            add_event("sftp_upload_failed", {
                "file": remote_name,
                "remote_dir": remote_dir,
                "error": error_text
            })
            return

        if mqtt_topic and mqtt_payload and not is_power_saving_active():
            try:
                time.sleep(15)
                if is_power_saving_active():
                    return

                mqtt_publish(mqtt_topic, mqtt_payload)
                update_state(lambda s: s["system"].update({
                    "mqtt_last_ok": now_str(),
                    "mqtt_last_error": None
                }))
            except Exception as exc:
                update_state(lambda s: s["system"].update({
                    "mqtt_last_error": str(exc)
                }))

    threading.Thread(target=worker, daemon=True).start()


def clear_audio_queue() -> int:
    cleared_count = 0

    while True:
        try:
            audio_queue.get_nowait()
            audio_queue.task_done()
            cleared_count += 1
        except queue.Empty:
            break

    return cleared_count


def enqueue_audio_job(job: Dict[str, Any]) -> int:
    audio_queue.put(job)
    try:
        return audio_queue.qsize()
    except NotImplementedError:
        return -1


def play_audio_file(local_source: Path) -> None:
    ext = local_source.suffix.lower()
    playable_wav = local_source

    if ext != ".wav":
        playable_wav = TMP_DIR / f"{local_source.stem}.wav"
        subprocess.check_call([
            "ffmpeg", "-y", "-i", str(local_source),
            "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
            str(playable_wav)
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def stop_current_audio_process() -> None:
    global current_audio_proc

    if current_audio_proc is None:
        return

    try:
        if current_audio_proc.poll() is None:
            current_audio_proc.terminate()
            try:
                current_audio_proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                current_audio_proc.kill()
                current_audio_proc.wait(timeout=2)
    except Exception:
        pass
    finally:
        current_audio_proc = None

    try:
        subprocess.run(
            ["pkill", "-9", "aplay"],
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )
    except Exception:
        pass


def play_with_aplay(file_path: Path, timeout_sec: int = 60, retries: int = 3) -> bool:
    global current_audio_proc

    with audio_lock:
        for attempt in range(1, retries + 1):
            try:
                current_audio_proc = subprocess.Popen(
                    ["aplay", "-q", str(file_path)],
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True
                )
                stdout_data, stderr_data = current_audio_proc.communicate(timeout=timeout_sec)

                if current_audio_proc.returncode == 0:
                    print(f"[AUDIO OK] Played: {file_path}", flush=True)
                    return True

                err = (stderr_data or stdout_data or "aplay failed").strip()
                print(f"[AUDIO ERROR] attempt={attempt} {err}", flush=True)

                current_audio_proc = None
                time.sleep(0.4)

            except subprocess.TimeoutExpired:
                stop_current_audio_process()
                print(f"[AUDIO ERROR] attempt={attempt} aplay timeout", flush=True)
                time.sleep(0.4)

            except Exception as exc:
                stop_current_audio_process()
                print(f"[AUDIO ERROR] attempt={attempt} {exc}", flush=True)
                time.sleep(0.4)

            finally:
                current_audio_proc = None

        return False

def play_warning_audio() -> None:
    wav = ASSET_DIR / "warning.wav"
    if not wav.exists():
        print(f"[AUDIO ERROR] Missing warning file: {wav}", flush=True)
        update_state(lambda s: s["system"].update({
            "last_error": f"Missing warning file: {wav}"
        }))
        return

    allowed, reason = is_warning_audio_allowed()
    if not allowed:
        update_state(lambda s: s["warning_audio"].update({
            "last_status": reason
        }))
        add_event("warning_audio_skipped", {
            "file": str(wav.name),
            "reason": reason
        })
        return

    update_state(lambda s: s["warning_audio"].update({
        "last_status": "playing"
    }))
    add_event("warning_audio_playing", {
        "file": str(wav.name),
        "reason": reason
    })

    update_state(lambda s: s.update({
        "last_audio": {
            "file": wav.name,
            "source_path": str(wav),
            "time": now_str(),
            "status": "playing_warning"
        }
    }))

    try:
        stop_current_audio_process()

        ok = play_with_aplay(wav, timeout_sec=20)

        update_state(lambda s: s["warning_audio"].update({
            "last_status": "played" if ok else "failed",
            "last_played_at": now_str()
        }))

        update_state(lambda s: s.update({
            "last_audio": {
                "file": wav.name,
                "source_path": str(wav),
                "time": now_str(),
                "status": "warning_completed" if ok else "warning_failed"
            }
        }))

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "warning_audio_played" if ok else "warning_audio_failed",
            "file": wav.name,
            "time": now_str()
        })

        if ok:
            add_event("warning_audio", {
                "file": wav.name,
                "status": "completed"
            })
        else:
            add_event("warning_audio", {
                "file": wav.name,
                "status": "failed"
            })

    except Exception as exc:
        update_state(lambda s: s["warning_audio"].update({
            "last_status": f"error: {exc}"
        }))
        update_state(lambda s: s["system"].update({
            "last_error": f"warning audio error: {exc}"
        }))
        print(f"[AUDIO ERROR] {exc}", flush=True)




def build_exclusion_mask(roi_shape: Tuple[int, int]) -> np.ndarray:
    roi_h, roi_w = roi_shape
    mask = np.ones((roi_h, roi_w), dtype=np.uint8) * 255

    for rx1, ry1, rx2, ry2 in GARBAGE_EXCLUSION_ZONES:
        x1 = int(roi_w * rx1)
        y1 = int(roi_h * ry1)
        x2 = int(roi_w * rx2)
        y2 = int(roi_h * ry2)
        cv2.rectangle(mask, (x1, y1), (x2, y2), 0, thickness=-1)

    return mask


def preprocess_roi(roi: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
    hsv = cv2.cvtColor(roi, cv2.COLOR_BGR2HSV)
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
    gray = cv2.GaussianBlur(gray, (5, 5), 0)
    hsv = cv2.GaussianBlur(hsv, (5, 5), 0)
    return gray, hsv


def load_reference_roi_shape(shape: Tuple[int, int]) -> Optional[np.ndarray]:
    if not GARBAGE_REF_FILE.exists():
        return None

    ref = cv2.imread(str(GARBAGE_REF_FILE))
    if ref is None:
        return None

    h, w = shape
    if ref.shape[:2] != (h, w):
        ref = cv2.resize(ref, (w, h))
    return ref


def save_reference_roi(color_roi: np.ndarray) -> None:
    cv2.imwrite(str(GARBAGE_REF_FILE), color_roi)


def build_foreground_mask(
    curr_roi: np.ndarray,
    ref_roi: np.ndarray
) -> Tuple[np.ndarray, float, float]:

    curr_gray, curr_hsv = preprocess_roi(curr_roi)
    ref_gray,  ref_hsv  = preprocess_roi(ref_roi)

    _, curr_s, curr_v = cv2.split(curr_hsv)
    _, ref_s,  ref_v  = cv2.split(ref_hsv)

    gray_diff = cv2.absdiff(curr_gray, ref_gray)
    sat_diff  = cv2.absdiff(curr_s,    ref_s)
    val_diff  = cv2.absdiff(curr_v,    ref_v)

    # ── tighter thresholds to reduce false positives ──────────────────
    _, m1 = cv2.threshold(gray_diff, 22,  255, cv2.THRESH_BINARY)
    _, m2 = cv2.threshold(sat_diff,  28,  255, cv2.THRESH_BINARY)
    _, m3 = cv2.threshold(val_diff,  24,  255, cv2.THRESH_BINARY)

    mask = cv2.bitwise_or(m1, m2)
    mask = cv2.bitwise_or(mask, m3)

    ref_edges  = cv2.Canny(ref_gray,  50, 150)
    curr_edges = cv2.Canny(curr_gray, 50, 150)
    edge_change = cv2.absdiff(curr_edges, ref_edges)
    _, edge_change_bin = cv2.threshold(edge_change, 28, 255, cv2.THRESH_BINARY)
    mask = cv2.bitwise_or(mask, edge_change_bin)

    # ── ignore very dark pixels (night shadows) ───────────────────────
    valid_light = cv2.inRange(curr_v, 32, 255)
    mask = cv2.bitwise_and(mask, valid_light)

    if GARBAGE_EXCLUSION_ZONES:
        exclusion_mask = build_exclusion_mask(curr_roi.shape[:2])
        mask           = cv2.bitwise_and(mask, exclusion_mask)
        edge_change_bin = cv2.bitwise_and(edge_change_bin, exclusion_mask)

    k3 = np.ones((3, 3), np.uint8)
    k5 = np.ones((5, 5), np.uint8)

    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,  k3)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, k5)
    mask = cv2.dilate(mask, k3, iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, k5)

    diff_ratio       = round(float(np.count_nonzero(mask))           / float(mask.size),            4) if mask.size            else 0.0
    edge_change_ratio = round(float(np.count_nonzero(edge_change_bin)) / float(edge_change_bin.size), 4) if edge_change_bin.size else 0.0

    return mask, diff_ratio, edge_change_ratio


def contour_solidity(contour: np.ndarray) -> float:
    area = cv2.contourArea(contour)
    hull = cv2.convexHull(contour)
    hull_area = cv2.contourArea(hull)
    if hull_area <= 0:
        return 1.0
    return float(area) / float(hull_area)


def count_candidate_objects(
    curr_roi: np.ndarray,
    ref_roi: np.ndarray
) -> Tuple[int, float, float, float, List[Tuple[int, int, int, int]]]:

    mask, diff_ratio, edge_change_ratio = build_foreground_mask(curr_roi, ref_roi)

    curr_gray = cv2.cvtColor(curr_roi, cv2.COLOR_BGR2GRAY)
    ref_gray  = cv2.cvtColor(ref_roi,  cv2.COLOR_BGR2GRAY)
    curr_edges = cv2.Canny(curr_gray, 40, 120)
    ref_edges  = cv2.Canny(ref_gray,  40, 120)
    edges = cv2.absdiff(curr_edges, ref_edges)

    if GARBAGE_EXCLUSION_ZONES:
        exclusion_mask = build_exclusion_mask(curr_roi.shape[:2])
        edges = cv2.bitwise_and(edges, exclusion_mask)

    # ── improved: watershed-style separation of touching objects ──────
    sure_bg = cv2.dilate(mask, np.ones((5, 5), np.uint8), iterations=2)
    dist    = cv2.distanceTransform(mask, cv2.DIST_L2, 5)
    if dist.max() > 0:
        _, sure_fg = cv2.threshold(dist, 0.28 * dist.max(), 255, 0)
    else:
        sure_fg = mask.copy()
    sure_fg  = sure_fg.astype(np.uint8)
    unknown  = cv2.subtract(sure_bg, sure_fg)

    _, markers = cv2.connectedComponents(sure_fg)
    markers = markers + 1
    markers[unknown == 255] = 0

    roi_color = curr_roi.copy()
    markers   = cv2.watershed(roi_color, markers)
    # convert watershed boundaries back to component mask
    watershed_mask = np.zeros_like(mask)
    watershed_mask[markers > 1] = 255

    # combine original mask + watershed split mask
    combined = cv2.bitwise_or(mask, watershed_mask)

    contours, _ = cv2.findContours(combined, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    roi_h, roi_w = curr_roi.shape[:2]
    roi_area = float(max(roi_h * roi_w, 1))
    max_component_area = roi_area * MAX_COMPONENT_AREA_RATIO

    boxes: List[Tuple[int, int, int, int]] = []
    total_blob_area = 0.0

    for contour in contours:
        area = cv2.contourArea(contour)
        if area < MIN_COMPONENT_AREA or area > max_component_area:
            continue

        x, y, w, h = cv2.boundingRect(contour)

        if w < MIN_COMPONENT_W or h < MIN_COMPONENT_H:
            continue

        if (y + h) > int(roi_h * BOTTOM_STRIP_IGNORE_RATIO):
            continue

        box_area    = float(max(w * h, 1))
        fill_ratio  = float(area) / box_area
        if fill_ratio < MIN_FILL_RATIO or fill_ratio > MAX_FILL_RATIO:
            continue

        aspect_ratio = max(w / max(h, 1), h / max(w, 1))
        if aspect_ratio > MAX_ASPECT_RATIO:
            continue

        if len(contour) < MIN_CONTOUR_POINTS:
            continue

        solidity = contour_solidity(contour)
        if solidity > MAX_SOLIDITY and fill_ratio > 0.88:
            continue

        # edge density check — rejects flat uniform regions (shadows, ground marks)
        component_mask = np.zeros((h, w), dtype=np.uint8)
        shifted = contour.copy()
        shifted[:, 0, 0] -= x
        shifted[:, 0, 1] -= y
        cv2.drawContours(component_mask, [shifted], -1, 255, thickness=-1)

        component_edges = cv2.bitwise_and(
            edges[y:y + h, x:x + w],
            edges[y:y + h, x:x + w],
            mask=component_mask
        )
        edge_density = float(np.count_nonzero(component_edges)) / float(max(area, 1))
        if edge_density < MIN_EDGE_DENSITY:
            continue

        # ── NEW: texture variance check — rejects sky/plain-ground blobs ──
        patch = curr_roi[y:y + h, x:x + w]
        patch_gray = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY) if patch.ndim == 3 else patch
        variance = float(patch_gray.var())
        if variance < 28.0:
            continue

        # ── NEW: color diversity check — garbage is rarely single-tone ──
        patch_hsv = cv2.cvtColor(patch, cv2.COLOR_BGR2HSV)
        hue_std = float(patch_hsv[:, :, 0].std())
        sat_mean = float(patch_hsv[:, :, 1].mean())
        if hue_std < 4.0 and sat_mean < 18.0:
            continue

        boxes.append((x, y, x + w, y + h))
        total_blob_area += area

    blob_area_ratio = round(total_blob_area / roi_area, 4)
    edge_ratio = edge_change_ratio
    return len(boxes), diff_ratio, edge_ratio, blob_area_ratio, boxes

def draw_garbage_boxes(
    frame: np.ndarray,
    roi_box: Tuple[int, int, int, int],
    detections: List[Dict[str, Any]],
    severity: str
) -> np.ndarray:
    out = frame.copy()
    x1_roi, y1_roi, x2_roi, y2_roi = roi_box

    color = (180, 180, 180)
    if severity == "LOW":
        color = (0, 255, 0)
    elif severity == "MEDIUM":
        color = (0, 165, 255)
    elif severity == "HIGH":
        color = (0, 0, 255)

    cv2.rectangle(out, (x1_roi, y1_roi), (x2_roi, y2_roi), (0, 255, 255), 2)
    cv2.putText(
        out,
        "Garbage ROI",
        (x1_roi + 6, max(20, y1_roi - 8)),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.6,
        (0, 255, 255),
        2
    )

    for idx, det in enumerate(detections, start=1):
        x1, y1, x2, y2 = det["box"]
        gx1 = x1 + x1_roi
        gy1 = y1 + y1_roi
        gx2 = x2 + x1_roi
        gy2 = y2 + y1_roi

        label = det.get("display_label", "Object")
        conf = det.get("confidence", 0.0)

        cv2.rectangle(out, (gx1, gy1), (gx2, gy2), color, 2)
        cv2.putText(
            out,
            f"{label} {conf:.2f}",
            (gx1, max(24, gy1 - 8)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.52,
            color,
            2,
            cv2.LINE_AA
        )

    cv2.putText(
        out,
        f"Severity: {severity}  Count: {len(detections)}",
        (20, 30),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.8,
        color,
        2,
        cv2.LINE_AA
    )
    return out


def garbage_count_to_severity(count: int) -> str:
    if count >= COUNT_HIGH_MIN:
        return "HIGH"
    if count >= COUNT_MEDIUM_MIN:
        return "MEDIUM"
    if count >= COUNT_LOW_MIN:
        return "LOW"
    return "NONE"


def _box_iou(box_a: Tuple[int, int, int, int], box_b: Tuple[int, int, int, int]) -> float:
    ax1, ay1, ax2, ay2 = box_a
    bx1, by1, bx2, by2 = box_b

    inter_x1 = max(ax1, bx1)
    inter_y1 = max(ay1, by1)
    inter_x2 = min(ax2, bx2)
    inter_y2 = min(ay2, by2)

    inter_w = max(0, inter_x2 - inter_x1)
    inter_h = max(0, inter_y2 - inter_y1)
    inter_area = inter_w * inter_h

    area_a = max(0, ax2 - ax1) * max(0, ay2 - ay1)
    area_b = max(0, bx2 - bx1) * max(0, by2 - by1)
    denom = float(area_a + area_b - inter_area)

    if denom <= 0.0:
        return 0.0
    return float(inter_area) / denom


def boxes_overlap_or_close(
    a: Tuple[int, int, int, int],
    b: Tuple[int, int, int, int],
    gap_px: int
) -> bool:
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b

    return not (
        ax2 + gap_px < bx1 or
        bx2 + gap_px < ax1 or
        ay2 + gap_px < by1 or
        by2 + gap_px < ay1
    )


def merge_close_boxes(
    boxes: List[Tuple[int, int, int, int]],
    roi_w: int,
    roi_h: int,
    gap_px: int
) -> List[Tuple[int, int, int, int]]:
    if not boxes:
        return []

    merged = list(boxes)
    changed = True

    while changed:
        changed = False
        result: List[Tuple[int, int, int, int]] = []
        used = [False] * len(merged)

        for i, box in enumerate(merged):
            if used[i]:
                continue

            x1, y1, x2, y2 = box
            used[i] = True

            for j in range(i + 1, len(merged)):
                if used[j]:
                    continue

                other = merged[j]
                if boxes_overlap_or_close((x1, y1, x2, y2), other, gap_px):
                    ox1, oy1, ox2, oy2 = other
                    x1 = min(x1, ox1)
                    y1 = min(y1, oy1)
                    x2 = max(x2, ox2)
                    y2 = max(y2, oy2)
                    used[j] = True
                    changed = True

            x1 = max(0, min(roi_w - 1, x1))
            y1 = max(0, min(roi_h - 1, y1))
            x2 = max(0, min(roi_w - 1, x2))
            y2 = max(0, min(roi_h - 1, y2))

            if x2 > x1 and y2 > y1:
                result.append((x1, y1, x2, y2))

        merged = result

    return merged


def garbage_change_box_allowed(
    roi: np.ndarray,
    box: Tuple[int, int, int, int],
    roi_area: float
) -> bool:
    roi_h, roi_w = roi.shape[:2]
    x1, y1, x2, y2 = box

    bw = x2 - x1
    bh = y2 - y1
    if bw <= 0 or bh <= 0:
        return False

    area = float(bw * bh)

    min_area = max(
        int(GARBAGE_CHANGE_MIN_AREA_PX),
        int(roi_area * GARBAGE_CHANGE_MIN_AREA_RATIO)
    )
    min_w = max(22, int(roi_w * GARBAGE_CHANGE_MIN_WIDTH_RATIO))
    min_h = max(22, int(roi_h * GARBAGE_CHANGE_MIN_HEIGHT_RATIO))

    if area < min_area:
        return False

    if bw < min_w or bh < min_h:
        return False

    if area > roi_area * GARBAGE_MAX_BOX_AREA_RATIO:
        return False

    cx = x1 + (bw // 2)
    cy = y1 + (bh // 2)

    for rx1, ry1, rx2, ry2 in GARBAGE_EXCLUSION_ZONES:
        ex1 = int(roi_w * rx1)
        ey1 = int(roi_h * ry1)
        ex2 = int(roi_w * rx2)
        ey2 = int(roi_h * ry2)
        if ex1 <= cx <= ex2 and ey1 <= cy <= ey2:
            return False

    patch = roi[y1:y2, x1:x2]
    if patch is None or patch.size == 0:
        return False

    gray = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY)
    hsv = cv2.cvtColor(patch, cv2.COLOR_BGR2HSV)

    variance = float(gray.var())
    saturation_mean = float(hsv[:, :, 1].mean())

    # Reject small plain ground/shadow/light patches.
    # White bags/paper usually still have enough texture/edge variance.
    if variance < GARBAGE_MIN_TEXTURE_VARIANCE and saturation_mean < GARBAGE_MIN_SATURATION_MEAN:
        return False

    return True


def _load_reference_frame() -> Optional[np.ndarray]:
    if not GARBAGE_REF_FILE.exists():
        return None
    ref = cv2.imread(str(GARBAGE_REF_FILE))
    return ref if ref is not None else None


def capture_garbage_reference_from_live() -> Dict[str, Any]:
    frame = get_frame_copy()
    if frame is None:
        raise RuntimeError("No live frame available")

    GARBAGE_REF_FILE.parent.mkdir(parents=True, exist_ok=True)

    if GARBAGE_REF_FILE.exists():
        GARBAGE_REF_FILE.unlink(missing_ok=True)

    ok = cv2.imwrite(str(GARBAGE_REF_FILE), frame, [int(cv2.IMWRITE_JPEG_QUALITY), 95])
    if not ok:
        raise RuntimeError(f"Failed to save reference image: {GARBAGE_REF_FILE}")

    now = now_dt()
    update_state(lambda s: s.update({
        "last_garbage": {
            "detected": False,
            "time": now.strftime("%Y-%m-%d %H:%M:%S"),
            "image": s["last_garbage"].get("image"),
            "severity": "NONE",
            "diff_ratio": 0.0,
            "edge_ratio": 0.0,
            "blob_area_ratio": 0.0,
            "blob_count": 0,
            "triggered_by": "garbage_sample_capture",
            "reference_items": ["Reference updated"]
        }
    }), force_flush=True)

    add_event("garbage_reference_captured", {
        "file": GARBAGE_REF_FILE.name,
        "time": now.strftime("%Y-%m-%d %H:%M:%S")
    })

    return {
        "file": str(GARBAGE_REF_FILE),
        "captured_at": now.isoformat(),
        "message": "Garbage reference image captured successfully"
    }


def draw_garbage_boxes(
    frame: np.ndarray,
    roi_box: Tuple[int, int, int, int],
    detections: List[Dict[str, Any]],
    severity: str,
    change_boxes: Optional[List[Tuple[int, int, int, int]]] = None
) -> np.ndarray:
    out = frame.copy()
    x1_roi, y1_roi, x2_roi, y2_roi = roi_box

    color = (160, 160, 160)
    if severity == "LOW":
        color = (0, 255, 0)
    elif severity == "MEDIUM":
        color = (0, 165, 255)
    elif severity == "HIGH":
        color = (0, 0, 255)

    garbage_points = get_garbage_polygon_pixels(frame)

    if len(garbage_points) >= 3:
        polygon_np = np.array(garbage_points, dtype=np.int32)
        cv2.polylines(out, [polygon_np], True, (0, 255, 255), 2, cv2.LINE_AA)

    for det in detections:
        x1, y1, x2, y2 = det["box"]
        gx1 = x1 + x1_roi
        gy1 = y1 + y1_roi
        gx2 = x2 + x1_roi
        gy2 = y2 + y1_roi

        cv2.rectangle(out, (gx1, gy1), (gx2, gy2), color, 2)

    for box in change_boxes or []:
        x1, y1, x2, y2 = box
        gx1 = x1 + x1_roi
        gy1 = y1 + y1_roi
        gx2 = x2 + x1_roi
        gy2 = y2 + y1_roi

        cv2.rectangle(out, (gx1, gy1), (gx2, gy2), (255, 0, 255), 2)

    return out



def analyze_garbage_once(frame: np.ndarray) -> Dict[str, Any]:
    reference_frame = _load_reference_frame()
    roi, roi_box = get_garbage_roi(frame)

    if reference_frame is None:
        marked = draw_garbage_boxes(
            frame=frame,
            roi_box=roi_box,
            detections=[],
            severity="NONE",
            change_boxes=[]
        )

        cv2.putText(
            marked,
            "Reference Missing - Send Garbage Sample Capture",
            (20, 35),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.75,
            (0, 0, 255),
            2,
            cv2.LINE_AA
        )
        return {
            "severity": "NONE",
            "diff_ratio": 0.0,
            "edge_ratio": 0.0,
            "blob_area_ratio": 0.0,
            "blob_count": 0,
            "boxes": [],
            "detections": [],
            "garbage_labels": [],
            "marked_frame": marked,
            "roi_color": roi.copy(),
            "ref_missing": True,
            "change_boxes": []
        }

    ref_roi, _ = get_garbage_roi(reference_frame)

    if ref_roi.shape[:2] != roi.shape[:2]:
        ref_roi = cv2.resize(ref_roi, (roi.shape[1], roi.shape[0]), interpolation=cv2.INTER_LINEAR)

    roi_h, roi_w = roi.shape[:2]
    roi_area = float(max(roi_h * roi_w, 1))

    diff_mask, diff_ratio, edge_change_ratio = build_foreground_mask(roi, ref_roi)

    contours, _ = cv2.findContours(diff_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    raw_change_boxes: List[Tuple[int, int, int, int]] = []

    for cnt in contours:
        x, y, w, h = cv2.boundingRect(cnt)
        candidate_box = (x, y, x + w, y + h)

        if not garbage_change_box_allowed(roi, candidate_box, roi_area):
            continue

        raw_change_boxes.append(candidate_box)

    merge_gap_px = max(14, int(min(roi_w, roi_h) * GARBAGE_CHANGE_MERGE_GAP_RATIO))
    merged_change_boxes = merge_close_boxes(raw_change_boxes, roi_w, roi_h, merge_gap_px)

    change_boxes: List[Tuple[int, int, int, int]] = []
    change_area_sum = 0.0

    for box in merged_change_boxes:
        if not garbage_change_box_allowed(roi, box, roi_area):
            continue
        x1, y1, x2, y2 = box
        change_boxes.append(box)
        change_area_sum += float((x2 - x1) * (y2 - y1))

    detections = detect_garbage_objects(roi)

    extra_change_boxes: List[Tuple[int, int, int, int]] = []
    for cbox in change_boxes:
        matched = False
        for det in detections:
            if _box_iou(cbox, det["box"]) >= 0.10:
                matched = True
                break
        if not matched:
            extra_change_boxes.append(cbox)

    total_count = len(detections) + len(extra_change_boxes)
    severity = garbage_count_to_severity(total_count)

    display_labels = [d["display_label"] for d in detections]
    if extra_change_boxes:
        display_labels.extend(["Mixed Trash"] * len(extra_change_boxes))

    avg_conf = round(
        float(sum(d["confidence"] for d in detections)) / float(max(len(detections), 1)),
        4
    ) if detections else 0.0

    blob_area_ratio = round(change_area_sum / roi_area, 4)
    edge_ratio = round(float(len(extra_change_boxes)) / float(max(total_count, 1)), 4)

    marked = draw_garbage_boxes(frame, roi_box, detections, severity, extra_change_boxes)

    return {
        "severity": severity,
        "diff_ratio": diff_ratio,
        "edge_ratio": edge_ratio,
        "blob_area_ratio": blob_area_ratio,
        "blob_count": total_count,
        "boxes": [d["box"] for d in detections],
        "detections": detections,
        "garbage_labels": display_labels,
        "marked_frame": marked,
        "roi_color": roi.copy(),
        "ref_missing": False,
        "change_boxes": extra_change_boxes,
        "object_count": len(detections),
        "change_count": len(extra_change_boxes),
        "object_confidence": avg_conf,
        "raw_change_count": len(raw_change_boxes),
        "merged_change_count": len(change_boxes)
    }


def analyze_garbage_stable() -> Optional[Dict[str, Any]]:
    samples = []

    for idx in range(GARBAGE_CAPTURE_SAMPLES):
        frame = get_frame_copy()
        if frame is None:
            time.sleep(0.2)
            continue

        result = analyze_garbage_once(frame)
        samples.append(result)

        if idx < (GARBAGE_CAPTURE_SAMPLES - 1):
            time.sleep(GARBAGE_SAMPLE_GAP_SEC)

    if not samples:
        return None

    # Prefer the sample with the highest blob_count. This is better for dump events.
    samples.sort(key=lambda item: (
        not item.get("ref_missing", False),
        int(item.get("blob_count", 0)),
        float(item.get("blob_area_ratio", 0.0))
    ), reverse=True)

    return samples[0]


def cloudflare_is_active() -> bool:
    return cloudflare_proc is not None and cloudflare_proc.poll() is None and bool(cloudflare_url)


def stop_live_tunnel() -> None:
    global cloudflare_proc, cloudflare_url, cloudflare_expiry_ts, cloudflare_stop_timer

    with cloudflare_lock:
        if cloudflare_stop_timer is not None:
            try:
                cloudflare_stop_timer.cancel()
            except Exception:
                pass
            cloudflare_stop_timer = None

        if cloudflare_proc is not None:
            try:
                cloudflare_proc.terminate()
                cloudflare_proc.wait(timeout=5)
            except Exception:
                try:
                    cloudflare_proc.kill()
                except Exception:
                    pass

        cloudflare_proc = None
        cloudflare_url = None
        cloudflare_expiry_ts = 0.0

    update_state(lambda s: s["live_link"].update({"active": False, "url": None, "expires_at": None}), force_flush=True)


def start_live_tunnel(duration_sec: int = LIVE_LINK_DURATION_SEC) -> Optional[str]:
    global cloudflare_proc, cloudflare_url, cloudflare_expiry_ts, cloudflare_stop_timer

    with cloudflare_lock:
        now_ts = time.time()

        if cloudflare_is_active() and cloudflare_url:
            cloudflare_expiry_ts = now_ts + duration_sec

            if cloudflare_stop_timer is not None:
                try:
                    cloudflare_stop_timer.cancel()
                except Exception:
                    pass

            cloudflare_stop_timer = threading.Timer(duration_sec, stop_live_tunnel)
            cloudflare_stop_timer.daemon = True
            cloudflare_stop_timer.start()

            expires_at = dt.datetime.fromtimestamp(cloudflare_expiry_ts).strftime("%Y-%m-%d %H:%M:%S")
            update_state(lambda s: s["live_link"].update({"active": True, "url": f"{cloudflare_url}/live", "expires_at": expires_at}), force_flush=True)
            return cloudflare_url

        if not Path(CLOUDFLARED_BIN).exists():
            return None

        if cloudflare_proc is not None:
            try:
                cloudflare_proc.terminate()
                cloudflare_proc.wait(timeout=5)
            except Exception:
                try:
                    cloudflare_proc.kill()
                except Exception:
                    pass

        cmd = [CLOUDFLARED_BIN, "tunnel", "--url", f"http://127.0.0.1:{PORT}", "--no-autoupdate"]
        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1
        )

        found_url = None
        deadline = time.time() + 25
        pattern = re.compile(r"https://[a-z0-9-]+\.trycloudflare\.com", re.IGNORECASE)

        while time.time() < deadline:
            line = proc.stdout.readline()
            if not line:
                if proc.poll() is not None:
                    break
                time.sleep(0.2)
                continue

            match = pattern.search(line)
            if match:
                found_url = match.group(0).rstrip("/")
                break

        if not found_url:
            try:
                proc.terminate()
            except Exception:
                pass
            return None

        cloudflare_proc = proc
        cloudflare_url = found_url
        cloudflare_expiry_ts = time.time() + duration_sec

        if cloudflare_stop_timer is not None:
            try:
                cloudflare_stop_timer.cancel()
            except Exception:
                pass

        cloudflare_stop_timer = threading.Timer(duration_sec, stop_live_tunnel)
        cloudflare_stop_timer.daemon = True
        cloudflare_stop_timer.start()

        expires_at = dt.datetime.fromtimestamp(cloudflare_expiry_ts).strftime("%Y-%m-%d %H:%M:%S")
        update_state(lambda s: s["live_link"].update({"active": True, "url": f"{found_url}/live", "expires_at": expires_at}), force_flush=True)
        return found_url

def permanent_live_url() -> Optional[str]:
    return PERMANENT_LIVE_URL


def permanent_dashboard_url() -> Optional[str]:
    return PERMANENT_DASHBOARD_URL

def build_live_links_payload(force_new: bool = False) -> Dict[str, Any]:
    if is_power_saving_active():
        return {
            "dashboard_url_temp": None,
            "live_stream_url_temp": None,
            "live_expires_in_sec": 0,
            "live_expires_at": None,
            "live_status": "blocked_power_saving"
        }

    live_base = start_live_tunnel(LIVE_LINK_DURATION_SEC) if force_new else (cloudflare_url if cloudflare_is_active() else None)
    expires_at = dt.datetime.fromtimestamp(cloudflare_expiry_ts).isoformat() if cloudflare_expiry_ts > 0 else None
    return {
        "dashboard_url_temp": live_base if live_base else None,
        "live_stream_url_temp": f"{live_base}/live" if live_base else None,
        "live_expires_in_sec": int(max(cloudflare_expiry_ts - time.time(), 0)) if live_base else 0,
        "live_expires_at": expires_at
    }


def build_common_links_payload() -> Dict[str, Any]:
    return {
        "dashboard_url_local": local_dashboard_url(),
        "live_stream_url_local": local_stream_url()
    }


def get_pi_temperature_c() -> Optional[float]:
    try:
        out = subprocess.check_output(["vcgencmd", "measure_temp"], text=True, timeout=3).strip()
        m = re.search(r"([0-9]+(?:\.[0-9]+)?)", out)
        if m:
            return round(float(m.group(1)), 2)
    except Exception:
        pass

    try:
        raw = Path("/sys/class/thermal/thermal_zone0/temp").read_text(encoding="utf-8").strip()
        return round(float(raw) / 1000.0, 2)
    except Exception:
        return None


def detect_primary_iface() -> Optional[str]:
    try:
        out = subprocess.check_output(["ip", "route", "get", "8.8.8.8"], text=True, timeout=3)
        m = re.search(r"\bdev\s+([a-zA-Z0-9._-]+)", out)
        if m:
            return m.group(1)
    except Exception:
        pass

    try:
        out = subprocess.check_output(["ip", "route", "show", "default"], text=True, timeout=3)
        m = re.search(r"\bdev\s+([a-zA-Z0-9._-]+)", out)
        if m:
            return m.group(1)
    except Exception:
        pass

    for candidate in ["eth0", "wlan0"]:
        if Path(f"/sys/class/net/{candidate}").exists():
            return candidate
    return None


def read_iface_link_speed(iface: Optional[str]) -> Optional[int]:
    if not iface:
        return None
    path = Path(f"/sys/class/net/{iface}/speed")
    try:
        raw = path.read_text(encoding="utf-8").strip()
        val = int(raw)
        return val if val > 0 else None
    except Exception:
        return None


def read_iface_operstate(iface: Optional[str]) -> str:
    if not iface:
        return "down"
    try:
        return Path(f"/sys/class/net/{iface}/operstate").read_text(encoding="utf-8").strip()
    except Exception:
        return "unknown"


def read_iface_bytes(iface: Optional[str]) -> Tuple[int, int]:
    if not iface:
        return 0, 0
    try:
        rx = int(Path(f"/sys/class/net/{iface}/statistics/rx_bytes").read_text(encoding="utf-8").strip())
        tx = int(Path(f"/sys/class/net/{iface}/statistics/tx_bytes").read_text(encoding="utf-8").strip())
        return rx, tx
    except Exception:
        return 0, 0


def calc_network_rates(iface: Optional[str]) -> Tuple[int, int]:
    global last_net_rx, last_net_tx, last_net_ts
    rx, tx = read_iface_bytes(iface)
    now_ts = time.time()

    if last_net_rx is None or last_net_tx is None or last_net_ts is None:
        last_net_rx = rx
        last_net_tx = tx
        last_net_ts = now_ts
        return 0, 0

    elapsed = max(now_ts - last_net_ts, 1e-6)
    rx_bps = int(max(rx - last_net_rx, 0) / elapsed)
    tx_bps = int(max(tx - last_net_tx, 0) / elapsed)

    last_net_rx = rx
    last_net_tx = tx
    last_net_ts = now_ts
    return rx_bps, tx_bps


def parse_first_float(text: str) -> Optional[float]:
    m = re.search(r"([-+]?[0-9]+(?:\.[0-9]+)?)", text)
    if not m:
        return None
    try:
        return float(m.group(1))
    except Exception:
        return None


def parse_battery_command_output(text: str) -> Dict[str, Optional[float]]:
    text = (text or "").strip()
    if not text:
        return {
            "battery_voltage": None,
            "battery_current_a": None,
            "battery_current_ma": None
        }

    try:
        payload = json.loads(text)
        voltage = payload.get("battery_voltage")
        current_a = payload.get("battery_current_a")
        current_ma = payload.get("battery_current_ma")

        if current_ma is None and current_a is not None:
            current_ma = float(current_a) * 1000.0
        if current_a is None and current_ma is not None:
            current_a = float(current_ma) / 1000.0

        return {
            "battery_voltage": round(float(voltage), 3) if voltage is not None else None,
            "battery_current_a": round(float(current_a), 3) if current_a is not None else None,
            "battery_current_ma": round(float(current_ma), 1) if current_ma is not None else None
        }
    except Exception:
        pass

    # Backward-compatible fallback:
    # if old command returns only a single float, treat it as voltage
    value = parse_first_float(text)
    return {
        "battery_voltage": round(value * BATTERY_DIVIDER_RATIO, 3) if value is not None else None,
        "battery_current_a": None,
        "battery_current_ma": None
    }


def read_battery_metrics() -> Dict[str, Optional[float]]:
    try:
        if BATTERY_MODE == "disabled":
            return {
                "battery_voltage": None,
                "battery_current_a": None,
                "battery_current_ma": None
            }

        if BATTERY_MODE == "sysfs" and BATTERY_SYSFS_PATH:
            raw = Path(BATTERY_SYSFS_PATH).read_text(encoding="utf-8").strip()
            value = parse_first_float(raw)
            if value is None:
                return {
                    "battery_voltage": None,
                    "battery_current_a": None,
                    "battery_current_ma": None
                }

            if value > 1000:
                if value > 100000:
                    value = value / 1000000.0
                else:
                    value = value / 1000.0

            return {
                "battery_voltage": round(value * BATTERY_DIVIDER_RATIO, 3),
                "battery_current_a": None,
                "battery_current_ma": None
            }

        if BATTERY_MODE == "command" and BATTERY_COMMAND:
            out = subprocess.check_output(
                shlex.split(BATTERY_COMMAND),
                text=True,
                timeout=5
            ).strip()
            return parse_battery_command_output(out)

    except Exception:
        pass

    return {
        "battery_voltage": None,
        "battery_current_a": None,
        "battery_current_ma": None
    }


def battery_status_text(voltage: Optional[float]) -> str:
    if voltage is None:
        return "Unavailable"
    if voltage >= 12.2:
        return "Good"
    if voltage >= 11.7:
        return "Medium"
    return "Low"


def read_ram_usage() -> Dict[str, Optional[float]]:
    try:
        vm = psutil.virtual_memory()
        total_mb = round(vm.total / (1024 * 1024), 2)
        used_mb = round((vm.total - vm.available) / (1024 * 1024), 2)
        percent = round(float(vm.percent), 2)
        return {
            "ram_total_mb": total_mb,
            "ram_used_mb": used_mb,
            "ram_percent": percent
        }
    except Exception:
        return {
            "ram_total_mb": None,
            "ram_used_mb": None,
            "ram_percent": None
        }


def get_tailscale_id() -> Optional[str]:
    """
    Returns the device Tailscale IPv4 address.

    Example command:
        tailscale ip -4

    Example output:
        100.96.110.12

    This value is sent as "tailscale_id" in Data Status.
    """
    try:
        result = subprocess.run(
            ["tailscale", "ip", "-4"],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            timeout=5,
            check=False
        )

        if result.returncode != 0:
            return None

        tailscale_ip_lines = result.stdout.strip().splitlines()
        if not tailscale_ip_lines:
            return None

        tailscale_ip = tailscale_ip_lines[0].strip()
        return tailscale_ip or None

    except Exception:
        return None


def collect_device_status() -> Dict[str, Any]:
    iface = detect_primary_iface()
    network_status = read_iface_operstate(iface)
    link_speed = read_iface_link_speed(iface)
    rx_bps, tx_bps = calc_network_rates(iface)
    temp_c = get_pi_temperature_c()
    location = detect_geo_location()
    ram = read_ram_usage()
    battery = read_battery_metrics()
    battery_voltage = battery.get("battery_voltage")
    tailscale_id = get_tailscale_id()

    return {
        "device_id": DEVICE_ID,
        "camera_name": CAMERA_NAME,
        "software_version": SOFTWARE_VERSION,
        "software_features": SOFTWARE_FEATURES,
        **power_saving_status_payload(),
        **feature_flags_payload(),
        **garbage_detection_mode_payload(),
        "router_ip": ROUTER_IP,
        "configured_pi_ip": PI_IP,
        "detected_pi_ip": detect_local_ip(),
        "public_ip": detect_public_ip(),
        "approx_public_location": location,
        "pi_temperature_c": temp_c,
        "network_status": network_status,
        "network_iface": iface,
        "network_link_speed_mbps": link_speed,
        "network_rx_bps": rx_bps,
        "network_tx_bps": tx_bps,
        "ram_total_mb": ram["ram_total_mb"],
        "ram_used_mb": ram["ram_used_mb"],
        "ram_percent": ram["ram_percent"],
        "battery_voltage": battery_voltage,
        "battery_current_a": battery.get("battery_current_a"),
        "battery_current_ma": battery.get("battery_current_ma"),
        "battery_status": battery_status_text(battery_voltage),
        "camera_ip": CAMERA_IP,
        "camera_config_url": CAMERA_CONFIG_URL,
        "rtsp_url": RTSP_URL,
        "cloudflare_live_rtsp_url": CLOUDFLARE_LIVE_RTSP_URL,
        "tailscale_id": tailscale_id,
        "tailscale_mail_id": TAILSCALE_MAIL_ID,
        "links": {
            "dashboard_local": local_dashboard_url(),
            "stream_local": local_stream_url()
        }
    }

def refresh_device_metrics_in_state() -> None:
    status = collect_device_status()

    def mutate(state):
        state["public_ip"] = status["public_ip"]
        state["raspberry_ip"] = status["detected_pi_ip"]
        state["links"]["dashboard_local"] = status["links"]["dashboard_local"]
        state["links"]["stream_local"] = status["links"]["stream_local"]
        state["camera_config_url"] = status["camera_config_url"]
        state["system"].update({
            "pi_temperature_c": status["pi_temperature_c"],
            "network_status": status["network_status"],
            "network_iface": status["network_iface"],
            "network_link_speed_mbps": status["network_link_speed_mbps"],
            "network_rx_bps": status["network_rx_bps"],
            "network_tx_bps": status["network_tx_bps"],
            "battery_voltage": status["battery_voltage"],
            "battery_current_a": status["battery_current_a"],
            "battery_current_ma": status["battery_current_ma"],
            "battery_status": status["battery_status"],
            "ram_total_mb": status["ram_total_mb"],
            "ram_used_mb": status["ram_used_mb"],
            "ram_percent": status["ram_percent"],
            "geo_location": status["approx_public_location"]
        })
        state["system"].pop("battery_voltage", None)
        state["system"].pop("battery_status", None)

    update_state(mutate)


def has_default_route_for_iface(iface: Optional[str]) -> bool:
    if not iface:
        return False

    try:
        proc = subprocess.run(
            ["ip", "route", "show", "default"],
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=2
        )

        output = proc.stdout.strip()
        if not output:
            return False

        return f" dev {iface} " in f" {output} " or f" dev {iface}\n" in f" {output}\n"

    except Exception:
        return False


def tcp_reachable_for_audio(host: str, port: int) -> Tuple[bool, str]:
    try:
        with socket.create_connection(
            (host, int(port)),
            timeout=NETWORK_AUDIO_TCP_TIMEOUT_SEC
        ):
            return True, f"tcp_reachable_{host}:{port}"
    except Exception as exc:
        return False, f"tcp_unreachable_{host}:{port}: {exc}"


def internet_reachable_for_audio() -> Tuple[bool, str]:
    targets: List[Tuple[str, int]] = []

    try:
        targets.append((str(MQTT_HOST), int(MQTT_PORT)))
    except Exception:
        pass

    targets.extend(NETWORK_AUDIO_INTERNET_TEST_TARGETS)

    seen = set()
    errors: List[str] = []

    for host, port in targets:
        key = (host, int(port))
        if key in seen:
            continue

        seen.add(key)

        ok, reason = tcp_reachable_for_audio(host, int(port))
        if ok:
            return True, reason

        errors.append(reason)

    return False, "all_internet_targets_unreachable: " + " | ".join(errors[:4])


def is_network_reachable_for_audio() -> Tuple[bool, str, Optional[str]]:
    """
    Audio mute/unmute must follow real internet availability.

    Correct behavior:
      - Interface down = offline.
      - Interface up but no default route = offline.
      - Interface up + router/default route but WAN internet down = offline.
      - Interface up + real TCP internet target reachable = online.
    """
    iface = detect_primary_iface()
    operstate = read_iface_operstate(iface)

    if operstate != "up":
        return False, f"interface_{operstate}", iface

    if not has_default_route_for_iface(iface):
        return False, f"interface_up_no_default_route_{iface}", iface

    internet_ok, internet_reason = internet_reachable_for_audio()

    if internet_ok:
        return True, f"interface_up_default_route_real_internet_ok_{internet_reason}", iface

    return False, f"interface_up_default_route_but_real_internet_offline_{internet_reason}", iface


def is_network_audio_muted() -> bool:
    try:
        state = load_state()
        return bool(state.get("system", {}).get("network_audio_muted", False))
    except Exception:
        return False


def update_network_audio_state(
    muted: bool,
    reason: str,
    iface: Optional[str],
    saved_volume: Optional[int]
) -> None:
    update_state(lambda s: s["system"].update({
        "network_audio_muted": muted,
        "network_audio_mute_reason": reason,
        "network_audio_last_changed_at": now_str(),
        "network_audio_last_check_at": now_str(),
        "network_audio_iface": iface,
        "network_audio_saved_volume": saved_volume
    }), force_flush=True)


def mute_audio_for_network_offline(reason: str, iface: Optional[str]) -> None:
    global network_audio_restore_volume

    saved_volume: Optional[int] = network_audio_restore_volume

    if saved_volume is None:
        try:
            state = load_state()
            old_saved_volume = state.get("system", {}).get("network_audio_saved_volume")
            if old_saved_volume is not None:
                old_saved_volume = int(old_saved_volume)
                if old_saved_volume > 0:
                    saved_volume = old_saved_volume
        except Exception:
            saved_volume = None

    if saved_volume is None:
        try:
            current_volume = get_current_pi_volume()
            current_percent = int(current_volume.get("volume_percent", 0))

            # Do not save 0 as restore volume.
            # If current volume is already 0, restore later to default 70%.
            if current_percent > 0:
                saved_volume = current_percent
            else:
                saved_volume = NETWORK_AUDIO_DEFAULT_RESTORE_VOLUME_PERCENT

            network_audio_restore_volume = saved_volume
        except Exception:
            saved_volume = NETWORK_AUDIO_DEFAULT_RESTORE_VOLUME_PERCENT
            network_audio_restore_volume = saved_volume

    try:
        set_pi_volume(0)
    except Exception as exc:
        update_state(lambda s: s["system"].update({
            "last_error": f"Network auto mute volume failed: {exc}"
        }))

    try:
        stop_current_audio_process()
    except Exception:
        pass

    cleared_count = clear_audio_queue()

    update_network_audio_state(
        muted=True,
        reason=reason,
        iface=iface,
        saved_volume=saved_volume
    )

    update_state(lambda s: s.update({
        "last_audio": {
            "file": None,
            "source_path": None,
            "time": now_str(),
            "status": f"auto_muted_network_offline_queue_cleared_{cleared_count}"
        }
    }), force_flush=True)

    add_event("network_audio_auto_muted", {
        "reason": reason,
        "iface": iface,
        "saved_volume": saved_volume,
        "cleared_count": cleared_count
    })


def unmute_audio_for_network_online(reason: str, iface: Optional[str]) -> None:
    global network_audio_restore_volume

    restore_volume = network_audio_restore_volume

    if restore_volume is None:
        try:
            state = load_state()
            saved_volume = state.get("system", {}).get("network_audio_saved_volume")
            if saved_volume is not None:
                restore_volume = int(saved_volume)
        except Exception:
            restore_volume = None

    # Never restore to 0 when network is online.
    if restore_volume is None or int(restore_volume) <= 0:
        restore_volume = NETWORK_AUDIO_DEFAULT_RESTORE_VOLUME_PERCENT

    restore_volume = max(1, min(100, int(restore_volume)))
    restore_status = "not_restored"

    try:
        result = set_pi_volume(restore_volume)
        restore_status = f"restored_{result['volume_percent']}%"
    except Exception as exc:
        restore_status = f"restore_failed: {exc}"
        update_state(lambda s: s["system"].update({
            "last_error": f"Network auto unmute volume restore failed: {exc}"
        }))

    network_audio_restore_volume = None

    update_network_audio_state(
        muted=False,
        reason=reason,
        iface=iface,
        saved_volume=None
    )

    update_state(lambda s: s.update({
        "last_audio": {
            "file": None,
            "source_path": None,
            "time": now_str(),
            "status": f"auto_unmuted_network_online_{restore_status}"
        }
    }), force_flush=True)

    add_event("network_audio_auto_unmuted", {
        "reason": reason,
        "iface": iface,
        "restore_status": restore_status
    })

    try:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "network_audio_auto_unmuted",
            "message": "Network is online. Audio volume restored.",
            "restore_status": restore_status,
            "time": now_str()
        })
    except Exception:
        pass


def network_audio_monitor_loop() -> None:
    global network_audio_last_online

    while not stop_event.is_set():
        try:
            online, reason, iface = is_network_reachable_for_audio()

            update_state(lambda s: s["system"].update({
                "network_audio_last_check_at": now_str(),
                "network_audio_iface": iface
            }))

            if network_audio_last_online is None:
                network_audio_last_online = online

                if online:
                    should_restore = False

                    try:
                        state = load_state()
                        system_state = state.get("system", {})
                        should_restore = (
                            bool(system_state.get("network_audio_muted", False))
                            or system_state.get("network_audio_saved_volume") is not None
                        )
                    except Exception:
                        should_restore = False

                    try:
                        current_volume = get_current_pi_volume()
                        current_percent = int(current_volume.get("volume_percent", 0))

                        # First boot self-heal:
                        # If app starts while network is already online but ALSA stayed at 0%,
                        # restore once instead of leaving audio muted forever.
                        if current_percent <= 0:
                            should_restore = True
                    except Exception:
                        pass

                    if should_restore:
                        unmute_audio_for_network_online(reason, iface)
                    else:
                        update_network_audio_state(
                            muted=False,
                            reason=reason,
                            iface=iface,
                            saved_volume=None
                        )
                else:
                    mute_audio_for_network_offline(reason, iface)

            elif online != network_audio_last_online:
                network_audio_last_online = online

                if online:
                    unmute_audio_for_network_online(reason, iface)
                else:
                    mute_audio_for_network_offline(reason, iface)

            elif online:
                try:
                    state = load_state()
                    system_state = state.get("system", {})

                    # Self-heal state mismatch:
                    # If state still says muted while network is online, force unmute.
                    if (
                        bool(system_state.get("network_audio_muted", False))
                        or system_state.get("network_audio_saved_volume") is not None
                    ):
                        unmute_audio_for_network_online(reason, iface)
                except Exception:
                    pass

            elif not online and is_network_audio_muted():
                # Keep device silent only while network is truly offline.
                try:
                    set_pi_volume(0)
                except Exception:
                    pass

                try:
                    stop_current_audio_process()
                except Exception:
                    pass

        except Exception as exc:
            update_state(lambda s: s["system"].update({
                "last_error": f"Network audio monitor error: {exc}"
            }))

        for _ in range(NETWORK_AUDIO_CHECK_INTERVAL_SEC):
            if stop_event.is_set():
                return
            time.sleep(1)

def live_talk_token_valid(req) -> bool:
    expected_token = str(MQTT_COMMAND_TOKEN or "").strip()

    if not expected_token:
        return True

    token = ""

    try:
        token = str(req.headers.get("X-GCam-Token") or "").strip()
    except Exception:
        token = ""

    if not token:
        try:
            token = str(req.args.get("token") or "").strip()
        except Exception:
            token = ""

    if not token:
        try:
            token = str(req.form.get("token") or "").strip()
        except Exception:
            token = ""

    if not token:
        try:
            data = req.get_json(force=False, silent=True) or {}
            token = str(data.get("token") or "").strip()
        except Exception:
            token = ""

    return token == expected_token


def is_live_talk_active() -> bool:
    with live_talk_lock:
        return bool(live_talk_active)


def set_live_talk_active(active: bool) -> None:
    global live_talk_active

    with live_talk_lock:
        live_talk_active = bool(active)


def is_live_talk_stopping() -> bool:
    with live_talk_lock:
        return bool(live_talk_stopping)


def set_live_talk_stopping(stopping: bool) -> None:
    global live_talk_stopping

    with live_talk_lock:
        live_talk_stopping = bool(stopping)


def clear_live_talk_queue() -> int:
    cleared = 0

    while True:
        try:
            live_talk_queue.get_nowait()
        except queue.Empty:
            break

        try:
            live_talk_queue.task_done()
        except Exception:
            pass

        cleared += 1

    update_state(lambda s: s["system"].update({
        "live_talk_queue_size": live_talk_queue.qsize()
    }))

    return cleared


def is_live_talk_pipeline_running() -> bool:
    with live_talk_lock:
        ffmpeg_running = (
            live_talk_ffmpeg_process is not None
            and live_talk_ffmpeg_process.poll() is None
        )
        aplay_running = (
            live_talk_aplay_process is not None
            and live_talk_aplay_process.poll() is None
        )

    return ffmpeg_running and aplay_running


def stop_live_talk_processes(graceful: bool = False) -> None:
    global live_talk_ffmpeg_process, live_talk_aplay_process

    with live_talk_lock:
        ffmpeg_proc = live_talk_ffmpeg_process
        aplay_proc = live_talk_aplay_process
        live_talk_ffmpeg_process = None
        live_talk_aplay_process = None

    if ffmpeg_proc is not None:
        try:
            if ffmpeg_proc.stdin:
                try:
                    ffmpeg_proc.stdin.close()
                except Exception:
                    pass
        except Exception:
            pass

    for proc in [ffmpeg_proc, aplay_proc]:
        if proc is None:
            continue

        try:
            if proc.poll() is None:
                if graceful:
                    try:
                        proc.wait(timeout=LIVE_TALK_STOP_TIMEOUT_SEC)
                        continue
                    except Exception:
                        pass

                proc.terminate()

                try:
                    proc.wait(timeout=LIVE_TALK_STOP_TIMEOUT_SEC)
                except Exception:
                    proc.kill()
        except Exception:
            pass


def finish_live_talk_after_drain() -> None:
    deadline = time.time() + LIVE_TALK_GRACEFUL_DRAIN_TIMEOUT_SEC

    while not stop_event.is_set() and time.time() < deadline:
        if live_talk_queue.qsize() <= 0:
            time.sleep(LIVE_TALK_GRACEFUL_IDLE_DELAY_SEC)
            break

        time.sleep(0.1)

    set_live_talk_active(False)
    set_live_talk_stopping(False)

    cleared_count = clear_live_talk_queue()
    stop_live_talk_processes(graceful=True)

    update_state(lambda s: s["system"].update({
        "live_talk_active": False,
        "live_talk_draining": False,
        "live_talk_last_stopped_at": now_str(),
        "live_talk_queue_size": live_talk_queue.qsize()
    }), force_flush=True)

    add_event("live_talk_stopped_after_drain", {
        "device_id": DEVICE_ID,
        "cleared_count": cleared_count,
        "time": now_str()
    })


def start_live_talk_pipeline() -> bool:
    global live_talk_ffmpeg_process, live_talk_aplay_process

    with live_talk_lock:
        existing_ffmpeg_ok = (
            live_talk_ffmpeg_process is not None
            and live_talk_ffmpeg_process.poll() is None
        )
        existing_aplay_ok = (
            live_talk_aplay_process is not None
            and live_talk_aplay_process.poll() is None
        )

        if existing_ffmpeg_ok and existing_aplay_ok:
            return True

    stop_live_talk_processes()

    ffmpeg_cmd = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-fflags",
        "nobuffer",
        "-flags",
        "low_delay",
        "-probesize",
        "32768",
        "-analyzeduration",
        "0",
        "-i",
        "pipe:0",
        "-vn",
        "-af",
        LIVE_TALK_VOLUME_FILTER,
        "-acodec",
        "pcm_s16le",
        "-ac",
        "1",
        "-ar",
        "16000",
        "-f",
        "s16le",
        "pipe:1"
    ]

    aplay_cmd = [
        "aplay",
        "-q",
        "-f",
        "S16_LE",
        "-c",
        "1",
        "-r",
        "16000",
        "-t",
        "raw",
        "-"
    ]

    try:
        ffmpeg_proc = subprocess.Popen(
            ffmpeg_cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            bufsize=0
        )

        if ffmpeg_proc.stdout is None:
            raise RuntimeError("ffmpeg_stdout_not_available")

        aplay_proc = subprocess.Popen(
            aplay_cmd,
            stdin=ffmpeg_proc.stdout,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            bufsize=0
        )

        try:
            ffmpeg_proc.stdout.close()
        except Exception:
            pass

        with live_talk_lock:
            live_talk_ffmpeg_process = ffmpeg_proc
            live_talk_aplay_process = aplay_proc

        update_state(lambda s: s["system"].update({
            "live_talk_last_error": None
        }))

        return True

    except Exception as exc:
        stop_live_talk_processes()
        update_state(lambda s: s["system"].update({
            "live_talk_last_error": f"pipeline_start_failed: {exc}"
        }))
        return False


def enqueue_live_talk_chunk(audio_bytes: bytes) -> Tuple[bool, str]:
    if not audio_bytes:
        return False, "empty_audio_chunk"

    if len(audio_bytes) > LIVE_TALK_MAX_CHUNK_BYTES:
        return False, "audio_chunk_too_large"

    try:
        live_talk_queue.put({
            "audio_bytes": audio_bytes,
            "created_at": now_str()
        }, timeout=1.5)

        update_state(lambda s: s["system"].update({
            "live_talk_queue_size": live_talk_queue.qsize(),
            "live_talk_last_chunk_at": now_str(),
            "live_talk_last_error": None
        }))

        return True, "queued"

    except queue.Full:
        return False, "live_talk_queue_full"


def write_live_talk_chunk_to_pipeline(audio_bytes: bytes) -> None:
    if not is_live_talk_active():
        return

    if is_power_saving_active():
        return

    if is_network_audio_muted():
        return

    if not start_live_talk_pipeline():
        return

    with live_talk_lock:
        ffmpeg_proc = live_talk_ffmpeg_process

    if ffmpeg_proc is None or ffmpeg_proc.poll() is not None or ffmpeg_proc.stdin is None:
        update_state(lambda s: s["system"].update({
            "live_talk_last_error": "ffmpeg_pipeline_not_running"
        }))
        set_live_talk_active(False)
        return

    try:
        ffmpeg_proc.stdin.write(audio_bytes)
        ffmpeg_proc.stdin.flush()

    except BrokenPipeError:
        update_state(lambda s: s["system"].update({
            "live_talk_last_error": "ffmpeg_broken_pipe"
        }))
        set_live_talk_active(False)
        stop_live_talk_processes()

    except Exception as exc:
        update_state(lambda s: s["system"].update({
            "live_talk_last_error": f"pipeline_write_failed: {exc}"
        }))
        set_live_talk_active(False)
        stop_live_talk_processes()


def live_talk_worker_loop() -> None:
    LIVE_TALK_DIR.mkdir(parents=True, exist_ok=True)

    while not stop_event.is_set():
        try:
            job = live_talk_queue.get(timeout=1)
        except queue.Empty:
            continue

        try:
            audio_bytes = job.get("audio_bytes", b"")

            if is_live_talk_active():
                write_live_talk_chunk_to_pipeline(audio_bytes)

        finally:
            try:
                live_talk_queue.task_done()
            except Exception:
                pass

            update_state(lambda s: s["system"].update({
                "live_talk_queue_size": live_talk_queue.qsize()
            }))

def reader_loop() -> None:
    global latest_frame, latest_frame_ts

    while not stop_event.is_set():
        if is_power_saving_active():
            clear_latest_frame()
            update_state(lambda s: s["system"].update({
                "camera_connected": False,
                "last_error": "Power saving mode active"
            }))
            time.sleep(1)
            continue

        cap = open_rtsp()

        if not cap.isOpened():
            update_state(lambda s: s["system"].update({"camera_connected": False, "last_error": "Unable to open RTSP stream"}))
            time.sleep(3)
            continue

        update_state(lambda s: s["system"].update({"camera_connected": True, "last_error": None}))

        while not stop_event.is_set():
            if is_power_saving_active():
                clear_latest_frame()
                update_state(lambda s: s["system"].update({
                    "camera_connected": False,
                    "last_error": "Power saving mode active"
                }))
                break

            ok, frame = cap.read()
            if not ok or frame is None:
                update_state(lambda s: s["system"].update({"camera_connected": False, "last_error": "RTSP read failed, reconnecting"}))
                break

            h, w = frame.shape[:2]
            if w > FRAME_WIDTH:
                new_h = int(h * (FRAME_WIDTH / float(w)))
                frame = cv2.resize(frame, (FRAME_WIDTH, new_h))

            frame_ts = time.time()

            with frame_lock:
                latest_frame = frame
                latest_frame_ts = frame_ts

            add_frame_to_rolling_buffer(frame, frame_ts)

            update_state(lambda s: s["system"].update({
                "camera_connected": True,
                "last_frame_at": now_str(),
                "last_error": None
            }))

        cap.release()
        time.sleep(2)


def detector_loop() -> None:
    last_person_event_ts = 0.0
    last_audio_ts = 0.0
    last_seen_person_ts = 0.0
    consecutive_hits = 0
    consecutive_misses = 0
    counter = 0
    person_present_latch = False

    last_vehicle_event_ts = 0.0
    last_seen_vehicle_ts = 0.0
    vehicle_present_latch = False
    vehicle_confirm_hits = 0

    # Vehicle stable-inside-marking confirmation state.
    vehicle_inside_started_ts = 0.0
    vehicle_inside_last_seen_ts = 0.0
    vehicle_inside_hit_count = 0

    previous_motion_frame: Optional[np.ndarray] = None
    last_vehicle_detection_mode = load_vehicle_detection_mode()
    last_disabled_state_update_ts = 0.0
    last_processed_frame_ts = 0.0

    while not stop_event.is_set():
        if is_power_saving_active():
            consecutive_hits = 0
            consecutive_misses = 0
            person_present_latch = False

            vehicle_present_latch = False
            vehicle_confirm_hits = 0
            vehicle_inside_started_ts = 0.0
            vehicle_inside_last_seen_ts = 0.0
            vehicle_inside_hit_count = 0

            previous_motion_frame = None

            update_state(lambda s: s.update({
                "last_person": {
                    "detected": False,
                    "time": s["last_person"].get("time"),
                    "image": s["last_person"].get("image"),
                    "count": s["last_person"].get("count", 0),
                    "best_confidence": s["last_person"].get("best_confidence", 0)
                },
                "last_vehicle": {
                    "detected": False,
                    "time": s["last_vehicle"].get("time"),
                    "image": s["last_vehicle"].get("image"),
                    "count": s["last_vehicle"].get("count", 0),
                    "best_confidence": s["last_vehicle"].get("best_confidence", 0.0),
                    "labels": s["last_vehicle"].get("labels", []),
                    "image_status": s["last_vehicle"].get("image_status"),
                    "image_file": s["last_vehicle"].get("image_file"),
                    "image_uploaded_path": s["last_vehicle"].get("image_uploaded_path"),
                }
            }))

            time.sleep(1)
            continue

        frame, frame_ts = get_frame_copy_with_ts()
        if frame is None:
            time.sleep(0.02)
            continue

        if frame_ts <= last_processed_frame_ts:
            time.sleep(0.005)
            continue

        last_processed_frame_ts = frame_ts

        flags = load_feature_flags()
        person_detection_enabled = bool(flags.get("person_detection_enabled", True))
        vehicle_detection_enabled = bool(flags.get("vehicle_detection_enabled", True))
        person_video_recording_enabled = bool(flags.get("person_video_recording_enabled", True))
        vehicle_video_recording_enabled = bool(flags.get("vehicle_video_recording_enabled", True))
        vehicle_detection_mode = load_vehicle_detection_mode()

        if vehicle_detection_mode != last_vehicle_detection_mode:
            vehicle_present_latch = False
            vehicle_confirm_hits = 0
            vehicle_inside_started_ts = 0.0
            vehicle_inside_last_seen_ts = 0.0
            vehicle_inside_hit_count = 0
            previous_motion_frame = None
            last_vehicle_detection_mode = vehicle_detection_mode

        if not person_detection_enabled:
            consecutive_hits = 0
            consecutive_misses = 0
            person_present_latch = False

        if not vehicle_detection_enabled:
            vehicle_present_latch = False
            vehicle_confirm_hits = 0
            vehicle_inside_started_ts = 0.0
            vehicle_inside_last_seen_ts = 0.0
            vehicle_inside_hit_count = 0
            previous_motion_frame = None

        if (not person_detection_enabled or not vehicle_detection_enabled) and (time.time() - last_disabled_state_update_ts) >= 1.0:
            def mark_disabled_detection_state(state):
                if not person_detection_enabled:
                    state["last_person"] = {
                        "detected": False,
                        "time": state["last_person"].get("time"),
                        "image": state["last_person"].get("image"),
                        "count": state["last_person"].get("count", 0),
                        "best_confidence": state["last_person"].get("best_confidence", 0),
                    }

                if not vehicle_detection_enabled:
                    state["last_vehicle"] = {
                        "detected": False,
                        "time": state["last_vehicle"].get("time"),
                        "image": state["last_vehicle"].get("image"),
                        "count": state["last_vehicle"].get("count", 0),
                        "best_confidence": state["last_vehicle"].get("best_confidence", 0.0),
                        "labels": state["last_vehicle"].get("labels", []),
                        "image_status": state["last_vehicle"].get("image_status"),
                        "image_file": state["last_vehicle"].get("image_file"),
                        "image_uploaded_path": state["last_vehicle"].get("image_uploaded_path"),
                    }

                state["feature_flags"] = flags

            update_state(mark_disabled_detection_state)
            last_disabled_state_update_ts = time.time()

        if not person_detection_enabled and not vehicle_detection_enabled:
            time.sleep(0.05)
            continue

        counter += 1
        persons: List[Dict[str, Any]] = []
        vehicles: List[Dict[str, Any]] = []

        try:
            motion_mask = None
            if vehicle_detection_enabled and vehicle_detection_mode == "moving":
                motion_mask = create_motion_mask(previous_motion_frame, frame)
                previous_motion_frame = frame.copy()
            elif vehicle_detection_enabled:
                previous_motion_frame = None

            if person_detection_enabled and (PERSON_DETECT_EVERY_N_FRAMES <= 1 or counter % PERSON_DETECT_EVERY_N_FRAMES == 0):
                persons = detect_persons(frame)
                print(
                    f"[PERSON DEBUG] raw_detected={len(persons)} "
                    f"hits={consecutive_hits}",
                    flush=True
                )

            if vehicle_detection_enabled and (VEHICLE_DETECT_EVERY_N_FRAMES <= 1 or counter % VEHICLE_DETECT_EVERY_N_FRAMES == 0):
                vehicles = detect_vehicles(frame, motion_mask, vehicle_detection_mode)
                print(
                    f"[VEHICLE DEBUG] mode={vehicle_detection_mode} detected={len(vehicles)} "
                    f"hits={vehicle_confirm_hits}",
                    flush=True
                )

            # ---------------- PERSON FLOW ----------------
            if person_detection_enabled:
                if persons:
                    now_ts = time.time()
                    consecutive_hits += 1
                    consecutive_misses = 0
                    last_seen_person_ts = now_ts

                    confirmed_person = consecutive_hits >= PERSON_CONFIRMATION_FRAMES
                    best_conf = round(max(p["confidence"] for p in persons), 3)

                    update_state(lambda s: s.update({
                        "last_person": {
                            "detected": confirmed_person,
                            "time": now_str(),
                            "image": s["last_person"].get("image"),
                            "count": len(persons),
                            "best_confidence": best_conf
                        }
                    }))

                    is_new_person = confirmed_person and (not person_present_latch)

                    if is_new_person and (now_ts - last_audio_ts) >= AUDIO_COOLDOWN_SEC:
                        allowed, reason = is_warning_audio_allowed()

                        update_state(lambda s: s["warning_audio"].update({
                            "last_status": reason
                        }))

                        if allowed:
                            update_state(lambda s: s.update({
                                "last_audio": {
                                    "file": "warning.wav",
                                    "source_path": str(ASSET_DIR / "warning.wav"),
                                    "time": now_str(),
                                    "status": "warning_triggered"
                                }
                            }))

                            add_event("warning_audio_triggered", {
                                "reason": reason,
                                "mode": "confirmed_person",
                                "confirmation_frames": PERSON_CONFIRMATION_FRAMES
                            })

                            threading.Thread(
                                target=play_warning_audio,
                                daemon=True
                            ).start()

                            last_audio_ts = now_ts
                        else:
                            add_event("warning_audio_skipped", {
                                "reason": reason
                            })

                    should_fire_event = (
                        is_new_person and
                        (now_ts - last_person_event_ts) >= PERSON_COOLDOWN_SEC
                    )

                    if should_fire_event:
                        now = now_dt()
                        event_ts = time.time()

                        filename = f"{DEVICE_ID}_Person_{now.strftime('%Y%m%d_%H%M%S')}.jpg"
                        marked = draw_person_annotations(frame, persons)

                        rel_img, path = save_capture(marked, filename, garbage=False)
                        remote_image_path = f"{SFTP_PERSON_DIR.rstrip('/')}/{filename}"

                        async_send(path, SFTP_PERSON_DIR, filename, None, None)

                        update_state(lambda s: s.update({
                            "last_person": {
                                "detected": True,
                                "time": now.strftime("%Y-%m-%d %H:%M:%S"),
                                "image": rel_img,
                                "count": len(persons),
                                "best_confidence": best_conf
                            }
                        }))

                        if person_video_recording_enabled:
                            threading.Thread(
                                target=handle_person_video_event,
                                args=(now, event_ts, len(persons), best_conf, filename, remote_image_path),
                                daemon=True
                            ).start()
                        else:
                            update_state(lambda s: s.update({
                                "last_video": {
                                    "file": None,
                                    "time": now.strftime("%Y-%m-%d %H:%M:%S"),
                                    "status": "skipped_disabled_by_mqtt",
                                    "duration_sec": 0,
                                    "person_count": len(persons),
                                    "best_confidence": best_conf,
                                }
                            }))

                            add_event("person_video_recording_skipped", {
                                "reason": "person video recording disabled by mqtt command",
                                "image_file": filename,
                                "image_path": remote_image_path,
                                "person_count": len(persons),
                                "best_confidence": best_conf,
                            })

                            try:
                                mqtt_publish(MQTT_CAMERA_EVENT_TOPIC, {
                                    "date": now.strftime("%Y-%m-%d"),
                                    "time": now.strftime("%H:%M:%S"),
                                    "event": "person_detected",
                                    "device_id": DEVICE_ID,
                                    "image_file": filename,
                                    "video_file": None,
                                    "image_path": remote_image_path,
                                    "video_path": None,
                                    "camera_name": CAMERA_NAME,
                                    "duration_sec": 0,
                                    "person_count": len(persons),
                                    "video_status": "skipped",
                                    "video_message": "Person video recording disabled by MQTT command",
                                    "best_confidence": best_conf,
                                    "video_uploaded_at": dt.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
                                    **build_common_links_payload(),
                                })
                                update_state(lambda s: s["system"].update({
                                    "mqtt_last_ok": now_str(),
                                    "mqtt_last_error": None,
                                }))
                            except Exception as mqtt_exc:
                                update_state(lambda s: s["system"].update({
                                    "mqtt_last_error": str(mqtt_exc),
                                }))

                        last_person_event_ts = now_ts

                    if confirmed_person:
                        person_present_latch = True

                else:
                    consecutive_hits = 0
                    consecutive_misses += 1

                    if (time.time() - last_seen_person_ts) >= PERSON_LOST_TIMEOUT_SEC:
                        person_present_latch = False
                        update_state(lambda s: s.update({
                            "last_person": {
                                "detected": False,
                                "time": s["last_person"].get("time"),
                                "image": s["last_person"].get("image"),
                                "count": s["last_person"].get("count", 0),
                                "best_confidence": s["last_person"].get("best_confidence", 0)
                            }
                        }))

            # ---------------- VEHICLE FLOW ----------------
            if vehicle_detection_enabled:
                now_ts = time.time()

                def get_vehicle_overlap(vehicle: Dict[str, Any]) -> float:
                    try:
                        return float(
                            vehicle.get(
                                "geofence_overlap",
                                vehicle.get(
                                    "overlap_ratio",
                                    vehicle.get("overlap", 0.0)
                                )
                            ) or 0.0
                        )
                    except Exception:
                        return 0.0

                stable_vehicle_candidates = []

                for vehicle in vehicles:
                    vehicle_conf = float(vehicle.get("confidence", 0.0) or 0.0)
                    vehicle_overlap = get_vehicle_overlap(vehicle)

                    if (
                        vehicle_conf >= VEHICLE_EVENT_MIN_CONF and
                        vehicle_overlap >= VEHICLE_GEOFENCE_MIN_OVERLAP_RATIO
                    ):
                        stable_vehicle_candidates.append(vehicle)

                raw_vehicle_count = len(vehicles)
                stable_vehicle_count = len(stable_vehicle_candidates)

                if vehicles:
                    best_vehicle_conf = round(
                        max(float(v.get("confidence", 0.0) or 0.0) for v in vehicles),
                        3
                    )
                    vehicle_labels = sorted({
                        str(v.get("label", "vehicle")).strip() or "vehicle"
                        for v in vehicles
                    })
                    best_geofence_overlap = round(
                        max(get_vehicle_overlap(v) for v in vehicles),
                        3
                    )
                else:
                    best_vehicle_conf = 0.0
                    vehicle_labels = []
                    best_geofence_overlap = 0.0

                if stable_vehicle_candidates:
                    last_seen_vehicle_ts = now_ts
                    vehicle_confirm_hits += 1

                    if (
                        vehicle_inside_started_ts <= 0.0 or
                        (now_ts - vehicle_inside_last_seen_ts) > VEHICLE_STABLE_GRACE_SEC
                    ):
                        vehicle_inside_started_ts = now_ts
                        vehicle_inside_hit_count = 0

                    vehicle_inside_last_seen_ts = now_ts
                    vehicle_inside_hit_count += 1

                else:
                    if (
                        vehicle_inside_last_seen_ts <= 0.0 or
                        (now_ts - vehicle_inside_last_seen_ts) > VEHICLE_STABLE_GRACE_SEC
                    ):
                        vehicle_inside_started_ts = 0.0
                        vehicle_inside_last_seen_ts = 0.0
                        vehicle_inside_hit_count = 0
                        vehicle_confirm_hits = max(0, vehicle_confirm_hits - 1)

                vehicle_stable_inside_sec = 0.0

                if vehicle_inside_started_ts > 0.0 and stable_vehicle_candidates:
                    vehicle_stable_inside_sec = max(0.0, now_ts - vehicle_inside_started_ts)

                confirmed_vehicle = (
                    stable_vehicle_count > 0 and
                    vehicle_stable_inside_sec >= VEHICLE_STABLE_INSIDE_SEC and
                    vehicle_inside_hit_count >= VEHICLE_MIN_STABLE_HITS
                )

                display_detected = confirmed_vehicle

                update_state(lambda s: s.update({
                    "last_vehicle": {
                        "detected": display_detected,
                        "time": now_str() if raw_vehicle_count > 0 else s["last_vehicle"].get("time"),
                        "image": s["last_vehicle"].get("image"),
                        "count": raw_vehicle_count,
                        "best_confidence": best_vehicle_conf,
                        "labels": vehicle_labels,
                        "image_status": s["last_vehicle"].get("image_status"),
                        "image_file": s["last_vehicle"].get("image_file"),
                        "image_uploaded_path": s["last_vehicle"].get("image_uploaded_path"),
                        "mode": vehicle_detection_mode,
                        "geofence_overlap": best_geofence_overlap,
                        "stable_inside_sec": round(vehicle_stable_inside_sec, 1),
                        "stable_hit_count": vehicle_inside_hit_count,
                        "stable_required_sec": VEHICLE_STABLE_INSIDE_SEC,
                        "stable_vehicle_count": stable_vehicle_count,
                    }
                }))

                is_new_vehicle = confirmed_vehicle and (not vehicle_present_latch)

                should_fire_vehicle_event = (
                    is_new_vehicle and
                    (now_ts - last_vehicle_event_ts) >= VEHICLE_COOLDOWN_SEC
                )

                if should_fire_vehicle_event:
                    now = now_dt()
                    event_ts = time.time()

                    vehicles_for_event = stable_vehicle_candidates
                    marked_vehicle = draw_vehicle_annotations(frame, vehicles_for_event)

                    vehicle_snapshot = f"{DEVICE_ID}_Vehicle_{now.strftime('%Y%m%d_%H%M%S')}.jpg"
                    rel_img, path = save_capture(marked_vehicle, vehicle_snapshot, garbage=False)
                    remote_vehicle_image_path = f"{SFTP_VEHICLE_DIR.rstrip('/')}/{vehicle_snapshot}"

                    update_state(lambda s: s.update({
                        "last_vehicle": {
                            "detected": True,
                            "time": now.strftime("%Y-%m-%d %H:%M:%S"),
                            "image": rel_img,
                            "count": len(vehicles_for_event),
                            "best_confidence": best_vehicle_conf,
                            "labels": vehicle_labels,
                            "image_status": "uploading",
                            "image_file": vehicle_snapshot,
                            "image_uploaded_path": remote_vehicle_image_path,
                            "mode": vehicle_detection_mode,
                            "geofence_overlap": best_geofence_overlap,
                            "stable_inside_sec": round(vehicle_stable_inside_sec, 1),
                            "stable_hit_count": vehicle_inside_hit_count,
                            "stable_required_sec": VEHICLE_STABLE_INSIDE_SEC,
                            "stable_vehicle_count": len(vehicles_for_event),
                        }
                    }))

                    event_key = init_vehicle_event_result(
                        event_time=now,
                        vehicle_count=len(vehicles_for_event),
                        best_confidence=best_vehicle_conf,
                        vehicle_labels=vehicle_labels,
                    )

                    async_upload_vehicle_image(
                        local_path=path,
                        remote_dir=SFTP_VEHICLE_DIR,
                        remote_name=vehicle_snapshot,
                        event_time=now,
                        vehicle_count=len(vehicles_for_event),
                        best_confidence=best_vehicle_conf,
                        vehicle_labels=vehicle_labels,
                        rel_img=rel_img,
                        vehicle_mode=vehicle_detection_mode,
                        geofence_overlap=best_geofence_overlap,
                    )

                    if vehicle_video_recording_enabled:
                        threading.Thread(
                            target=handle_vehicle_video_event,
                            args=(
                                now,
                                event_ts,
                                len(vehicles_for_event),
                                best_vehicle_conf,
                                vehicle_labels
                            ),
                            daemon=True
                        ).start()
                    else:
                        update_state(lambda s: s.update({
                            "last_vehicle_video": {
                                "file": None,
                                "time": now.strftime("%Y-%m-%d %H:%M:%S"),
                                "status": "skipped_disabled_by_mqtt",
                                "duration_sec": 0,
                                "vehicle_count": len(vehicles_for_event),
                                "best_confidence": best_vehicle_conf,
                                "labels": vehicle_labels,
                            }
                        }))

                        update_vehicle_event_result(
                            event_key,
                            video_done=True,
                            video_ok=False,
                            video_file=None,
                            video_path=None,
                            video_status="skipped",
                            video_message="Vehicle video recording disabled by MQTT command",
                            duration_sec=0,
                        )
                        maybe_publish_vehicle_combined_response(event_key)

                        add_event("vehicle_video_recording_skipped", {
                            "reason": "vehicle video recording disabled by mqtt command",
                            "vehicle_count": len(vehicles_for_event),
                            "vehicle_labels": vehicle_labels,
                            "best_confidence": best_vehicle_conf,
                        })

                    add_event("vehicle_detected", {
                        "mode": vehicle_detection_mode,
                        "count": len(vehicles_for_event),
                        "raw_count": raw_vehicle_count,
                        "stable_count": stable_vehicle_count,
                        "labels": vehicle_labels,
                        "best_confidence": best_vehicle_conf,
                        "geofence_overlap": best_geofence_overlap,
                        "stable_inside_sec": round(vehicle_stable_inside_sec, 1),
                        "snapshot": rel_img,
                        "image_path": remote_vehicle_image_path,
                    })

                    last_vehicle_event_ts = now_ts
                    vehicle_present_latch = True

                if confirmed_vehicle:
                    vehicle_present_latch = True

                if raw_vehicle_count <= 0:
                    if (now_ts - last_seen_vehicle_ts) >= VEHICLE_LOST_TIMEOUT_SEC:
                        vehicle_present_latch = False
                        vehicle_confirm_hits = 0
                        vehicle_inside_started_ts = 0.0
                        vehicle_inside_last_seen_ts = 0.0
                        vehicle_inside_hit_count = 0

                        update_state(lambda s: s.update({
                            "last_vehicle": {
                                "detected": False,
                                "time": s["last_vehicle"].get("time"),
                                "image": s["last_vehicle"].get("image"),
                                "count": s["last_vehicle"].get("count", 0),
                                "best_confidence": s["last_vehicle"].get("best_confidence", 0.0),
                                "labels": s["last_vehicle"].get("labels", []),
                                "image_status": s["last_vehicle"].get("image_status"),
                                "image_file": s["last_vehicle"].get("image_file"),
                                "image_uploaded_path": s["last_vehicle"].get("image_uploaded_path"),
                                "mode": vehicle_detection_mode,
                                "geofence_overlap": s["last_vehicle"].get("geofence_overlap", 0.0),
                                "stable_inside_sec": 0.0,
                                "stable_hit_count": 0,
                                "stable_required_sec": VEHICLE_STABLE_INSIDE_SEC,
                                "stable_vehicle_count": 0,
                            }
                        }))

            time.sleep(0.005)

        except Exception as exc:
            update_state(lambda s: s["system"].update({"last_error": f"Detector loop error: {exc}"}))
            time.sleep(0.1)
		
		
def run_garbage_detection_normal_mode(
    triggered_by: str = "manual",
    create_live_link: bool = True
) -> Optional[Dict[str, Any]]:
    global garbage_clean_streak

    frame = get_frame_copy()
    if frame is None:
        return None

    reference_frame = _load_reference_frame()
    reference_ready = reference_frame is not None

    _, roi_box = get_garbage_roi(frame)

    marked_frame = draw_garbage_boxes(
        frame=frame,
        roi_box=roi_box,
        detections=[],
        severity="NONE",
        change_boxes=[]
    )

    if not reference_ready:
        cv2.putText(
            marked_frame,
            "Reference Missing - Send Garbage Sample Capture",
            (20, 35),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.75,
            (0, 0, 255),
            2,
            cv2.LINE_AA
        )

    now = now_dt()
    filename = f"{DEVICE_ID}_{now.strftime('%Y%m%d_%H%M%S')}.jpg"
    rel_img, path = save_capture(marked_frame, filename, garbage=True)

    garbage_clean_streak = 0
    live_payload = build_live_links_payload(force_new=create_live_link)

    mqtt_payload = {
        "device_id": DEVICE_ID,
        "timestamp": now.isoformat(),
        "triggered_by": triggered_by,
        "garbage_detection_mode": "normal",
        "mode": "normal",
        "ai_mode": False,
        "normal_mode": True,
        "severity": "NONE",
        "diff_ratio": 0.0,
        "edge_ratio": 0.0,
        "blob_area_ratio": 0.0,
        "blob_count": 0,
        "garbage_count": 0,
        "garbage_labels": [],
        "reference_ready": reference_ready,
        "image_file": filename,
        "camera_name": CAMERA_NAME,
        **build_common_links_payload(),
        **live_payload
    }

    async_send(path, SFTP_GARBAGE_DIR, filename, MQTT_GARBAGE_TOPIC, mqtt_payload)

    update_state(lambda s: s.update({
        "counts": {
            "HIGH": 0,
            "MEDIUM": 0,
            "LOW": 0
        },
        "last_garbage": {
            "detected": False,
            "time": now.strftime("%Y-%m-%d %H:%M:%S"),
            "image": rel_img,
            "severity": "NONE" if reference_ready else "REFERENCE_MISSING",
            "diff_ratio": 0.0,
            "edge_ratio": 0.0,
            "blob_area_ratio": 0.0,
            "blob_count": 0,
            "triggered_by": triggered_by,
            "reference_items": [],
            "mode": "normal"
        }
    }))

    add_event("garbage_normal_capture", {
        "image": rel_img,
        "severity": "NONE" if reference_ready else "REFERENCE_MISSING",
        "blob_count": 0,
        "triggered_by": triggered_by,
        "mode": "normal",
        "reference_ready": reference_ready
    })

    return mqtt_payload


def run_garbage_detection(triggered_by: str = "manual", create_live_link: bool = True) -> Optional[Dict[str, Any]]:
    global garbage_clean_streak

    if is_power_saving_active():
        return None

    if not is_feature_enabled("garbage_detection_enabled"):
        add_event("garbage_detection_skipped", {
            "triggered_by": triggered_by,
            "reason": "garbage detection disabled by mqtt command"
        })
        return None

    garbage_mode = load_garbage_detection_mode()

    if garbage_mode == "normal":
        return run_garbage_detection_normal_mode(
            triggered_by=triggered_by,
            create_live_link=create_live_link
        )

    result = analyze_garbage_stable()
    if result is None:
        return None

    now = now_dt()
    filename = f"{DEVICE_ID}_{now.strftime('%Y%m%d_%H%M%S')}.jpg"
    rel_img, path = save_capture(result["marked_frame"], filename, garbage=True)

    if result.get("ref_missing"):
        severity = "NONE"
        garbage_clean_streak = 0
    else:
        severity = result["severity"]
        if severity == "NONE":
            garbage_clean_streak += 1
        else:
            garbage_clean_streak = 0

    live_payload = build_live_links_payload(force_new=create_live_link)

    mqtt_payload = {
        "device_id": DEVICE_ID,
        "timestamp": now.isoformat(),
        "triggered_by": triggered_by,
        "garbage_detection_mode": "ai",
        "mode": "ai",
        "ai_mode": True,
        "normal_mode": False,
        "severity": severity,
        "diff_ratio": result["diff_ratio"],
        "edge_ratio": result["edge_ratio"],
        "blob_area_ratio": result["blob_area_ratio"],
        "blob_count": result["blob_count"],
        "garbage_count": result["blob_count"],
        "garbage_labels": result.get("garbage_labels", []),
        "reference_ready": not bool(result.get("ref_missing", False)),
        "image_file": filename,
        "camera_name": CAMERA_NAME,
        **build_common_links_payload(),
        **live_payload
    }

    async_send(path, SFTP_GARBAGE_DIR, filename, MQTT_GARBAGE_TOPIC, mqtt_payload)

    update_state(lambda s: s.update({
        "counts": {
            "HIGH": 1 if severity == "HIGH" else 0,
            "MEDIUM": 1 if severity == "MEDIUM" else 0,
            "LOW": 1 if severity == "LOW" else 0
        },
        "last_garbage": {
            "detected": (severity != "NONE") and (not result.get("ref_missing", False)),
            "time": now.strftime("%Y-%m-%d %H:%M:%S"),
            "image": rel_img,
            "severity": severity if not result.get("ref_missing", False) else "REFERENCE_MISSING",
            "diff_ratio": result["diff_ratio"],
            "edge_ratio": result["edge_ratio"],
            "blob_area_ratio": result["blob_area_ratio"],
            "blob_count": result["blob_count"],
            "triggered_by": triggered_by,
            "reference_items": result.get("garbage_labels", []),
            "mode": "ai"
        }
    }))

    add_event("garbage", {
        "image": rel_img,
        "severity": severity,
        "blob_count": result["blob_count"],
        "triggered_by": triggered_by,
        "mode": "ai"
    })

    return mqtt_payload


def garbage_loop() -> None:
    time.sleep(GARBAGE_START_DELAY_SEC)

    while not stop_event.is_set():
        if is_power_saving_active():
            time.sleep(1)
            continue

        if not is_feature_enabled("garbage_detection_enabled"):
            time.sleep(1)
            continue

        try:
            run_garbage_detection(triggered_by="scheduled", create_live_link=True)
        except Exception as exc:
            print(f"Scheduled garbage detection failed: {exc}", flush=True)

        for _ in range(GARBAGE_INTERVAL_SEC):
            if stop_event.is_set():
                return
            if is_power_saving_active():
                break
            if not is_feature_enabled("garbage_detection_enabled"):
                break
            time.sleep(1)


def cleanup_loop() -> None:
    while not stop_event.is_set():
        cutoff = time.time() - RETENTION_SECONDS

        for folder in [FILES_DIR, GARBAGE_DIR, AUDIO_DIR, TMP_DIR]:
            if folder.exists():
                for p in folder.glob("*"):
                    try:
                        if p.is_file() and p.stat().st_mtime < cutoff:
                            p.unlink(missing_ok=True)
                    except Exception:
                        pass

        for _ in range(CLEANUP_INTERVAL_SEC):
            if stop_event.is_set():
                return
            time.sleep(1)


def heartbeat_loop() -> None:
    while not stop_event.is_set():
        try:
            refresh_runtime_links_in_state()
            refresh_device_metrics_in_state()
        except Exception:
            pass

        for _ in range(HEARTBEAT_INTERVAL_SEC):
            if stop_event.is_set():
                return
            time.sleep(1)


def state_flusher_loop() -> None:
    while not stop_event.is_set():
        try:
            flush_state(False)
        except Exception:
            pass
        time.sleep(5)


def validate_command(payload: Dict[str, Any]) -> bool:
    if not MQTT_COMMAND_TOKEN:
        return True
    return str(payload.get("token", "")).strip() == MQTT_COMMAND_TOKEN


def normalize_command(raw: str) -> str:
    original = str(raw or "").strip()
    cmd = re.sub(r"\s+", " ", original.lower())

    if cmd.startswith("play default audio - "):
        return cmd

    mapping = {
        "live stream": LIVE_LINK_COMMAND,
        "garbage detect": MANUAL_GARBAGE_COMMAND,
        "garbage sample capture": GARBAGE_SAMPLE_CAPTURE_COMMAND,

        "garbage ai mode": GARBAGE_AI_MODE_COMMAND,
        "garbage ai": GARBAGE_AI_MODE_COMMAND,
        "garbage detection ai mode": GARBAGE_AI_MODE_COMMAND,
        "garbage detection ai": GARBAGE_AI_MODE_COMMAND,
        "ai garbage mode": GARBAGE_AI_MODE_COMMAND,

        "garbage normal mode": GARBAGE_NORMAL_MODE_COMMAND,
        "garbage normal": GARBAGE_NORMAL_MODE_COMMAND,
        "garbage detection normal mode": GARBAGE_NORMAL_MODE_COMMAND,
        "garbage detection normal": GARBAGE_NORMAL_MODE_COMMAND,
        "normal garbage mode": GARBAGE_NORMAL_MODE_COMMAND,

        "audio file play": AUDIO_PLAY_COMMAND,
        "import default audio": IMPORT_DEFAULT_AUDIO_COMMAND,
        "erase default audio in pi": ERASE_DEFAULT_AUDIO_IN_PI_COMMAND,
        "data status": DATA_STATUS_COMMAND,
        "software version": SOFTWARE_VERSION_COMMAND,
        "device restart": DEVICE_RESTART_COMMAND,
        "warning audio disable": WARNING_AUDIO_DISABLE_COMMAND,
        "warning audio enable": WARNING_AUDIO_ENABLE_COMMAND,
        "scheduled warning audio": WARNING_AUDIO_SCHEDULE_COMMAND,
        "scheduled warning disabled": WARNING_AUDIO_SCHEDULE_DISABLE_COMMAND,
        "power saving enable": POWER_SAVING_ENABLE_COMMAND,
        "enable power saving": POWER_SAVING_ENABLE_COMMAND,
        "power saving disable": POWER_SAVING_DISABLE_COMMAND,
        "disable power saving": POWER_SAVING_DISABLE_COMMAND,
        "power saving status": POWER_SAVING_STATUS_COMMAND,
        "scheduled power saving": POWER_SAVING_SCHEDULE_COMMAND,
        "power saving schedule": POWER_SAVING_SCHEDULE_COMMAND,
        "scheduled power saving disabled": POWER_SAVING_SCHEDULE_DISABLE_COMMAND,
        "power saving schedule disabled": POWER_SAVING_SCHEDULE_DISABLE_COMMAND,

        "person detection enable": PERSON_DETECTION_ENABLE_COMMAND,
        "enable person detection": PERSON_DETECTION_ENABLE_COMMAND,
        "person detection disable": PERSON_DETECTION_DISABLE_COMMAND,
        "disable person detection": PERSON_DETECTION_DISABLE_COMMAND,

        "vehicle detection enable": VEHICLE_DETECTION_ENABLE_COMMAND,
        "enable vehicle detection": VEHICLE_DETECTION_ENABLE_COMMAND,
        "vehicle detection disable": VEHICLE_DETECTION_DISABLE_COMMAND,
        "disable vehicle detection": VEHICLE_DETECTION_DISABLE_COMMAND,

        "garbage detection enable": GARBAGE_DETECTION_ENABLE_COMMAND,
        "enable garbage detection": GARBAGE_DETECTION_ENABLE_COMMAND,
        "garbage detection disable": GARBAGE_DETECTION_DISABLE_COMMAND,
        "disable garbage detection": GARBAGE_DETECTION_DISABLE_COMMAND,

        "person video recording enable": PERSON_VIDEO_RECORDING_ENABLE_COMMAND,
        "enable person video recording": PERSON_VIDEO_RECORDING_ENABLE_COMMAND,
        "person video recording disable": PERSON_VIDEO_RECORDING_DISABLE_COMMAND,
        "disable person video recording": PERSON_VIDEO_RECORDING_DISABLE_COMMAND,

        "vehicle video recording enable": VEHICLE_VIDEO_RECORDING_ENABLE_COMMAND,
        "enable vehicle video recording": VEHICLE_VIDEO_RECORDING_ENABLE_COMMAND,
        "vehicle video recording disable": VEHICLE_VIDEO_RECORDING_DISABLE_COMMAND,
        "disable vehicle video recording": VEHICLE_VIDEO_RECORDING_DISABLE_COMMAND,
    }
    return mapping.get(cmd, cmd)


def extract_garbage_interval_sec(command_text: str) -> Optional[int]:
    text = re.sub(r"\s+", " ", str(command_text or "").strip().lower())

    m = re.match(
        r"^garbage detection set to (\d+)\s*(second|seconds|sec|secs|minute|minutes|min|mins|hour|hours|hr|hrs)$",
        text
    )
    if not m:
        return None

    value = int(m.group(1))
    unit = m.group(2)

    if value <= 0:
        return None

    if unit in {"second", "seconds", "sec", "secs"}:
        sec = value
    elif unit in {"minute", "minutes", "min", "mins"}:
        sec = value * 60
    else:
        sec = value * 3600

    return max(60, sec)


def save_garbage_interval_sec(interval_sec: int) -> None:
    global CONFIG, GARBAGE_INTERVAL_SEC

    interval_sec = max(60, int(interval_sec))
    cfg = load_config()
    cfg["garbage_interval_sec"] = interval_sec
    safe_write_json(CONFIG_FILE, cfg)

    CONFIG = cfg
    GARBAGE_INTERVAL_SEC = interval_sec

    update_state(lambda s: s["system"].update({
        "garbage_interval_sec": interval_sec
    }), force_flush=True)


def resolve_audio_remote_path(payload: Dict[str, Any]) -> Tuple[Optional[str], Optional[str]]:
    audio_path = str(payload.get("audio_path", "")).strip()
    audio_name = str(payload.get("audio_name", "")).strip()

    if audio_path:
        base_name = Path(audio_path).name
        return audio_path, base_name

    if audio_name:
        return f"{SFTP_AUDIO_DIR.rstrip('/')}/{audio_name}", audio_name

    return None, None

def parse_clock_time(value: str) -> Optional[dt.time]:
    raw = re.sub(r"\s+", "", str(value or "").strip().upper())
    if not raw:
        return None

    for fmt in ("%I%p", "%I:%M%p", "%H:%M", "%H%M"):
        try:
            return dt.datetime.strptime(raw, fmt).time()
        except ValueError:
            continue
    return None


def extract_warning_schedule(payload: Dict[str, Any]) -> Tuple[Optional[str], Optional[str]]:
    start_time = str(payload.get("start_time", "")).strip()
    end_time = str(payload.get("end_time", "")).strip()

    if start_time and end_time:
        return start_time, end_time

    schedule_text = str(
        payload.get("schedule", "") or
        payload.get("time_range", "") or
        payload.get("value", "")
    ).strip()

    if schedule_text:
        m = re.match(r"(.+?)\s+to\s+(.+)", schedule_text, flags=re.IGNORECASE)
        if m:
            return m.group(1).strip(), m.group(2).strip()

    return None, None


def is_current_time_in_range(start_t: dt.time, end_t: dt.time, now_t: Optional[dt.time] = None) -> bool:
    current = now_t or now_dt().time()

    if start_t <= end_t:
        return start_t <= current <= end_t

    return current >= start_t or current <= end_t


def is_warning_audio_allowed() -> Tuple[bool, str]:
    state = load_state()
    warning_cfg = state.get("warning_audio", {})

    if not bool(warning_cfg.get("enabled", True)):
        return False, "warning audio disabled"

    if not bool(warning_cfg.get("schedule_enabled", False)):
        return True, "warning audio enabled without schedule"

    start_raw = str(warning_cfg.get("start_time", "10:00"))
    end_raw = str(warning_cfg.get("end_time", "18:00"))
    start_t = parse_clock_time(start_raw)
    end_t = parse_clock_time(end_raw)

    if start_t is None or end_t is None:
        return False, "warning audio schedule invalid"

    if is_current_time_in_range(start_t, end_t):
        return True, f"within scheduled range {start_raw} to {end_raw}"

    return False, f"outside scheduled range {start_raw} to {end_raw}"


def enqueue_audio_job(job: Dict[str, Any]) -> int:
    audio_queue.put(job)
    try:
        return audio_queue.qsize()
    except NotImplementedError:
        return -1


def play_audio_file(local_source: Path) -> None:
    ext = local_source.suffix.lower()
    playable_wav = local_source

    if ext != ".wav":
        playable_wav = TMP_DIR / f"{local_source.stem}.wav"
        subprocess.check_call([
            "ffmpeg", "-y", "-i", str(local_source),
            "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
            str(playable_wav)
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    ok = play_with_aplay(playable_wav, timeout_sec=120)
    if not ok:
        raise RuntimeError(f"failed to play audio: {local_source.name}")

    if playable_wav != local_source and playable_wav.exists():
        playable_wav.unlink(missing_ok=True)


def audio_worker_loop() -> None:
    while not stop_event.is_set():
        if is_live_talk_active():
            try:
                stop_current_audio_process()
            except Exception:
                pass

            cleared_count = clear_audio_queue()
            if cleared_count > 0:
                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": None,
                        "source_path": None,
                        "time": now_str(),
                        "status": f"queue_cleared_live_talk_active ({cleared_count})"
                    }
                }))

            time.sleep(1)
            continue

        if is_power_saving_active():
            try:
                stop_current_audio_process()
            except Exception:
                pass

            cleared_count = clear_audio_queue()
            if cleared_count > 0:
                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": None,
                        "source_path": None,
                        "time": now_str(),
                        "status": f"queue_cleared_power_saving ({cleared_count})"
                    }
                }))

            time.sleep(1)
            continue

        if is_network_audio_muted():
            try:
                stop_current_audio_process()
            except Exception:
                pass

            cleared_count = clear_audio_queue()
            if cleared_count > 0:
                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": None,
                        "source_path": None,
                        "time": now_str(),
                        "status": f"queue_cleared_network_offline ({cleared_count})"
                    }
                }))

            time.sleep(1)
            continue

        try:
            job = audio_queue.get(timeout=1)
        except queue.Empty:
            continue

        if is_live_talk_active() or is_power_saving_active() or is_network_audio_muted():
            audio_queue.task_done()
            continue

        local_file: Optional[Path] = None
        cleanup_local_file = False

        try:
            job_type = str(job.get("job_type", "")).strip().lower()

            if job_type == "warning":
                wav = ASSET_DIR / "warning.wav"
                if not wav.exists():
                    raise RuntimeError(f"Missing warning file: {wav}")

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": wav.name,
                        "source_path": str(wav),
                        "time": now_str(),
                        "status": "playing_warning"
                    }
                }))

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "warning_audio_started",
                    "file": wav.name
                })

                ok = play_with_aplay(wav, timeout_sec=20)
                if not ok:
                    raise RuntimeError("warning audio play failed")

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": wav.name,
                        "source_path": str(wav),
                        "time": now_str(),
                        "status": "warning_completed"
                    }
                }))
                add_event("warning_audio", {"file": wav.name, "status": "completed"})

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "warning_audio_completed",
                    "file": wav.name
                })

            elif job_type == "file":
                remote_path = str(job["remote_path"])
                file_name = str(job["file_name"])
                local_file = AUDIO_DIR / file_name
                cleanup_local_file = True

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": file_name,
                        "source_path": remote_path,
                        "time": now_str(),
                        "status": "downloading"
                    }
                }))

                sftp_download(remote_path, local_file)

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": file_name,
                        "source_path": remote_path,
                        "time": now_str(),
                        "status": "playing"
                    }
                }))

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "audio_play_started",
                    "file": file_name,
                    "source_path": remote_path
                })

                play_audio_file(local_file)

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": file_name,
                        "source_path": remote_path,
                        "time": now_str(),
                        "status": "completed"
                    }
                }))

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "audio_play_completed",
                    "file": file_name,
                    "source_path": remote_path
                })
                add_event("audio_play", {
                    "file": file_name,
                    "source_path": remote_path,
                    "status": "completed"
                })

            elif job_type == "local_default":
                file_name = str(job["file_name"])
                local_file = Path(str(job["local_path"]))
                cleanup_local_file = False

                if not local_file.exists() or not local_file.is_file():
                    raise RuntimeError(f"default audio file not found: {local_file}")

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": file_name,
                        "source_path": str(local_file),
                        "time": now_str(),
                        "status": "playing_default_audio"
                    }
                }))

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "default_audio_play_started",
                    "file": file_name,
                    "local_path": str(local_file)
                })

                play_audio_file(local_file)

                update_state(lambda s: s.update({
                    "last_audio": {
                        "file": file_name,
                        "source_path": str(local_file),
                        "time": now_str(),
                        "status": "default_audio_completed"
                    }
                }))

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "default_audio_play_completed",
                    "file": file_name,
                    "local_path": str(local_file)
                })

                add_event("default_audio_play", {
                    "file": file_name,
                    "local_path": str(local_file),
                    "status": "completed"
                })

            else:
                raise RuntimeError(f"unknown audio job type: {job_type}")

        except Exception as exc:
            update_state(lambda s: s.update({
                "last_audio": {
                    "file": job.get("file_name"),
                    "source_path": job.get("remote_path") or job.get("local_path"),
                    "time": now_str(),
                    "status": f"failed: {exc}"
                }
            }))

            event_name = "warning_audio_failed"
            if str(job.get("job_type", "")).lower() == "file":
                event_name = "audio_play_failed"
            elif str(job.get("job_type", "")).lower() == "local_default":
                event_name = "default_audio_play_failed"

            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": event_name,
                "file": job.get("file_name"),
                "source_path": job.get("remote_path") or job.get("local_path"),
                "message": str(exc)
            })
        finally:
            try:
                if cleanup_local_file and local_file and local_file.exists():
                    local_file.unlink(missing_ok=True)
            except Exception:
                pass
            audio_queue.task_done()

def play_audio_file(local_source: Path) -> None:
    ext = local_source.suffix.lower()
    playable_wav = local_source

    if ext != ".wav":
        playable_wav = TMP_DIR / f"{local_source.stem}.wav"
        subprocess.check_call([
            "ffmpeg", "-y", "-i", str(local_source),
            "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le",
            str(playable_wav)
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    play_with_aplay(playable_wav, timeout_sec=120)

    if playable_wav != local_source and playable_wav.exists():
        playable_wav.unlink(missing_ok=True)


def handle_audio_play(payload: Dict[str, Any]) -> None:
    remote_path, file_name = resolve_audio_remote_path(payload)
    if not remote_path or not file_name:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "audio_play_failed",
            "message": "audio_name or audio_path required"
        })
        return

    queue_size = enqueue_audio_job({
        "job_type": "file",
        "remote_path": remote_path,
        "file_name": file_name,
        "requested_at": now_str()
    })

    update_state(lambda s: s.update({
        "last_audio": {
            "file": file_name,
            "source_path": remote_path,
            "time": now_str(),
            "status": f"queued ({queue_size})"
        }
    }))

    mqtt_publish(MQTT_RESPONSE_TOPIC, {
        "device_id": DEVICE_ID,
        "event": "audio_play_queued",
        "file": file_name,
        "source_path": remote_path,
        "queue_size": queue_size
    })

    add_event("audio_play_queued", {
        "file": file_name,
        "source_path": remote_path,
        "queue_size": queue_size
    })

def is_allowed_audio_file(file_name: str) -> bool:
    ext = Path(file_name).suffix.lower()
    return ext in {".mp3", ".wav", ".aac", ".m4a", ".flac", ".ogg"}


def list_sftp_files(remote_dir: str) -> List[str]:
    transport = None
    sftp = None
    try:
        transport = paramiko.Transport((SFTP_HOST, SFTP_PORT))
        transport.connect(username=SFTP_USERNAME, password=SFTP_PASSWORD)
        sftp = paramiko.SFTPClient.from_transport(transport)

        names: List[str] = []
        for entry in sftp.listdir_attr(remote_dir):
            if not is_allowed_audio_file(entry.filename):
                continue
            names.append(entry.filename)

        names.sort()
        update_state(lambda s: s["system"].update({
            "sftp_last_ok": now_str(),
            "sftp_last_error": None
        }))
        return names

    except Exception as exc:
        update_state(lambda s: s["system"].update({
            "sftp_last_error": str(exc)
        }))
        raise
    finally:
        try:
            if sftp:
                sftp.close()
        except Exception:
            pass
        try:
            if transport:
                transport.close()
        except Exception:
            pass


def import_default_audio_to_pi() -> None:
    DEFAULT_AUDIO_DIR.mkdir(parents=True, exist_ok=True)

    try:
        remote_files = list_sftp_files(SFTP_DEFAULT_AUDIO_DIR)
        if not remote_files:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "default_audio_import_completed",
                "imported_count": 0,
                "files": [],
                "message": f"No audio files found in {SFTP_DEFAULT_AUDIO_DIR}"
            })
            add_event("default_audio_import_completed", {
                "imported_count": 0,
                "files": []
            })
            return

        imported_files: List[str] = []
        skipped_files: List[str] = []

        for file_name in remote_files:
            remote_path = f"{SFTP_DEFAULT_AUDIO_DIR.rstrip('/')}/{file_name}"
            local_path = DEFAULT_AUDIO_DIR / file_name

            if local_path.exists() and local_path.is_file():
                skipped_files.append(file_name)
                continue

            sftp_download(remote_path, local_path)
            imported_files.append(file_name)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "default_audio_import_completed",
            "imported_count": len(imported_files),
            "skipped_count": len(skipped_files),
            "imported_files": imported_files,
            "skipped_files": skipped_files,
            "message": "Default audio imported to PI successfully"
        })

        add_event("default_audio_import_completed", {
            "imported_count": len(imported_files),
            "skipped_count": len(skipped_files),
            "imported_files": imported_files,
            "skipped_files": skipped_files
        })

    except Exception as exc:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "default_audio_import_failed",
            "message": str(exc)
        })
        add_event("default_audio_import_failed", {
            "message": str(exc)
        })


def erase_default_audio_in_pi() -> None:
    DEFAULT_AUDIO_DIR.mkdir(parents=True, exist_ok=True)

    deleted_files: List[str] = []
    failed_files: List[str] = []

    for path in sorted(DEFAULT_AUDIO_DIR.iterdir()):
        if not path.is_file():
            continue
        if not is_allowed_audio_file(path.name):
            continue

        try:
            path.unlink()
            deleted_files.append(path.name)
        except Exception:
            failed_files.append(path.name)

    mqtt_publish(MQTT_RESPONSE_TOPIC, {
        "device_id": DEVICE_ID,
        "event": "default_audio_erase_completed",
        "deleted_count": len(deleted_files),
        "failed_count": len(failed_files),
        "deleted_files": deleted_files,
        "failed_files": failed_files,
        "message": "Default audio files erased from PI"
    })

    add_event("default_audio_erase_completed", {
        "deleted_count": len(deleted_files),
        "failed_count": len(failed_files),
        "deleted_files": deleted_files,
        "failed_files": failed_files
    })


def extract_default_audio_name(command_text: str) -> str:
    raw = str(command_text or "").strip()
    lower_raw = raw.lower()
    if not lower_raw.startswith(PLAY_DEFAULT_AUDIO_COMMAND_PREFIX):
        return ""
    file_name = raw[len("Play Default Audio - "):].strip()
    return Path(file_name).name.strip()


def handle_play_default_audio(payload: Dict[str, Any]) -> None:
    command_text = str(payload.get("command", "")).strip()
    file_name = extract_default_audio_name(command_text)

    if not file_name:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "default_audio_play_failed",
            "message": "Command format: Play Default Audio - Alert_Aud-1.mp3"
        })
        return

    if not is_allowed_audio_file(file_name):
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "default_audio_play_failed",
            "file": file_name,
            "message": "Unsupported audio file extension"
        })
        return

    local_file = DEFAULT_AUDIO_DIR / file_name
    if not local_file.exists() or not local_file.is_file():
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "default_audio_play_failed",
            "file": file_name,
            "message": "File not found in PI default audio folder. Run 'Import Default Audio' first."
        })
        return

    queue_size = enqueue_audio_job({
        "job_type": "local_default",
        "local_path": str(local_file),
        "file_name": file_name,
        "requested_at": now_str()
    })

    update_state(lambda s: s.update({
        "last_audio": {
            "file": file_name,
            "source_path": str(local_file),
            "time": now_str(),
            "status": f"default_audio_queued ({queue_size})"
        }
    }))

    mqtt_publish(MQTT_RESPONSE_TOPIC, {
        "device_id": DEVICE_ID,
        "event": "default_audio_play_queued",
        "file": file_name,
        "local_path": str(local_file),
        "queue_size": queue_size
    })

    add_event("default_audio_play_queued", {
        "file": file_name,
        "local_path": str(local_file),
        "queue_size": queue_size
    })

def delayed_reboot() -> None:
    update_state(lambda s: s["system"].update({"last_restart_request_at": now_str()}), force_flush=True)
    time.sleep(2)
    stop_event.set()
    try:
        flush_state(True)
    except Exception:
        pass
    try:
        stop_live_tunnel()
    except Exception:
        pass
    try:
        subprocess.Popen(["sudo", "/usr/local/bin/gcam-force-reboot.sh"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception as exc:
        try:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "device_restart_failed",
                "message": str(exc)
            })
        except Exception:
            pass


router_reboot_gpio_initialized = False


def run_pinctrl_set(level: int) -> None:
    # active-low setup:
    # level 1 => drive HIGH
    # level 0 => drive LOW
    drive = "dh" if level == 1 else "dl"

    result = subprocess.run(
        [
            "pinctrl",
            "set",
            str(ROUTER_REBOOT_GPIO),
            "op",
            drive,
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        raise RuntimeError(
            f"pinctrl failed for GPIO {ROUTER_REBOOT_GPIO}="
            f"{level}: {result.stderr.strip()}"
        )


def init_router_reboot_gpio() -> None:
    global router_reboot_gpio_initialized

    # Put GPIO in known idle state at startup
    run_pinctrl_set(ROUTER_REBOOT_INACTIVE_VALUE)
    router_reboot_gpio_initialized = True


def pulse_router_reboot_gpio() -> None:
    if not router_reboot_gpio_initialized:
        init_router_reboot_gpio()

    # Ensure idle state first
    run_pinctrl_set(ROUTER_REBOOT_INACTIVE_VALUE)
    time.sleep(0.2)

    # Stage 1
    run_pinctrl_set(ROUTER_REBOOT_ACTIVE_VALUE)
    time.sleep(ROUTER_REBOOT_PULSE_SEC)
    run_pinctrl_set(ROUTER_REBOOT_INACTIVE_VALUE)

    # Gap between stage 1 and stage 2
    time.sleep(ROUTER_REBOOT_STAGE_GAP_SEC)

    # Stage 2
    run_pinctrl_set(ROUTER_REBOOT_ACTIVE_VALUE)
    time.sleep(ROUTER_REBOOT_PULSE_SEC)
    run_pinctrl_set(ROUTER_REBOOT_INACTIVE_VALUE)

def trigger_router_reboot_gpio(triggered_by: str) -> bool:
    if not router_reboot_lock.acquire(blocking=False):
        try:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "router_reboot_trigger_skipped",
                "triggered_by": triggered_by,
                "message": "GPIO trigger already in progress"
            })
        except Exception:
            pass
        return False

    try:
        init_router_reboot_gpio()

        try:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "router_reboot_trigger_started",
                "triggered_by": triggered_by,
                "gpio": ROUTER_REBOOT_GPIO,
                "duration_sec": ROUTER_REBOOT_PULSE_SEC,
                "time": now_str()
            })
        except Exception:
            pass

        pulse_router_reboot_gpio()

        try:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "router_reboot_trigger_completed",
                "triggered_by": triggered_by,
                "gpio": ROUTER_REBOOT_GPIO,
                "duration_sec": ROUTER_REBOOT_PULSE_SEC,
                "time": now_str()
            })
        except Exception:
            pass

        try:
            add_event("router_reboot_triggered", {
                "triggered_by": triggered_by,
                "gpio": ROUTER_REBOOT_GPIO,
                "duration_sec": ROUTER_REBOOT_PULSE_SEC
            })
        except Exception:
            pass

        return True

    except Exception as exc:
        try:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "router_reboot_trigger_failed",
                "triggered_by": triggered_by,
                "message": str(exc),
                "time": now_str()
            })
        except Exception:
            pass
        return False

    finally:
        router_reboot_lock.release()


def trigger_router_reboot_async(triggered_by: str) -> None:
    threading.Thread(
        target=trigger_router_reboot_gpio,
        args=(triggered_by,),
        daemon=True
    ).start()


def router_reboot_schedule_loop() -> None:
    global last_router_reboot_schedule_key

    while not stop_event.is_set():
        now = dt.datetime.now()
        hhmm = now.strftime("%H:%M")
        schedule_key = now.strftime("%Y-%m-%d %H:%M")

        if hhmm in ROUTER_REBOOT_SCHEDULE_TIMES and last_router_reboot_schedule_key != schedule_key:
            last_router_reboot_schedule_key = schedule_key
            trigger_router_reboot_async(triggered_by=f"schedule_{hhmm}")

        time.sleep(1)


def power_saving_schedule_loop() -> None:
    while not stop_event.is_set():
        try:
            state = load_state()
            ps = state.get("power_saving", {})

            if not bool(ps.get("schedule_enabled", False)):
                time.sleep(POWER_SAVING_SCHEDULE_CHECK_SEC)
                continue

            start_t = parse_clock_time(str(ps.get("start_time", "22:00")))
            end_t = parse_clock_time(str(ps.get("end_time", "06:00")))

            if start_t is None or end_t is None:
                update_state(lambda s: s["system"].update({
                    "last_error": "Invalid power saving schedule time"
                }))
                time.sleep(POWER_SAVING_SCHEDULE_CHECK_SEC)
                continue

            now_time = dt.datetime.now().time().replace(second=0, microsecond=0)
            should_be_active = is_time_inside_range(now_time, start_t, end_t)

            currently_active = bool(ps.get("enabled", False))
            current_source = ps.get("source")

            update_state(lambda s: s["power_saving"].update({
                "last_schedule_check": now_str()
            }), force_flush=True)

            if should_be_active and not currently_active:
                status = set_power_saving_enabled(
                    enabled=True,
                    reason=f"scheduled power saving active from {start_t.strftime('%H:%M')} to {end_t.strftime('%H:%M')}",
                    source="schedule"
                )

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "power_saving_schedule_started",
                    "message": "Scheduled power saving mode started.",
                    **status
                })

            elif (not should_be_active) and currently_active and current_source == "schedule":
                status = set_power_saving_enabled(
                    enabled=False,
                    reason="scheduled power saving window ended",
                    source="schedule"
                )

                mqtt_publish(MQTT_RESPONSE_TOPIC, {
                    "device_id": DEVICE_ID,
                    "event": "power_saving_schedule_ended",
                    "message": "Scheduled power saving mode ended. Normal functions resumed.",
                    **status
                })

        except Exception as exc:
            update_state(lambda s: s["system"].update({
                "last_error": f"Power saving schedule loop error: {exc}"
            }))

        time.sleep(POWER_SAVING_SCHEDULE_CHECK_SEC)


def handle_command(payload: Dict[str, Any]) -> None:
    if not validate_command(payload):
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "command_rejected",
            "message": "Invalid token"
        })
        return

    raw_command = str(payload.get("command", "")).strip()
    cmd = normalize_command(raw_command)

    if cmd == POWER_SAVING_ENABLE_COMMAND:
        status = set_power_saving_enabled(
            enabled=True,
            reason="enabled by mqtt command",
            source="manual"
        )
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "power_saving_enabled",
            "message": "Power saving mode enabled. Camera, detection, live stream, audio and uploads are paused.",
            **status
        })
        return

    if cmd == POWER_SAVING_DISABLE_COMMAND:
        status = set_power_saving_enabled(
            enabled=False,
            reason="disabled by mqtt command",
            source="manual",
            disable_schedule=True
        )
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "power_saving_disabled",
            "message": "Power saving mode disabled. Normal camera and detection functions will resume.",
            **status
        })
        return

    if cmd == POWER_SAVING_STATUS_COMMAND:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "power_saving_status",
            **power_saving_status_payload(),
            "time": now_str()
        })
        return

    if cmd == POWER_SAVING_SCHEDULE_DISABLE_COMMAND:
        previous = load_state().get("power_saving", {})
        was_schedule_active = bool(previous.get("enabled", False)) and previous.get("source") == "schedule"

        def mutate(state):
            state.setdefault("power_saving", default_state()["power_saving"])
            state["power_saving"]["schedule_enabled"] = False
            state["power_saving"]["last_schedule_check"] = now_str()

        update_state(mutate, force_flush=True)

        if was_schedule_active:
            status = set_power_saving_enabled(
                enabled=False,
                reason="scheduled power saving disabled by mqtt command",
                source="manual"
            )
        else:
            status = power_saving_status_payload()

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "power_saving_schedule_disabled",
            "message": "Scheduled power saving disabled successfully.",
            **status
        })
        add_event("power_saving_schedule_disabled", {})
        return

    if cmd == POWER_SAVING_SCHEDULE_COMMAND:
        start_raw, end_raw = parse_power_saving_schedule(payload)
        start_t = parse_clock_time(start_raw or "")
        end_t = parse_clock_time(end_raw or "")

        if start_t is None or end_t is None:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "power_saving_schedule_failed",
                "message": "start_time and end_time are required. Example: 10PM / 6AM or 22:00 / 06:00"
            })
            return

        normalized_start = start_t.strftime("%H:%M")
        normalized_end = end_t.strftime("%H:%M")

        def mutate(state):
            state.setdefault("power_saving", default_state()["power_saving"])
            state["power_saving"]["schedule_enabled"] = True
            state["power_saving"]["start_time"] = normalized_start
            state["power_saving"]["end_time"] = normalized_end
            state["power_saving"]["last_schedule_check"] = now_str()

        update_state(mutate, force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "power_saving_schedule_updated",
            "start_time": normalized_start,
            "end_time": normalized_end,
            "message": f"Power saving scheduled from {normalized_start} to {normalized_end}",
            **power_saving_status_payload()
        })
        add_event("power_saving_schedule_updated", {
            "start_time": normalized_start,
            "end_time": normalized_end
        })
        return

    if is_power_saving_active() and not power_saving_command_allowed(cmd):
        mqtt_publish(MQTT_RESPONSE_TOPIC, build_power_saving_blocked_response(cmd))
        return

    if cmd in GARBAGE_MODE_COMMANDS:
        mode = "ai" if cmd == GARBAGE_AI_MODE_COMMAND else "normal"
        status = set_garbage_detection_mode(mode, source="mqtt command")

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "garbage_detection_mode_changed",
            "command": raw_command,
            "garbage_detection_mode": status["garbage_detection_mode"],
            "garbage_detection_mode_label": status["garbage_detection_mode_label"],
            "message": f"Garbage detection mode changed to {status['garbage_detection_mode_label']}",
            "time": now_str(),
        })
        return

    feature_command_map = {
        PERSON_DETECTION_ENABLE_COMMAND: ("person_detection_enabled", True, "person_detection_enabled"),
        PERSON_DETECTION_DISABLE_COMMAND: ("person_detection_enabled", False, "person_detection_disabled"),

        VEHICLE_DETECTION_ENABLE_COMMAND: ("vehicle_detection_enabled", True, "vehicle_detection_enabled"),
        VEHICLE_DETECTION_DISABLE_COMMAND: ("vehicle_detection_enabled", False, "vehicle_detection_disabled"),

        GARBAGE_DETECTION_ENABLE_COMMAND: ("garbage_detection_enabled", True, "garbage_detection_enabled"),
        GARBAGE_DETECTION_DISABLE_COMMAND: ("garbage_detection_enabled", False, "garbage_detection_disabled"),

        PERSON_VIDEO_RECORDING_ENABLE_COMMAND: ("person_video_recording_enabled", True, "person_video_recording_enabled"),
        PERSON_VIDEO_RECORDING_DISABLE_COMMAND: ("person_video_recording_enabled", False, "person_video_recording_disabled"),

        VEHICLE_VIDEO_RECORDING_ENABLE_COMMAND: ("vehicle_video_recording_enabled", True, "vehicle_video_recording_enabled"),
        VEHICLE_VIDEO_RECORDING_DISABLE_COMMAND: ("vehicle_video_recording_enabled", False, "vehicle_video_recording_disabled"),
    }

    if cmd in feature_command_map:
        flag_name, enabled, event_name = feature_command_map[cmd]
        flags = set_feature_flag(flag_name, enabled, source="mqtt command")

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": event_name,
            "command": raw_command,
            "flag": flag_name,
            "enabled": bool(enabled),
            "status": "enabled" if enabled else "disabled",
            "message": f"{flag_name} set to {'enabled' if enabled else 'disabled'}",
            "feature_flags": flags,
            "time": now_str(),
        })
        return

    if cmd == LIVE_LINK_COMMAND:
        live_payload = build_live_links_payload(force_new=True)
        mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "live_link_created", **live_payload})
        add_event("live_link_created", live_payload)
        return

    if cmd == MANUAL_GARBAGE_COMMAND:
        if not is_feature_enabled("garbage_detection_enabled"):
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "manual_garbage_blocked",
                "status": "disabled",
                "message": "Garbage detection is disabled by MQTT command",
                **feature_flags_payload(),
                "time": now_str(),
            })
            return

        result = run_garbage_detection(triggered_by="manual", create_live_link=True)
        if result is not None:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "manual_garbage_detected", **result})
        else:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "manual_garbage_failed", "message": "No frame available"})
        return

    if cmd == GARBAGE_SAMPLE_CAPTURE_COMMAND:
        try:
            capture_result = capture_garbage_reference_from_live()
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "garbage_sample_captured",
                **capture_result
            })
        except Exception as exc:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "garbage_sample_capture_failed",
                "message": str(exc)
            })
        return

    garbage_interval_sec = extract_garbage_interval_sec(raw_command)
    if garbage_interval_sec is not None:
        save_garbage_interval_sec(garbage_interval_sec)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "garbage_interval_updated",
            "garbage_interval_sec": garbage_interval_sec,
            "garbage_interval_minutes": round(garbage_interval_sec / 60, 2),
            "garbage_interval_hours": round(garbage_interval_sec / 3600, 2),
            "message": f"Scheduled garbage detection set to {garbage_interval_sec} seconds"
        })
        add_event("garbage_interval_updated", {
            "garbage_interval_sec": garbage_interval_sec
        })
        return

    if cmd == AUDIO_PLAY_COMMAND:
        handle_audio_play(payload)
        return

    if cmd == IMPORT_DEFAULT_AUDIO_COMMAND:
        import_default_audio_to_pi()
        return

    if cmd == ERASE_DEFAULT_AUDIO_IN_PI_COMMAND:
        erase_default_audio_in_pi()
        return

    if cmd == QUEUE_CLEAR_COMMAND:
        stop_current_audio_process()
        cleared_count = clear_audio_queue()

        update_state(lambda s: s.update({
            "last_audio": {
                "file": None,
                "source_path": None,
                "time": now_str(),
                "status": f"queue_cleared ({cleared_count})"
            }
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "queue_cleared",
            "cleared_count": cleared_count,
            "message": "Audio queue cleared successfully"
        })

        add_event("queue_cleared", {
            "cleared_count": cleared_count
        })
        return

    if str(raw_command).strip().lower().startswith(PLAY_DEFAULT_AUDIO_COMMAND_PREFIX):
        handle_play_default_audio(payload)
        return

    if cmd == CURRENT_VOLUME_COMMAND:
        handle_current_pi_volume_command(payload)
        return

    if extract_volume_percent(raw_command) is not None:
        handle_pi_volume_command(payload)
        return

    if cmd == WARNING_AUDIO_DISABLE_COMMAND:
        update_state(lambda s: s["warning_audio"].update({
            "enabled": False,
            "last_status": "disabled by mqtt command"
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "warning_audio_disabled",
            "message": "Warning audio disabled successfully"
        })
        add_event("warning_audio_disabled", {})
        return

    if cmd == WARNING_AUDIO_ENABLE_COMMAND:
        update_state(lambda s: s["warning_audio"].update({
            "enabled": True,
            "schedule_enabled": False,
            "last_status": "enabled for 24 hours by mqtt command"
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "warning_audio_enabled",
            "message": "Warning audio enabled successfully in 24 hours mode"
        })
        add_event("warning_audio_enabled", {"schedule_enabled": False})
        return

    if cmd == WARNING_AUDIO_SCHEDULE_DISABLE_COMMAND:
        update_state(lambda s: s["warning_audio"].update({
            "enabled": True,
            "schedule_enabled": False,
            "last_status": "schedule disabled, warning audio set to 24 hours mode"
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "warning_audio_schedule_disabled",
            "message": "Scheduled warning disabled successfully. Warning audio is now active for 24 hours"
        })
        add_event("warning_audio_schedule_disabled", {"schedule_enabled": False})
        return

    if cmd == WARNING_AUDIO_SCHEDULE_COMMAND:
        start_raw, end_raw = extract_warning_schedule(payload)
        start_t = parse_clock_time(start_raw or "")
        end_t = parse_clock_time(end_raw or "")

        if start_t is None or end_t is None:
            mqtt_publish(MQTT_RESPONSE_TOPIC, {
                "device_id": DEVICE_ID,
                "event": "warning_audio_schedule_failed",
                "message": "start_time and end_time are required. Example: 10AM / 6PM or 10:00 / 18:00"
            })
            return

        normalized_start = start_t.strftime("%H:%M")
        normalized_end = end_t.strftime("%H:%M")

        update_state(lambda s: s["warning_audio"].update({
            "enabled": True,
            "schedule_enabled": True,
            "start_time": normalized_start,
            "end_time": normalized_end,
            "last_status": f"scheduled {normalized_start} to {normalized_end}"
        }), force_flush=True)

        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "warning_audio_schedule_updated",
            "start_time": normalized_start,
            "end_time": normalized_end,
            "message": f"Warning audio scheduled from {normalized_start} to {normalized_end}"
        })
        add_event("warning_audio_schedule_updated", {
            "start_time": normalized_start,
            "end_time": normalized_end
        })
        return

    if cmd == DATA_STATUS_COMMAND:
        status = collect_device_status()
        mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "data_status", **status})
        add_event("data_status_requested", {
            "network_status": status["network_status"],
            "ram_percent": status["ram_percent"],
            "software_version": status["software_version"],
            "power_saving_enabled": status.get("power_saving_enabled")
        })
        return

    if cmd == SOFTWARE_VERSION_COMMAND:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "software_version",
            "software_version": SOFTWARE_VERSION,
            "software_features": SOFTWARE_FEATURES,
            "time": now_str()
        })
        add_event("software_version_requested", {
            "software_version": SOFTWARE_VERSION
        })
        return

    if cmd == ROUTER_REBOOT_COMMAND:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "router_reboot_trigger_started",
            "triggered_by": "manual_mqtt",
            "gpio": ROUTER_REBOOT_GPIO,
            "duration_sec": ROUTER_REBOOT_PULSE_SEC,
            "time": now_str()
        })
        trigger_router_reboot_async(triggered_by="manual_mqtt")
        return

    if cmd == DEVICE_RESTART_COMMAND:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "device_restart_started", "time": now_str()})
        add_event("device_restart_requested", {})
        threading.Thread(target=delayed_reboot, daemon=True).start()
        return

    if cmd in {"close the software", "open the software", "camera temp url"}:
        mqtt_publish(MQTT_RESPONSE_TOPIC, {
            "device_id": DEVICE_ID,
            "event": "command_forwarded_to_controller",
            "message": cmd
        })
        return

    mqtt_publish(MQTT_RESPONSE_TOPIC, {"device_id": DEVICE_ID, "event": "unknown_command", "message": cmd})


def mqtt_command_loop() -> None:
    def on_connect(client, userdata, flags, reason_code, properties=None):
        client.subscribe(MQTT_COMMAND_TOPIC, qos=1)
        update_state(lambda s: s["system"].update({"command_listener": True}))

    def on_message(client, userdata, msg):
        try:
            raw = msg.payload.decode("utf-8", errors="ignore").strip()
            payload = json.loads(raw) if raw.startswith("{") else {"command": raw}
            handle_command(payload)
        except Exception as exc:
            print(f"MQTT command processing failed: {exc}", flush=True)

    while not stop_event.is_set():
        client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
        client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)
        client.on_connect = on_connect
        client.on_message = on_message

        try:
            client.connect(MQTT_HOST, MQTT_PORT, 20)
            client.loop_start()

            while not stop_event.is_set():
                time.sleep(1)

        except Exception as exc:
            update_state(lambda s: s["system"].update({"command_listener": False, "mqtt_last_error": str(exc)}))
        finally:
            try:
                client.loop_stop()
            except Exception:
                pass
            try:
                client.disconnect()
            except Exception:
                pass

        time.sleep(5)


def mjpeg_stream():
    while not stop_event.is_set():
        if is_power_saving_active():
            time.sleep(1)
            continue

        frame = get_frame_copy()
        if frame is None:
            time.sleep(0.2)
            continue

        ok, jpg = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), MJPEG_QUALITY])
        if ok:
            yield (
                b"--frame\r\n"
                b"Content-Type: image/jpeg\r\n\r\n" + jpg.tobytes() + b"\r\n"
            )
        time.sleep(0.06)


def render_dashboard(initial_tab: str = "live", geofence_page_only: bool = False):
    state = load_state()
    return render_template(
        "index.html",
        state=state,
        ts=int(time.time()),
        local_dashboard=local_dashboard_url(),
        local_stream=local_stream_url(),
        sftp_base_dir=SFTP_BASE_DIR,
        sftp_person_dir=SFTP_PERSON_DIR,
        sftp_person_video_dir=SFTP_PERSON_VIDEO_DIR,
        sftp_garbage_dir=SFTP_GARBAGE_DIR,
        sftp_audio_dir=SFTP_AUDIO_DIR,
        initial_tab=initial_tab,
        geofence_page_only=geofence_page_only
    )


@app.route("/")
def index():
    return render_dashboard("live", geofence_page_only=False)


@app.route("/garbage_geofencing")
def garbage_geofencing_page():
    return render_dashboard("geofence", geofence_page_only=True)


@app.route("/person_geofencing")
def person_geofencing_page():
    return render_dashboard("person_geofence", geofence_page_only=True)


@app.route("/vehicle_geofencing")
def vehicle_geofencing_page():
    return render_dashboard("vehicle_geofence", geofence_page_only=True)


@app.route("/live_talk")
def live_talk_page():
    return render_template(
        "live_talk.html",
        device_id=DEVICE_ID,
        command_token=MQTT_COMMAND_TOKEN,
        chunk_ms=int(LIVE_TALK_CHUNK_SECONDS * 1000)
    )


@app.route("/api/live_talk/start", methods=["POST"])
def api_live_talk_start():
    if not live_talk_token_valid(request):
        return jsonify({
            "ok": False,
            "error": "invalid_token"
        }), 403

    if is_power_saving_active():
        return jsonify({
            "ok": False,
            "error": "power_saving_active"
        }), 409

    if is_network_audio_muted():
        return jsonify({
            "ok": False,
            "error": "network_audio_muted"
        }), 409

    LIVE_TALK_DIR.mkdir(parents=True, exist_ok=True)

    try:
        stop_current_audio_process()
    except Exception:
        pass

    try:
        clear_audio_queue()
    except Exception:
        pass

    set_live_talk_active(False)
    set_live_talk_stopping(False)
    clear_live_talk_queue()
    stop_live_talk_processes()

    set_live_talk_active(True)

    if not start_live_talk_pipeline():
        set_live_talk_active(False)
        return jsonify({
            "ok": False,
            "error": "pipeline_start_failed"
        }), 500

    update_state(lambda s: s["system"].update({
        "live_talk_active": True,
        "live_talk_last_started_at": now_str(),
        "live_talk_last_error": None,
        "live_talk_queue_size": 0
    }), force_flush=True)

    add_event("live_talk_started", {
        "device_id": DEVICE_ID,
        "time": now_str()
    })

    return jsonify({
        "ok": True,
        "active": True,
        "chunk_ms": int(LIVE_TALK_CHUNK_SECONDS * 1000),
        "message": "Live talk started"
    })


@app.route("/api/live_talk/chunk", methods=["POST"])
def api_live_talk_chunk():
    if not live_talk_token_valid(request):
        return jsonify({
            "ok": False,
            "error": "invalid_token"
        }), 403

    if is_power_saving_active():
        return jsonify({
            "ok": False,
            "error": "power_saving_active"
        }), 409

    if is_network_audio_muted():
        return jsonify({
            "ok": False,
            "error": "network_audio_muted"
        }), 409

    if not is_live_talk_active():
        return jsonify({
            "ok": False,
            "error": "live_talk_not_active"
        }), 409

    if is_live_talk_stopping():
        return jsonify({
            "ok": False,
            "error": "live_talk_is_stopping"
        }), 409

    audio_file = request.files.get("audio")
    if audio_file is None:
        return jsonify({
            "ok": False,
            "error": "missing_audio_file"
        }), 400

    try:
        audio_bytes = audio_file.read()
    except Exception as exc:
        return jsonify({
            "ok": False,
            "error": f"audio_read_failed: {exc}"
        }), 400

    ok, message = enqueue_live_talk_chunk(audio_bytes)
    if not ok:
        update_state(lambda s: s["system"].update({
            "live_talk_last_error": message
        }))

        status_code = 429 if message == "live_talk_queue_full" else 400
        return jsonify({
            "ok": False,
            "error": message
        }), status_code

    update_state(lambda s: s["system"].update({
        "live_talk_active": True,
        "live_talk_last_chunk_at": now_str(),
        "live_talk_last_error": None,
        "live_talk_queue_size": live_talk_queue.qsize()
    }))

    return jsonify({
        "ok": True,
        "queued": True,
        "queue_size": live_talk_queue.qsize()
    })


@app.route("/api/live_talk/stop", methods=["POST"])
def api_live_talk_stop():
    if not live_talk_token_valid(request):
        return jsonify({
            "ok": False,
            "error": "invalid_token"
        }), 403

    data = request.get_json(force=False, silent=True) or {}
    mode = str(
        data.get("mode")
        or request.form.get("mode")
        or request.args.get("mode")
        or "graceful"
    ).strip().lower()

    if mode == "hard":
        set_live_talk_active(False)
        set_live_talk_stopping(False)
        cleared_count = clear_live_talk_queue()
        stop_live_talk_processes(graceful=False)

        update_state(lambda s: s["system"].update({
            "live_talk_active": False,
            "live_talk_draining": False,
            "live_talk_last_stopped_at": now_str(),
            "live_talk_queue_size": 0
        }), force_flush=True)

        add_event("live_talk_hard_stopped", {
            "device_id": DEVICE_ID,
            "cleared_count": cleared_count,
            "time": now_str()
        })

        return jsonify({
            "ok": True,
            "active": False,
            "draining": False,
            "cleared_count": cleared_count,
            "message": "Live talk hard stopped"
        })

    if is_live_talk_stopping():
        return jsonify({
            "ok": True,
            "active": is_live_talk_active(),
            "draining": True,
            "queue_size": live_talk_queue.qsize(),
            "message": "Live talk already draining"
        })

    set_live_talk_stopping(True)

    update_state(lambda s: s["system"].update({
        "live_talk_active": True,
        "live_talk_draining": True,
        "live_talk_last_stop_requested_at": now_str(),
        "live_talk_queue_size": live_talk_queue.qsize()
    }), force_flush=True)

    threading.Thread(
        target=finish_live_talk_after_drain,
        daemon=True
    ).start()

    add_event("live_talk_graceful_stop_requested", {
        "device_id": DEVICE_ID,
        "queue_size": live_talk_queue.qsize(),
        "time": now_str()
    })

    return jsonify({
        "ok": True,
        "active": True,
        "draining": True,
        "queue_size": live_talk_queue.qsize(),
        "message": "Live talk draining remaining audio"
    })


@app.route("/api/live_talk/status", methods=["GET"])
def api_live_talk_status():
    return jsonify({
        "ok": True,
        "active": is_live_talk_active(),
        "pipeline_running": is_live_talk_pipeline_running(),
        "network_audio_muted": is_network_audio_muted(),
        "power_saving_active": is_power_saving_active(),
        "queue_size": live_talk_queue.qsize()
    })


def is_cloudflare_live_request(host: str) -> bool:
    host = (host or "").split(":")[0].strip().lower()
    permanent_host = ""

    if PERMANENT_LIVE_BASE_URL:
        permanent_host = PERMANENT_LIVE_BASE_URL.replace("https://", "").replace("http://", "").strip().lower().rstrip("/")

    return (
        host.endswith(".trycloudflare.com") or
        (permanent_host and host == permanent_host)
    )


@app.route("/live")
def live():
    host = request.host or ""
    stream_endpoint = "/video_feed_cloudflare" if is_cloudflare_live_request(host) else "/video_feed"
    return render_template("live.html", stream_endpoint=stream_endpoint)


@app.route("/video_feed")
def video_feed():
    if is_power_saving_active():
        return Response("Power saving mode active. Live stream disabled.", status=503)
    return Response(mjpeg_stream(), mimetype="multipart/x-mixed-replace; boundary=frame")


@app.route("/video_feed_cloudflare")
def video_feed_cloudflare():
    if is_power_saving_active():
        return Response("Power saving mode active. Cloudflare live stream disabled.", status=503)
    return Response(
        mjpeg_stream_from_rtsp(CLOUDFLARE_LIVE_RTSP_URL),
        mimetype="multipart/x-mixed-replace; boundary=frame"
    )


@app.route("/api/state")
def api_state():
    payload = load_state()
    payload["links"]["dashboard_local"] = local_dashboard_url()
    payload["links"]["stream_local"] = local_stream_url()
    payload["links"]["dashboard_public"] = PERMANENT_DASHBOARD_URL
    payload["links"]["stream_public"] = PERMANENT_LIVE_URL
    payload["sftp_base_dir"] = SFTP_BASE_DIR
    payload["sftp_person_dir"] = SFTP_PERSON_DIR
    payload["sftp_person_video_dir"] = SFTP_PERSON_VIDEO_DIR
    payload["sftp_garbage_dir"] = SFTP_GARBAGE_DIR
    payload["sftp_audio_dir"] = SFTP_AUDIO_DIR
    payload["public_ip"] = detect_public_ip()
    payload["local_ip"] = detect_local_ip()
    payload["camera_config_url"] = CAMERA_CONFIG_URL
    payload["garbage_detection_mode"] = load_garbage_detection_mode()
    payload["garbage_detection_mode_label"] = garbage_detection_mode_label(payload["garbage_detection_mode"])
    payload["vehicle_detection_mode"] = load_vehicle_detection_mode()
    payload["vehicle_geofence_overlap_threshold"] = load_vehicle_geofence_overlap_threshold()

    payload.setdefault("system", {})
    payload["system"]["garbage_detection_mode"] = payload["garbage_detection_mode"]
    payload["system"]["garbage_mode"] = payload["garbage_detection_mode_label"]
    payload["system"]["vehicle_detection_mode"] = payload["vehicle_detection_mode"]
    payload["system"]["vehicle_geofence_overlap_threshold"] = payload["vehicle_geofence_overlap_threshold"]

    payload.setdefault("last_vehicle", {})
    payload["last_vehicle"].setdefault("mode", payload["vehicle_detection_mode"])
    payload["last_vehicle"].setdefault("geofence_overlap", 0.0)

    if PERMANENT_LIVE_URL:
        payload["live_link"] = {
            "active": True,
            "url": PERMANENT_LIVE_URL,
            "expires_at": "Never"
        }

    return jsonify(payload)


def dashboard_bool_from_payload(data: Dict[str, Any], default: bool = False) -> bool:
    raw = data.get("enabled", data.get("value", default))

    if isinstance(raw, bool):
        return raw

    if isinstance(raw, (int, float)):
        return bool(raw)

    text = str(raw).strip().lower()
    return text in {"1", "true", "yes", "on", "enable", "enabled"}


def dashboard_controls_payload(include_volume: bool = True) -> Dict[str, Any]:
    state = load_state()
    payload = {
        "ok": True,
        **feature_flags_payload(),
        **garbage_detection_mode_payload(),
        "warning_audio": state.get("warning_audio", {}),
        "power_saving": state.get("power_saving", {}),
        **power_saving_status_payload(),
        "audio_queue_size": audio_queue.qsize() if hasattr(audio_queue, "qsize") else None,
    }

    if include_volume:
        try:
            payload["volume"] = {
                "ok": True,
                **get_current_pi_volume()
            }
        except Exception as exc:
            payload["volume"] = {
                "ok": False,
                "error": str(exc)
            }

    return payload


@app.route("/api/controls/state", methods=["GET"])
def api_controls_state():
    return jsonify(dashboard_controls_payload(include_volume=True))


@app.route("/api/controls/feature", methods=["POST"])
def api_controls_feature_set():
    data = request.get_json(force=True, silent=True) or {}
    flag_name = str(data.get("flag", "")).strip()
    enabled = dashboard_bool_from_payload(data)

    if flag_name not in DEFAULT_FEATURE_FLAGS:
        return jsonify({
            "ok": False,
            "error": f"Unknown feature flag: {flag_name}",
            "allowed_flags": sorted(DEFAULT_FEATURE_FLAGS.keys())
        }), 400

    flags = set_feature_flag(flag_name, enabled, source="dashboard controls")

    return jsonify({
        "ok": True,
        "flag": flag_name,
        "enabled": bool(enabled),
        "feature_flags": flags,
        **dashboard_controls_payload(include_volume=False)
    })


@app.route("/api/controls/warning_audio", methods=["POST"])
def api_controls_warning_audio_set():
    data = request.get_json(force=True, silent=True) or {}
    enabled = dashboard_bool_from_payload(data)

    if enabled:
        status = "enabled for 24 hours by dashboard controls"
        event_name = "warning_audio_enabled"
        update_state(lambda s: s["warning_audio"].update({
            "enabled": True,
            "schedule_enabled": False,
            "last_status": status
        }), force_flush=True)
        add_event(event_name, {"source": "dashboard controls", "schedule_enabled": False})
    else:
        status = "disabled by dashboard controls"
        event_name = "warning_audio_disabled"
        update_state(lambda s: s["warning_audio"].update({
            "enabled": False,
            "last_status": status
        }), force_flush=True)
        add_event(event_name, {"source": "dashboard controls"})

    return jsonify({
        "ok": True,
        "enabled": enabled,
        "status": status,
        **dashboard_controls_payload(include_volume=False)
    })
@app.route("/api/controls/garbage/mode", methods=["POST"])
def api_controls_garbage_mode_set():
    data = request.get_json(force=True, silent=True) or {}
    raw_mode = str(data.get("mode", data.get("garbage_detection_mode", ""))).strip().lower()

    if raw_mode not in GARBAGE_DETECTION_MODES:
        return jsonify({
            "ok": False,
            "error": f"Unknown garbage detection mode: {raw_mode}",
            "allowed_modes": sorted(GARBAGE_DETECTION_MODES)
        }), 400

    status = set_garbage_detection_mode(raw_mode, source="dashboard controls")

    return jsonify({
        "ok": True,
        "message": f"Garbage detection mode changed to {status['garbage_detection_mode_label']}",
        **status,
        **dashboard_controls_payload(include_volume=False)
    })


@app.route("/api/controls/garbage/manual_detect", methods=["POST"])
def api_controls_garbage_manual_detect():
    if is_power_saving_active():
        return jsonify({
            "ok": False,
            "error": "Power saving mode is active. Disable power saving before manual garbage detection."
        }), 409

    if not is_feature_enabled("garbage_detection_enabled"):
        return jsonify({
            "ok": False,
            "error": "Garbage detection is disabled. Enable garbage detection first.",
            **feature_flags_payload()
        }), 409

    result = run_garbage_detection(triggered_by="dashboard_manual", create_live_link=True)

    if result is None:
        return jsonify({
            "ok": False,
            "error": "Manual garbage detection failed. No frame/result available."
        }), 500

    return jsonify({
        "ok": True,
        "message": "Manual garbage detection completed",
        "result": result,
        **dashboard_controls_payload(include_volume=False)
    })


@app.route("/api/controls/garbage/sample_capture", methods=["POST"])
def api_controls_garbage_sample_capture():
    if is_power_saving_active():
        return jsonify({
            "ok": False,
            "error": "Power saving mode is active. Disable power saving before sample capture."
        }), 409

    try:
        result = capture_garbage_reference_from_live()
        return jsonify({
            "ok": True,
            "message": "Garbage sample captured successfully",
            "result": result,
            **dashboard_controls_payload(include_volume=False)
        })
    except Exception as exc:
        return jsonify({
            "ok": False,
            "error": str(exc)
        }), 500


@app.route("/api/controls/audio/clear_queue", methods=["POST"])
def api_controls_audio_clear_queue():
    try:
        stop_current_audio_process()
    except Exception:
        pass

    cleared_count = clear_audio_queue()

    update_state(lambda s: s.update({
        "last_audio": {
            "file": None,
            "source_path": None,
            "time": now_str(),
            "status": f"queue_cleared_dashboard ({cleared_count})"
        }
    }), force_flush=True)

    add_event("queue_cleared", {
        "source": "dashboard controls",
        "cleared_count": cleared_count
    })

    return jsonify({
        "ok": True,
        "cleared_count": cleared_count,
        "message": "Audio queue cleared successfully",
        **dashboard_controls_payload(include_volume=False)
    })


@app.route("/api/controls/power_saving", methods=["POST"])
def api_controls_power_saving_set():
    data = request.get_json(force=True, silent=True) or {}
    enabled = dashboard_bool_from_payload(data)

    result = set_power_saving_enabled(
        enabled=enabled,
        reason="dashboard controls",
        source="dashboard controls",
        disable_schedule=True
    )

    return jsonify({
        "ok": True,
        "enabled": enabled,
        **result,
        **dashboard_controls_payload(include_volume=False)
    })


@app.route("/api/controls/volume", methods=["GET", "POST"])
def api_controls_volume():
    if request.method == "GET":
        try:
            result = get_current_pi_volume()
            return jsonify({
                "ok": True,
                **result
            })
        except Exception as exc:
            return jsonify({
                "ok": False,
                "error": str(exc)
            }), 500

    data = request.get_json(force=True, silent=True) or {}
    percent = data.get("volume_percent", data.get("percent", data.get("volume")))

    try:
        percent = int(percent)
    except Exception:
        return jsonify({
            "ok": False,
            "error": "volume_percent must be a number from 0 to 100"
        }), 400

    try:
        result = set_pi_volume(percent)

        update_state(lambda s: s.update({
            "last_audio": {
                "file": None,
                "source_path": None,
                "time": now_str(),
                "status": f"pi_volume_set_to_{result['volume_percent']}%_dashboard"
            }
        }), force_flush=True)

        add_event("pi_volume_set", {
            "source": "dashboard controls",
            "volume_percent": result["volume_percent"],
            "requested_volume_percent": result["requested_volume_percent"]
        })

        return jsonify({
            "ok": True,
            **result
        })

    except Exception as exc:
        add_event("pi_volume_set_failed", {
            "source": "dashboard controls",
            "volume_percent": percent,
            "message": str(exc)
        })

        return jsonify({
            "ok": False,
            "error": str(exc)
        }), 500


@app.route("/api/geofence", methods=["GET"])
def api_geofence_get():
    """Return current garbage polygon geofence as ratios."""
    polygon = load_garbage_polygon()
    roi_box = ratio_polygon_to_box(polygon)

    return jsonify({
        "garbage_polygon": round_ratio_polygon(polygon),
        "garbage_roi": [round(v, 4) for v in roi_box],
        "min_points": POLYGON_MIN_POINTS,
        "max_points": POLYGON_MAX_POINTS,
        "source": "config"
    })


@app.route("/api/geofence", methods=["POST"])
def api_geofence_set():
    """Save garbage polygon geofence to config.json. Hot reload, no restart."""
    data = request.get_json(force=True, silent=True) or {}

    polygon = sanitize_ratio_polygon(data.get("garbage_polygon"))

    # Backward compatibility: accept old rectangle ROI also.
    if polygon is None:
        old_box = _valid_ratio_box(data.get("garbage_roi"))
        if old_box is not None:
            polygon = ratio_box_to_polygon(old_box)

    if polygon is None:
        return jsonify({
            "ok": False,
            "error": f"garbage_polygon must contain {POLYGON_MIN_POINTS} to {POLYGON_MAX_POINTS} points. Example: [[0.1,0.2],[0.8,0.2],[0.8,0.8],[0.1,0.8]]"
        }), 400

    cfg = load_config()
    cfg["garbage_polygon"] = round_ratio_polygon(polygon)

    # Keep old ROI key for backward compatibility with old dashboard/app versions.
    roi_box = ratio_polygon_to_box(polygon)
    cfg["garbage_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "garbage_polygon": cfg["garbage_polygon"],
        "garbage_roi": cfg["garbage_roi"]
    })


@app.route("/api/geofence/reset", methods=["POST"])
def api_geofence_reset():
    """Reset garbage polygon geofence to default."""
    polygon = [(float(x), float(y)) for x, y in DEFAULT_GARBAGE_POLYGON]

    cfg = load_config()
    cfg["garbage_polygon"] = round_ratio_polygon(polygon)

    roi_box = ratio_polygon_to_box(polygon)
    cfg["garbage_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "garbage_polygon": cfg["garbage_polygon"],
        "garbage_roi": cfg["garbage_roi"]
    })


@app.route("/api/person_geofence", methods=["GET"])
def api_person_geofence_get():
    """Return current person polygon geofence as ratios."""
    polygon = load_person_polygon()
    roi_box = ratio_polygon_to_box(polygon)

    return jsonify({
        "person_polygon": round_ratio_polygon(polygon),
        "person_roi": [round(v, 4) for v in roi_box],
        "min_points": POLYGON_MIN_POINTS,
        "max_points": POLYGON_MAX_POINTS,
        "source": "config"
    })


@app.route("/api/person_geofence", methods=["POST"])
def api_person_geofence_set():
    """Save person polygon geofence to config.json. Hot reload, no restart."""
    data = request.get_json(force=True, silent=True) or {}

    polygon = sanitize_ratio_polygon(data.get("person_polygon"))

    # Backward compatibility: accept old rectangle ROI also.
    if polygon is None:
        old_box = _valid_ratio_box(data.get("person_roi"))
        if old_box is not None:
            polygon = ratio_box_to_polygon(old_box)

    if polygon is None:
        return jsonify({
            "ok": False,
            "error": f"person_polygon must contain {POLYGON_MIN_POINTS} to {POLYGON_MAX_POINTS} points. Example: [[0.1,0.2],[0.8,0.2],[0.8,0.8],[0.1,0.8]]"
        }), 400

    cfg = load_config()
    cfg["person_polygon"] = round_ratio_polygon(polygon)

    # Keep old ROI key for backward compatibility with old dashboard/app versions.
    roi_box = ratio_polygon_to_box(polygon)
    cfg["person_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "person_polygon": cfg["person_polygon"],
        "person_roi": cfg["person_roi"]
    })


@app.route("/api/person_geofence/reset", methods=["POST"])
def api_person_geofence_reset():
    """Reset person polygon geofence to default."""
    polygon = [(float(x), float(y)) for x, y in DEFAULT_PERSON_POLYGON]

    cfg = load_config()
    cfg["person_polygon"] = round_ratio_polygon(polygon)

    roi_box = ratio_polygon_to_box(polygon)
    cfg["person_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "person_polygon": cfg["person_polygon"],
        "person_roi": cfg["person_roi"]
    })


@app.route("/api/vehicle_geofence", methods=["GET"])
def api_vehicle_geofence_get():
    """Return current vehicle polygon geofence as ratios."""
    polygon = load_vehicle_polygon()
    roi_box = ratio_polygon_to_box(polygon)

    return jsonify({
        "vehicle_polygon": round_ratio_polygon(polygon),
        "vehicle_roi": [round(v, 4) for v in roi_box],
        "min_points": POLYGON_MIN_POINTS,
        "max_points": POLYGON_MAX_POINTS,
        "overlap_threshold": load_vehicle_geofence_overlap_threshold(),
        "source": "config"
    })


@app.route("/api/vehicle_geofence", methods=["POST"])
def api_vehicle_geofence_set():
    """Save vehicle polygon geofence to config.json. Hot reload, no restart."""
    data = request.get_json(force=True, silent=True) or {}

    polygon = sanitize_ratio_polygon(data.get("vehicle_polygon"))

    # Backward compatibility: accept old rectangle ROI also.
    if polygon is None:
        old_box = _valid_ratio_box(data.get("vehicle_roi"))
        if old_box is not None:
            polygon = ratio_box_to_polygon(old_box)

    if polygon is None:
        return jsonify({
            "ok": False,
            "error": f"vehicle_polygon must contain {POLYGON_MIN_POINTS} to {POLYGON_MAX_POINTS} points. Example: [[0.1,0.2],[0.8,0.2],[0.8,0.8],[0.1,0.8]]"
        }), 400

    cfg = load_config()
    cfg["vehicle_polygon"] = round_ratio_polygon(polygon)

    # Keep old ROI key for backward compatibility with old dashboard/app versions.
    roi_box = ratio_polygon_to_box(polygon)
    cfg["vehicle_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "vehicle_polygon": cfg["vehicle_polygon"],
        "vehicle_roi": cfg["vehicle_roi"],
        "overlap_threshold": load_vehicle_geofence_overlap_threshold()
    })


@app.route("/api/vehicle_geofence/reset", methods=["POST"])
def api_vehicle_geofence_reset():
    """Reset vehicle polygon geofence to default."""
    polygon = [(float(x), float(y)) for x, y in DEFAULT_VEHICLE_POLYGON]

    cfg = load_config()
    cfg["vehicle_polygon"] = round_ratio_polygon(polygon)

    roi_box = ratio_polygon_to_box(polygon)
    cfg["vehicle_roi"] = [round(v, 4) for v in roi_box]

    safe_write_json(CONFIG_FILE, cfg)

    return jsonify({
        "ok": True,
        "vehicle_polygon": cfg["vehicle_polygon"],
        "vehicle_roi": cfg["vehicle_roi"],
        "overlap_threshold": load_vehicle_geofence_overlap_threshold()
    })


@app.route("/api/vehicle_detection_mode", methods=["GET"])
def api_vehicle_detection_mode_get():
    return jsonify({
        "ok": True,
        **vehicle_detection_mode_payload()
    })


@app.route("/api/vehicle_detection_mode", methods=["POST"])
def api_vehicle_detection_mode_set():
    data = request.get_json(force=True, silent=True) or {}
    mode = data.get("mode") or data.get("vehicle_detection_mode")
    normalized_mode = normalize_vehicle_detection_mode(mode)

    result = set_vehicle_detection_mode(normalized_mode, source="dashboard")

    return jsonify({
        "ok": True,
        **result
    })

@app.route("/api/snapshot")
def api_snapshot():
    """Return a single JPEG frame for the geofence editor."""
    if is_power_saving_active():
        return Response("Power saving mode active. Snapshot disabled.", status=503)

    frame = get_frame_copy()
    if frame is None:
        return Response("No frame", status=503)
    ok, jpg = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), 80])
    if not ok:
        return Response("Encode failed", status=500)
    return Response(jpg.tobytes(), mimetype="image/jpeg")


@app.route("/media/<path:filename>")
def media(filename):
    if filename.startswith("garbage/"):
        return send_from_directory(str(GARBAGE_DIR), filename.replace("garbage/", "", 1))
    return send_from_directory(str(FILES_DIR), filename)


def startup() -> None:
    load_state()

    try:
        init_models()
    except Exception as exc:
        update_state(lambda s: s["system"].update({"last_error": f"Model init failed: {exc}"}), True)

    try:
        init_router_reboot_gpio()
    except Exception as exc:
        update_state(lambda s: s["system"].update({"last_error": f"GPIO init failed: {exc}"}), True)

    try:
        refresh_runtime_links_in_state()
        refresh_device_metrics_in_state()
        update_state(
            lambda s: s["live_link"].update({
                "active": True if PERMANENT_LIVE_URL else False,
                "url": PERMANENT_LIVE_URL,
                "expires_at": "Never" if PERMANENT_LIVE_URL else None
            }),
            force_flush=True
        )
    except Exception:
        pass

    threading.Thread(target=reader_loop, daemon=True).start()
    threading.Thread(target=detector_loop, daemon=True).start()
    threading.Thread(target=garbage_loop, daemon=True).start()
    threading.Thread(target=cleanup_loop, daemon=True).start()
    threading.Thread(target=heartbeat_loop, daemon=True).start()
    threading.Thread(target=state_flusher_loop, daemon=True).start()
    threading.Thread(target=audio_worker_loop, daemon=True).start()
    threading.Thread(target=live_talk_worker_loop, daemon=True).start()
    threading.Thread(target=network_audio_monitor_loop, daemon=True).start()
    threading.Thread(target=mqtt_command_loop, daemon=True).start()
    threading.Thread(target=router_reboot_schedule_loop, daemon=True).start()
    threading.Thread(target=power_saving_schedule_loop, daemon=True).start()


def shutdown(*_args) -> None:
    global router_reboot_gpio_initialized

    stop_event.set()

    try:
        stop_current_audio_process()
    except Exception:
        pass

    try:
        set_live_talk_active(False)
        set_live_talk_stopping(False)
        stop_live_talk_processes()
        clear_live_talk_queue()
    except Exception:
        pass

    try:
        flush_state(True)
    except Exception:
        pass
    try:
        stop_live_tunnel()
    except Exception:
        pass

    router_reboot_gpio_initialized = False


atexit.register(shutdown)
signal.signal(signal.SIGTERM, shutdown)
signal.signal(signal.SIGINT, shutdown)

if __name__ == "__main__":
    startup()
    serve(app, host=HOST, port=PORT, threads=8)
PY

echo "[16/17] Write controller.py..."
cat > "$APP_DIR/controller.py" <<'PY'
import atexit
import datetime as dt
import json
import re
import signal
import subprocess
import threading
import time
from pathlib import Path
from typing import Any, Dict, Optional

import paho.mqtt.client as mqtt

APP_DIR = Path("/opt/gcam")
DATA_DIR = APP_DIR / "data"
CONFIG_FILE = DATA_DIR / "config.json"

CLOUDFLARED_BIN = "/usr/bin/cloudflared"
APP_SERVICE_NAME = "gcam.service"
CAMERA_TUNNEL_DURATION_SEC = 600

stop_event = threading.Event()
tunnel_lock = threading.Lock()
camera_tunnel_proc: Optional[subprocess.Popen] = None
camera_tunnel_url: Optional[str] = None
camera_tunnel_expiry_ts: float = 0.0
camera_tunnel_stop_timer: Optional[threading.Timer] = None


def load_config() -> Dict[str, Any]:
    with open(CONFIG_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


CONFIG = load_config()

DEVICE_ID = CONFIG["device_id"]
CAMERA_NAME = CONFIG["camera_name"]
CAMERA_IP = CONFIG["camera_ip"]
CAMERA_CONFIG_URL = CONFIG.get("camera_config_url", f"https://{CAMERA_IP}:443")

MQTT_HOST = CONFIG["mqtt"]["host"]
MQTT_PORT = int(CONFIG["mqtt"]["port"])
MQTT_USERNAME = CONFIG["mqtt"]["username"]
MQTT_PASSWORD = CONFIG["mqtt"]["password"]
MQTT_COMMAND_TOPIC = CONFIG["mqtt"].get("command_topic", "G-Cam-RnD/command")
MQTT_RESPONSE_TOPIC = CONFIG["mqtt"].get("response_topic", "G-Cam-RnD/device_response")
MQTT_COMMAND_TOKEN = CONFIG["mqtt"].get("command_token", "")


def now_str() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def validate_command(payload: Dict[str, Any]) -> bool:
    if not MQTT_COMMAND_TOKEN:
        return True
    return str(payload.get("token", "")).strip() == MQTT_COMMAND_TOKEN


def normalize_command(raw: str) -> str:
    cmd = re.sub(r"\s+", " ", str(raw).strip().lower())
    mapping = {
        "close the software": "close the software",
        "open the software": "open the software",
        "camera temp url": "camera temp url",
        "camera live url": "camera temp url",
        "camera permanent url": "camera temp url",
        "warning audio disable": "warning audio disable",
        "warning audio enable": "warning audio enable",
        "scheduled warning audio": "scheduled warning audio",
        "scheduled warning disabled": "scheduled warning disabled"
    }
    return mapping.get(cmd, cmd)


def mqtt_publish(payload: Dict[str, Any]) -> None:
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)
    client.connect(MQTT_HOST, MQTT_PORT, 20)
    info = client.publish(MQTT_RESPONSE_TOPIC, json.dumps(payload), qos=1, retain=False)
    info.wait_for_publish(timeout=5)
    client.disconnect()


def run_systemctl(action: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["sudo", "/usr/bin/systemctl", action, APP_SERVICE_NAME],
        capture_output=True,
        text=True,
        timeout=20
    )


def is_app_active() -> bool:
    proc = subprocess.run(
        ["sudo", "/usr/bin/systemctl", "is-active", APP_SERVICE_NAME],
        capture_output=True,
        text=True,
        timeout=10
    )
    return proc.returncode == 0 and proc.stdout.strip() == "active"


def camera_tunnel_is_active() -> bool:
    return camera_tunnel_proc is not None and camera_tunnel_proc.poll() is None and bool(camera_tunnel_url)


def stop_camera_tunnel() -> None:
    global camera_tunnel_proc, camera_tunnel_url, camera_tunnel_expiry_ts, camera_tunnel_stop_timer

    with tunnel_lock:
        if camera_tunnel_stop_timer is not None:
            try:
                camera_tunnel_stop_timer.cancel()
            except Exception:
                pass
            camera_tunnel_stop_timer = None

        if camera_tunnel_proc is not None:
            try:
                camera_tunnel_proc.terminate()
                camera_tunnel_proc.wait(timeout=5)
            except Exception:
                try:
                    camera_tunnel_proc.kill()
                except Exception:
                    pass

        camera_tunnel_proc = None
        camera_tunnel_url = None
        camera_tunnel_expiry_ts = 0.0


def start_camera_tunnel(duration_sec: int = CAMERA_TUNNEL_DURATION_SEC) -> Optional[Dict[str, Any]]:
    global camera_tunnel_proc, camera_tunnel_url, camera_tunnel_expiry_ts, camera_tunnel_stop_timer

    with tunnel_lock:
        now_ts = time.time()

        if camera_tunnel_is_active() and camera_tunnel_url:
            camera_tunnel_expiry_ts = now_ts + duration_sec

            if camera_tunnel_stop_timer is not None:
                try:
                    camera_tunnel_stop_timer.cancel()
                except Exception:
                    pass

            camera_tunnel_stop_timer = threading.Timer(duration_sec, stop_camera_tunnel)
            camera_tunnel_stop_timer.daemon = True
            camera_tunnel_stop_timer.start()

            return {
                "camera_config_local_url": CAMERA_CONFIG_URL,
                "camera_config_temp_url": camera_tunnel_url,
                "camera_config_expires_in_sec": int(max(camera_tunnel_expiry_ts - time.time(), 0)),
                "camera_config_expires_at": dt.datetime.fromtimestamp(camera_tunnel_expiry_ts).isoformat(),
                "camera_name": CAMERA_NAME,
                "device_id": DEVICE_ID
            }

        if not Path(CLOUDFLARED_BIN).exists():
            return None

        if camera_tunnel_proc is not None:
            try:
                camera_tunnel_proc.terminate()
                camera_tunnel_proc.wait(timeout=5)
            except Exception:
                try:
                    camera_tunnel_proc.kill()
                except Exception:
                    pass

        cmd = [
            CLOUDFLARED_BIN,
            "tunnel",
            "--url", CAMERA_CONFIG_URL,
            "--no-autoupdate",
            "--no-tls-verify"
        ]

        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1
        )

        found_url = None
        deadline = time.time() + 30
        pattern = re.compile(r"https://[a-z0-9-]+\.trycloudflare\.com", re.IGNORECASE)

        while time.time() < deadline:
            line = proc.stdout.readline()
            if not line:
                if proc.poll() is not None:
                    break
                time.sleep(0.2)
                continue

            match = pattern.search(line)
            if match:
                found_url = match.group(0).rstrip("/")
                break

        if not found_url:
            try:
                proc.terminate()
            except Exception:
                pass
            return None

        camera_tunnel_proc = proc
        camera_tunnel_url = found_url
        camera_tunnel_expiry_ts = time.time() + duration_sec

        if camera_tunnel_stop_timer is not None:
            try:
                camera_tunnel_stop_timer.cancel()
            except Exception:
                pass

        camera_tunnel_stop_timer = threading.Timer(duration_sec, stop_camera_tunnel)
        camera_tunnel_stop_timer.daemon = True
        camera_tunnel_stop_timer.start()

        return {
            "camera_config_local_url": CAMERA_CONFIG_URL,
            "camera_config_temp_url": found_url,
            "camera_config_expires_in_sec": int(max(camera_tunnel_expiry_ts - time.time(), 0)),
            "camera_config_expires_at": dt.datetime.fromtimestamp(camera_tunnel_expiry_ts).isoformat(),
            "camera_name": CAMERA_NAME,
            "device_id": DEVICE_ID
        }


def handle_close_software() -> None:
    before_active = is_app_active()
    result = run_systemctl("stop")
    after_active = is_app_active()

    mqtt_publish({
        "device_id": DEVICE_ID,
        "event": "software_closed",
        "requested_at": now_str(),
        "was_active_before": before_active,
        "is_active_after": after_active,
        "service": APP_SERVICE_NAME,
        "return_code": result.returncode,
        "stdout": result.stdout.strip(),
        "stderr": result.stderr.strip()
    })


def handle_open_software() -> None:
    before_active = is_app_active()
    result = run_systemctl("start")
    time.sleep(2)
    after_active = is_app_active()

    mqtt_publish({
        "device_id": DEVICE_ID,
        "event": "software_opened",
        "requested_at": now_str(),
        "was_active_before": before_active,
        "is_active_after": after_active,
        "service": APP_SERVICE_NAME,
        "return_code": result.returncode,
        "stdout": result.stdout.strip(),
        "stderr": result.stderr.strip()
    })

def handle_camera_temp_url() -> None:
    payload = start_camera_tunnel(CAMERA_TUNNEL_DURATION_SEC)
    if payload is None:
        mqtt_publish({
            "device_id": DEVICE_ID,
            "event": "camera_temp_url_failed",
            "requested_at": now_str(),
            "camera_config_local_url": CAMERA_CONFIG_URL,
            "message": "Unable to create Cloudflare camera temporary URL"
        })
        return

    mqtt_publish({
        "event": "camera_temp_url_created",
        "requested_at": now_str(),
        **payload
    })

def handle_command(payload: Dict[str, Any]) -> None:
    if not validate_command(payload):
        mqtt_publish({
            "device_id": DEVICE_ID,
            "event": "command_rejected",
            "requested_at": now_str(),
            "message": "Invalid token"
        })
        return

    cmd = normalize_command(payload.get("command", ""))

    if cmd == "close the software":
        handle_close_software()
        return

    if cmd == "open the software":
        handle_open_software()
        return

    if cmd == "camera temp url":
        handle_camera_temp_url()
        return

def mqtt_loop() -> None:
    def on_connect(client, userdata, flags, reason_code, properties=None):
        client.subscribe(MQTT_COMMAND_TOPIC, qos=1)

    def on_message(client, userdata, msg):
        try:
            raw = msg.payload.decode("utf-8", errors="ignore").strip()
            payload = json.loads(raw) if raw.startswith("{") else {"command": raw}
            handle_command(payload)
        except Exception as exc:
            try:
                mqtt_publish({
                    "device_id": DEVICE_ID,
                    "event": "controller_error",
                    "requested_at": now_str(),
                    "message": str(exc)
                })
            except Exception:
                pass

    while not stop_event.is_set():
        client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
        client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)
        client.on_connect = on_connect
        client.on_message = on_message

        try:
            client.connect(MQTT_HOST, MQTT_PORT, 20)
            client.loop_start()
            while not stop_event.is_set():
                time.sleep(1)
        except Exception:
            pass
        finally:
            try:
                client.loop_stop()
            except Exception:
                pass
            try:
                client.disconnect()
            except Exception:
                pass

        time.sleep(5)


def shutdown(*_args) -> None:
    stop_event.set()
    try:
        stop_camera_tunnel()
    except Exception:
        pass


atexit.register(shutdown)
signal.signal(signal.SIGTERM, shutdown)
signal.signal(signal.SIGINT, shutdown)

if __name__ == "__main__":
    mqtt_loop()
PY

echo "[17/17] Write state, services and timers..."
cat > "$APP_DIR/data/state.json" <<JSON
{
  "app_name": "G-Cam \"RealTech Systems\"",
  "started_at": null,
  "camera_ip": "$CAMERA_IP",
  "router_ip": "$ROUTER_IP",
  "raspberry_ip": "$PI_IP",
  "device_id": "$DEVICE_ID",
  "camera_name": "$CAMERA_NAME",
  "camera_config_url": "$CAMERA_CONFIG_URL",
  "public_ip": null,
  "system": {
    "camera_connected": false,
    "last_frame_at": null,
    "last_error": null,
    "sftp_last_ok": null,
    "sftp_last_error": null,
    "mqtt_last_ok": null,
    "mqtt_last_error": null,
    "command_listener": false,
    "garbage_mode": "hybrid_reference_filtered",
    "pi_temperature_c": null,
    "network_status": null,
    "network_iface": null,
    "network_link_speed_mbps": null,
    "network_rx_bps": 0,
    "network_tx_bps": 0,
    "battery_voltage": null,
    "battery_current_a": null,
    "battery_current_ma": null,
    "battery_status": null,
    "ram_total_mb": null,
    "ram_used_mb": null,
    "ram_percent": null,
    "geo_location": null,
    "last_restart_request_at": null
  },
  "links": {
    "dashboard_local": null,
    "stream_local": null
  },
  "live_link": {
    "active": false,
    "url": null,
    "expires_at": null
  },
  "counts": {
    "HIGH": 0,
    "MEDIUM": 0,
    "LOW": 0
  },
  "last_person": {
    "detected": false,
    "time": null,
    "image": null,
	"count": 0,
	"best_confidence": 0.0
  },
  "last_garbage": {
    "detected": false,
    "time": null,
    "image": null,
    "severity": null,
    "diff_ratio": 0.0,
    "edge_ratio": 0.0,
    "blob_area_ratio": 0.0,
    "blob_count": 0,
    "triggered_by": null,
    "reference_items": [
      "Dust",
      "Bags",
      "Covers",
      "Carry bags",
      "Box",
      "Leaf",
      "Slippers",
      "Shoes",
      "Paper",
      "Notebooks",
      "Cloths",
      "Dress"
    ]
  },
  "last_audio": {
    "file": null,
    "source_path": null,
    "time": null,
    "status": null
  },
  "events": []
}
JSON

sudo tee "$MAINT_SCRIPT" >/dev/null <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
LOG_FILE="/var/log/gcam-maintenance.log"
{
  echo "=================================================="
  echo "G-Cam maintenance started at $(date '+%Y-%m-%d %H:%M:%S')"
  apt-get update
  apt-get -y autoremove
  apt-get -y autoclean
  journalctl --vacuum-time=3d || true
  sync
  echo "G-Cam maintenance completed at $(date '+%Y-%m-%d %H:%M:%S')"
} >> "$LOG_FILE" 2>&1
EOF
sudo chmod +x "$MAINT_SCRIPT"

sudo tee "$MAINT_SERVICE" >/dev/null <<EOF
[Unit]
Description=G-Cam Maintenance Job
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=$MAINT_SCRIPT
EOF

sudo tee "$MAINT_TIMER" >/dev/null <<EOF
[Unit]
Description=Run G-Cam maintenance twice daily

[Timer]
OnCalendar=*-*-* 02:45:00
OnCalendar=*-*-* 14:45:00
Persistent=true
Unit=gcam-maintenance.service

[Install]
WantedBy=timers.target
EOF

sudo tee "$APP_RESTART_SERVICE" >/dev/null <<EOF
[Unit]
Description=Restart G-Cam app service

[Service]
Type=oneshot
ExecStart=/usr/bin/systemctl restart gcam.service
EOF

sudo tee "$APP_RESTART_TIMER" >/dev/null <<EOF
[Unit]
Description=Restart G-Cam app every 3 hours

[Timer]
OnCalendar=*-*-* 00,03,06,09,12,15,18,21:00:00
Persistent=true
Unit=gcam-app-restart.service

[Install]
WantedBy=timers.target
EOF

sudo tee "$PI_REBOOT_SERVICE" >/dev/null <<EOF
[Unit]
Description=Reboot Raspberry Pi for G-Cam stability

[Service]
Type=oneshot
ExecStart=/usr/bin/systemctl reboot
EOF

sudo tee "$PI_REBOOT_TIMER" >/dev/null <<EOF
[Unit]
Description=Reboot Raspberry Pi twice daily

[Timer]
OnCalendar=*-*-* ${REBOOT_TIME_1}
OnCalendar=*-*-* ${REBOOT_TIME_2}
Persistent=true
Unit=gcam-pi-reboot.service

[Install]
WantedBy=timers.target
EOF

sudo tee "$SERVICE_FILE" >/dev/null <<EOF
[Unit]
Description=G-Cam RealTech Systems
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$PI_USER
WorkingDirectory=$APP_DIR
Environment=PYTHONUNBUFFERED=1
ExecStart=$VENV_DIR/bin/python $APP_DIR/app.py
Restart=always
RestartSec=5
TimeoutStopSec=10
KillSignal=SIGINT
KillMode=mixed

[Install]
WantedBy=multi-user.target
EOF
sudo tee "$CONTROLLER_SERVICE_FILE" >/dev/null <<EOF
[Unit]
Description=G-Cam MQTT Controller
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$PI_USER
WorkingDirectory=$APP_DIR
Environment=PYTHONUNBUFFERED=1
ExecStart=$VENV_DIR/bin/python $APP_DIR/controller.py
Restart=always
RestartSec=5
TimeoutStopSec=10
KillSignal=SIGINT
KillMode=mixed

[Install]
WantedBy=multi-user.target
EOF

sudo chown -R "$PI_USER:$PI_USER" "$APP_DIR"

echo "[INFO] Camera config will use temporary Cloudflare URL only."
echo "[INFO] Permanent live URL remains: ${PERMANENT_LIVE_BASE_URL}/live"
echo "[INFO] Local camera config URL remains: ${CAMERA_CONFIG_URL}"


echo "[PATCH] Create Cloudflare ping script..."
sudo tee "$CLOUDFLARE_PING_SCRIPT" >/dev/null <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

LOG_FILE="/opt/gcam/logs/cloudflare_ping.log"

mkdir -p /opt/gcam/logs

while true; do
  TS="$(date '+%Y-%m-%d %H:%M:%S')"

  if ping -c 1 -W 5 cloudflare.com >/dev/null 2>&1; then
    echo "$TS OK cloudflare.com reachable" >> "$LOG_FILE"
  else
    echo "$TS FAIL cloudflare.com unreachable" >> "$LOG_FILE"
  fi

  sleep 15
done
EOF
sudo chmod +x "$CLOUDFLARE_PING_SCRIPT"

echo "[PATCH] Create Cloudflare ping service..."
sudo tee "$CLOUDFLARE_PING_SERVICE" >/dev/null <<EOF
[Unit]
Description=GCam Cloudflare Ping Monitor
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
ExecStart=$CLOUDFLARE_PING_SCRIPT
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF


echo "[FINAL] Python compile check..."
sudo -u "$PI_USER" "$VENV_DIR/bin/python" -m py_compile "$APP_DIR/app.py"
sudo -u "$PI_USER" "$VENV_DIR/bin/python" -m py_compile "$APP_DIR/controller.py"

sudo systemctl daemon-reload
sudo systemctl enable gcam.service
sudo systemctl enable gcam-cloudflare-ping.service
sudo systemctl restart gcam.service
sudo systemctl restart gcam-cloudflare-ping.service
sudo systemctl enable gcam-controller.service
sudo systemctl restart gcam-controller.service
sudo systemctl enable --now gcam-maintenance.timer
sudo systemctl enable --now gcam-app-restart.timer
sudo systemctl enable --now gcam-pi-reboot.timer

echo "=================================================="
echo " FULL CLEAN REINSTALL COMPLETED"
echo "=================================================="
echo "Dashboard                : http://${PI_IP}:8080"
echo "Live only                : http://${PI_IP}:8080/live"
echo "Camera config URL        : ${CAMERA_CONFIG_URL}"
echo "Permanent Dashboard URL  : ${PERMANENT_LIVE_BASE_URL}"
echo "Permanent Live URL       : ${PERMANENT_LIVE_BASE_URL}/live"
echo "MQTT Command Topic       : ${MQTT_COMMAND_TOPIC}"
echo "MQTT Response Topic      : ${MQTT_RESPONSE_TOPIC}"
echo "SFTP Base Path           : ${SFTP_BASE_DIR}"
echo "SFTP Garbage Path        : ${SFTP_GARBAGE_DIR}"
echo "SFTP Audio Path          : ${SFTP_AUDIO_DIR}"
echo
echo "New MQTT commands:"
echo '  {"command":"Close The Software","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Open The Software","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Camera Live URL","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Person Detection Enable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Person Detection Disable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Vehicle Detection Enable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Vehicle Detection Disable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Garbage Detection Enable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Garbage Detection Disable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Garbage AI Mode","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Garbage Normal Mode","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Person Video Recording Enable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Person Video Recording Disable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Vehicle Video Recording Enable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo '  {"command":"Vehicle Video Recording Disable","token":"'"${MQTT_COMMAND_TOKEN}"'"}'
echo
echo "Check app service:"
echo "  sudo systemctl status gcam.service --no-pager"
echo
echo "Check controller service:"
echo "  sudo systemctl status gcam-controller.service --no-pager"
echo
echo "Check app logs:"
echo "  journalctl -u gcam.service -n 100 --no-pager"
echo
echo "Check controller logs:"
echo "  journalctl -u gcam-controller.service -n 100 --no-pager"
echo
echo "Check timers:"
echo "  systemctl list-timers --all | grep gcam"
echo
echo "Battery reader script:"
echo "  sudo nano ${BATTERY_SCRIPT}"
BASH