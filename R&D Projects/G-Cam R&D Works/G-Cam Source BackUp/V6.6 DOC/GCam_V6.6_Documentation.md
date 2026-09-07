# G-Cam V6.6 — System Documentation
### RealTech Systems | Raspberry Pi AI Camera Platform

---

## What Is G-Cam?

G-Cam is an AI-powered surveillance camera system that runs on a Raspberry Pi. It connects to an IP camera over RTSP (a video stream protocol), performs real-time detection of people, vehicles, and garbage, and reports events through MQTT messages, SFTP file uploads, and a local web dashboard.

The installer script (`gcam_full_reinstall_v6.6_fixed.sh`) fully sets up everything from scratch — Python environment, detection models, system services, timers, audio, and network integrations.

---

## What the Installer Does (Step by Step)

The script runs 17 numbered steps. Here is what each step does in plain terms.

**Step 1 — Stop old services**
Kills any previously running G-Cam processes and removes old service files so the system starts clean.

**Step 2 — Remove old app**
Deletes the entire `/opt/gcam` folder so nothing from a previous install carries over.

**Step 3 — Fix DNS if needed**
Checks if the internet is reachable. If not, it replaces the DNS config with Google (8.8.8.8) and Cloudflare (1.1.1.1).

**Step 4 — Update apt**
Runs `apt-get update` to refresh the package list.

**Step 5 — Install system packages**
Installs all required Linux packages including Python, FFmpeg, ALSA audio tools, OpenCV dependencies, I2C tools, GPIO tools, espeak-ng (text-to-speech), curl, and more.

**Step 6 — Install Cloudflared**
Downloads and installs the Cloudflare tunnel binary (`cloudflared`) for the correct CPU architecture (ARM64 or ARMv7).

**Step 7 — Configure ALSA audio**
Sets the default sound device to the USB speaker at hardware slot `hw:0,0` by writing `/etc/asound.conf`.

**Step 8 — Create folders**
Creates the required directory structure under `/opt/gcam/`:

| Folder | Purpose |
|---|---|
| `assets/` | Warning audio WAV file |
| `data/` | Config and state JSON files |
| `logs/` | Log files |
| `models/` | AI detection model files |
| `files/` | Captured images |
| `files/garbage/` | Garbage detection images |
| `files/audio/` | Downloaded audio files |
| `files/video/` | Recorded event videos |
| `tmp/` | Temporary files |
| `templates/` | Web dashboard HTML |

**Step 9 — Create Python virtual environment**
Creates a Python venv at `/opt/gcam/venv` and installs all required Python libraries including Flask, OpenCV, paho-mqtt, paramiko (SFTP), psutil, smbus2, and waitress.

**Step 10 — Download detection models**
Downloads the MobileNetSSD person/vehicle detection model (`.prototxt` + `.caffemodel`) from GitHub. Also copies `garbage_yolo.onnx` from the Pi user's home directory.

**Step 11 — Create warning audio**
Uses `espeak-ng` to generate a spoken warning message, then processes it through FFmpeg to reduce noise and normalize volume. Output: `/opt/gcam/assets/warning.wav`.

**Step 12 — Write config.json**
Saves all the values entered during setup into `/opt/gcam/data/config.json`. This file is the single source of truth for the entire application.

**Step 13 — Write helper scripts**
Creates three helper scripts:

- `read-battery-voltage.sh` — Python script that reads voltage and current from an INA226 sensor over I2C. Returns JSON with battery_voltage, battery_current_a, and battery_current_ma.
- `gcam-force-reboot.sh` — Simple script that calls `systemctl reboot` (used for safe sudo-allowed reboot).
- Sudoers files — Grants the Pi user permission to reboot and to start/stop/restart the gcam service without a password.

**Step 14 — Write HTML templates**
Creates three web pages served by the Flask app:

- `index.html` — Full dashboard with live feed, garbage/person/vehicle detection results, geofence editors, controls panel, links, and device health.
- `live.html` — Minimal full-screen live video page.
- `live_talk.html` — Mobile-friendly push-to-talk page that streams microphone audio to the Pi speaker in real time.

**Step 15 — Write app.py**
The main application (~5000+ lines). Handles everything: RTSP capture, AI detection, Flask web server, MQTT command listener, SFTP uploads, audio playback, Cloudflare tunnels, and background threads.

**Step 16 — Write controller.py**
A small companion process that handles three specific MQTT commands that require controlling the main app service: `Close The Software`, `Open The Software`, and `Camera Live URL` (temporary Cloudflare tunnel to camera config page).

**Step 17 — Write services and timers, then start everything**
Creates and enables all systemd units, then starts the application.

---

## Configuration — What You Are Asked to Enter

When you run the installer, it asks for these values interactively (with defaults shown in brackets):

| Parameter | Example Value | What It Does |
|---|---|---|
| Device ID | `RTGCAMx` | Unique name for this device, used in filenames and MQTT topics |
| Camera Name | `RTGCAMx` | Human-friendly camera label |
| Main RTSP URL | `rtsp://admin:admin@192.168.22.225:554/...` | Full-resolution stream for detection |
| Cloudflare Live RTSP URL | `rtsp://...sub/...` | Sub-stream used for Cloudflare live view |
| Pi IP | `192.168.22.101` | Local IP of the Raspberry Pi |
| Router IP | `192.168.22.1` | Local router IP |
| Camera IP | `192.168.22.225` | Local IP of the IP camera |
| Camera Config URL | `https://192.168.22.225:443` | URL to camera's own web interface |
| Subdomain Name | `RTGCAMx` | Used for permanent Cloudflare live URL |
| Base Domain | `ifill.in` | Your domain, forms `https://RTGCAMx.ifill.in` |
| Tailscale Mail ID | *(optional)* | For Tailscale VPN identity tracking |
| MQTT Host/Port/User/Pass | `3.111.78.82:1883` | Broker connection for commands and responses |
| MQTT Topics | `RTGCAMx/command` etc. | Topics for garbage events, camera events, commands |
| MQTT Command Token | `GCAM_SECRET_123` | Auth token — all commands must include this |
| SFTP Host/Port/User/Pass | `3.111.78.82:22` | SFTP server for uploading images, videos, audio |
| SFTP Paths (7 paths) | `/home/ftpuser/ftp/files/RTGCAMx/...` | Where each type of file is stored on SFTP server |
| Pi Reboot Times | `03:00:00` and `15:00:00` | Daily full reboot schedule |
| Router Reboot Times | `03:30` and `15:30` | GPIO pulse schedule to reboot the router |

---

## Files Created After Install

| File / Path | Purpose |
|---|---|
| `/opt/gcam/data/config.json` | All configuration values |
| `/opt/gcam/data/state.json` | Live runtime state (updated every few seconds) |
| `/opt/gcam/data/garbage_reference.jpg` | Baseline image used for garbage comparison |
| `/opt/gcam/app.py` | Main application |
| `/opt/gcam/controller.py` | MQTT controller for service management |
| `/opt/gcam/assets/warning.wav` | Spoken warning audio |
| `/opt/gcam/models/MobileNetSSD_deploy.prototxt` | Person/vehicle model config |
| `/opt/gcam/models/MobileNetSSD_deploy.caffemodel` | Person/vehicle model weights |
| `/opt/gcam/models/garbage_yolo.onnx` | Garbage object detection model |
| `/usr/local/bin/read-battery-voltage.sh` | INA226 battery reader |
| `/usr/local/bin/gcam-force-reboot.sh` | Safe reboot helper |
| `/etc/asound.conf` | ALSA USB audio config |
| `/etc/sudoers.d/gcam-reboot` | Allow Pi user to reboot without password |
| `/etc/sudoers.d/gcam-controller` | Allow Pi user to manage gcam.service |

---

## Services and Timers

After install, these systemd units are active:

| Unit | Type | What It Does |
|---|---|---|
| `gcam.service` | Service | Main G-Cam app, restarts automatically if it crashes |
| `gcam-controller.service` | Service | MQTT controller for open/close software commands |
| `gcam-cloudflare-ping.service` | Service | Pings cloudflare.com every 15 seconds, logs result |
| `gcam-app-restart.timer` | Timer | Restarts `gcam.service` every 3 hours (00, 03, 06... 21:00) |
| `gcam-pi-reboot.timer` | Timer | Full Pi reboot at the two configured times daily |
| `gcam-maintenance.timer` | Timer | Runs apt cleanup and log vacuum at 02:45 and 14:45 |

---

## Detection Features

### Person Detection
Uses MobileNetSSD (a lightweight neural network). Detects people in the frame, filters out poles, trees, and walls using aspect ratio, edge density, and texture checks. Requires the person's foot point to be inside the configured polygon zone.

When a confirmed person is detected:
- A warning audio plays on the speaker
- A JPEG snapshot is captured and uploaded via SFTP
- A video clip is recorded (6 seconds before + 8 seconds after) and uploaded
- An MQTT message is published to the camera event topic

### Vehicle Detection
Uses the same MobileNetSSD model but looks for cars, buses, and motorbikes. Has two modes:

- **Static mode** — detects parked or stopped vehicles. A vehicle must stay inside the geofence polygon for 5 continuous seconds with at least 3 confirmed detections before triggering.
- **Moving mode** — additionally requires the vehicle to show motion (pixel difference between frames).

When confirmed:
- A snapshot is uploaded via SFTP
- A video clip is recorded and uploaded
- A combined MQTT message is published once both image and video are done

### Garbage Detection
Has two modes selectable from dashboard or MQTT:

- **AI Mode** — Takes 3 sample frames, compares each against the reference image, runs the YOLO object detector on the garbage zone, and combines change-detection boxes with object detection boxes to assign a severity level.
- **Normal Mode** — Captures a frame and uploads it without analysis. Useful when you just want a scheduled photo.

Severity levels are based on detected garbage count and changed area ratio:

| Severity | Garbage Count | Area Change |
|---|---|---|
| LOW | 1–2 | ≥ 2.5% |
| MEDIUM | 3–5 | ≥ 8% |
| HIGH | 6+ | ≥ 18% |
| NONE | 0 | < 2.5% |

Garbage detection runs on a schedule (default: every 60 minutes) and can also be triggered manually via MQTT or dashboard.

---

## Geofence (Polygon Zones)

All three detection types have independent polygon geofences drawn on the dashboard. These define the active detection area inside the camera frame.

- Points are stored as ratios (0.0 to 1.0) relative to frame width and height
- Minimum 3 points, maximum 20 points
- Changes apply immediately without restarting the app
- A snapshot from the live feed is shown as background for drawing

Geofence pages are available at:
- `/garbage_geofencing`
- `/person_geofencing`
- `/vehicle_geofencing`

---

## Audio System

### Warning Audio
A pre-recorded spoken warning plays when a person is detected. This can be:
- Enabled/disabled via MQTT or dashboard
- Scheduled to a specific time window (e.g., only between 10:00 and 18:00)

### MQTT Audio Playback
Audio files stored on the SFTP server can be played on the Pi speaker by sending an MQTT command. The audio worker downloads the file, converts it to WAV if needed, and plays it through aplay.

### Default Audio (Pre-loaded)
Audio files can be imported from the SFTP default audio folder to the Pi's local storage, then played without downloading each time. This saves bandwidth and allows offline playback.

### Live Talk
The `/live_talk` page on the dashboard allows a mobile user to hold a button and speak directly into the Pi's speaker in real time. Audio is captured from the phone microphone, sent as WebM chunks to the Pi, converted by FFmpeg, and played through aplay.

### Network Audio Muting
If the internet connection drops, the system automatically mutes the speaker volume to 0% and clears the audio queue. When the connection comes back, the volume is restored to its previous level. This prevents queued audio from playing when network returns after a long gap.

---

## MQTT Commands Reference

All commands are sent as JSON to the configured command topic. Token is always required.

```json
{"command": "CommandName", "token": "GCAM_SECRET_123"}
```

| Command | What It Does |
|---|---|
| `Live Stream` | Creates a temporary Cloudflare public URL for the live feed (3 minutes) |
| `Garbage Detect` | Triggers an immediate garbage detection cycle |
| `Garbage Sample Capture` | Captures current frame as the new garbage reference image |
| `Garbage AI Mode` | Switches garbage detection to AI analysis mode |
| `Garbage Normal Mode` | Switches garbage detection to capture-only mode |
| `Garbage Detection Set to 2hours` | Changes the scheduled detection interval |
| `Audio File Play` + `audio_name` | Downloads and plays an audio file from SFTP |
| `Import Default Audio` | Downloads all files from SFTP default audio folder to Pi |
| `Erase Default Audio in Pi` | Deletes all default audio files from Pi local storage |
| `Play Default Audio - filename.mp3` | Plays a file already stored on the Pi |
| `Warning Audio Enable` | Enables the person-detected warning audio (24 hour mode) |
| `Warning Audio Disable` | Disables the warning audio |
| `Scheduled Warning Audio` + `start_time` + `end_time` | Sets a time window for warning audio |
| `Scheduled Warning Disabled` | Clears the schedule, keeps warning audio on all day |
| `Queue Clear` | Stops current audio and clears all pending audio jobs |
| `Current Pi Volume` | Returns the current ALSA speaker volume percentage |
| `Pi Volume 70%` | Sets the speaker volume (0%–100%) |
| `Person Detection Enable/Disable` | Turns person detection on or off |
| `Vehicle Detection Enable/Disable` | Turns vehicle detection on or off |
| `Garbage Detection Enable/Disable` | Turns garbage detection on or off |
| `Person Video Recording Enable/Disable` | Controls whether person events record a video clip |
| `Vehicle Video Recording Enable/Disable` | Controls whether vehicle events record a video clip |
| `Power Saving Enable` | Pauses camera, detection, uploads, and audio |
| `Power Saving Disable` | Resumes all functions |
| `Scheduled Power Saving` + `start_time` + `end_time` | Sets automatic power saving hours |
| `Data Status` | Returns full device health, network, battery, RAM, versions |
| `Software Version` | Returns current version string and feature list |
| `Device Restart` | Reboots the Raspberry Pi |
| `Close The Software` | Stops the gcam.service (handled by controller) |
| `Open The Software` | Starts the gcam.service (handled by controller) |
| `Camera Live URL` | Creates a temporary Cloudflare URL to the camera config page |

---

## Dashboard Pages

All pages are served at `http://<Pi_IP>:8080/`

| URL | What It Shows |
|---|---|
| `/` | Main dashboard — all tabs |
| `/live` | Full-screen live video only |
| `/live_talk` | Push-to-talk audio page |
| `/garbage_geofencing` | Garbage polygon editor only |
| `/person_geofencing` | Person polygon editor only |
| `/vehicle_geofencing` | Vehicle polygon editor only |
| `/video_feed` | Raw MJPEG stream |
| `/video_feed_cloudflare` | Sub-stream MJPEG for Cloudflare |

Dashboard tabs include: Live Feed, Last Garbage Detection, Last Person Detection, Last Vehicle Detection, Garbage Geofence, Person Geofence, Vehicle Geofence, Controls, Links & Status, Device Health.

---

## Battery Monitoring

The system reads voltage and current from an INA226 power sensor connected via I2C (bus 1, address 0x40). The reader script samples 8 readings and returns averaged values as JSON:

```json
{
  "battery_voltage": 12.451,
  "battery_current_a": 0.832,
  "battery_current_ma": 832.1
}
```

Battery status labels: **Good** (≥ 12.2V), **Medium** (≥ 11.7V), **Low** (< 11.7V).

---

## Power Saving Mode

When power saving is enabled (by MQTT, dashboard, or schedule):
- RTSP capture stops
- All detection stops
- Live stream becomes unavailable
- Audio playback stops and queue is cleared
- Cloudflare tunnel is closed
- Only Data Status and Power Saving commands are accepted via MQTT

The system resumes normally when power saving is disabled.

---

## Router Reboot (GPIO)

The Pi can reboot the router by pulsing GPIO pin 17. The sequence is:

1. Set GPIO HIGH for 1 second
2. Wait 5 seconds
3. Set GPIO HIGH for 1 second again
4. Return to LOW (idle)

This runs automatically at the two configured times per day and can also be triggered via MQTT.

---

## Useful Commands to Check Status

```bash
# Check if the main app is running
sudo systemctl status gcam.service --no-pager

# Check controller status
sudo systemctl status gcam-controller.service --no-pager

# View live app logs
journalctl -u gcam.service -n 100 --no-pager -f

# View controller logs
journalctl -u gcam-controller.service -n 100 --no-pager

# List all G-Cam timers
systemctl list-timers --all | grep gcam

# Check ALSA sound card
aplay -l

# Test battery reader
sudo /usr/local/bin/read-battery-voltage.sh

# Check I2C device
i2cdetect -y 1
```

---

## Before Running the Installer — Checklist

- [ ] Copy `garbage_yolo.onnx` to the Pi user's home directory (e.g., `/home/pi/garbage_yolo.onnx`)
- [ ] Confirm the RTSP URL is correct and accessible from the Pi
- [ ] Confirm MQTT broker is reachable from the Pi
- [ ] Confirm SFTP server is reachable and the paths already exist (or the user has permission to create them)
- [ ] Run the installer with `sudo bash` or as a user with sudo access
- [ ] After install, verify `sudo systemctl status gcam.service` shows `active (running)`

---

*G-Cam Version 6.6 — RealTech Systems*
