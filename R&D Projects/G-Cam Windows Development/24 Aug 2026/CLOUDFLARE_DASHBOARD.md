# Cloudflare / Web Dashboard - v1.0.5

## What this adds

The Windows EXE now hosts its own ASP.NET Core/Kestrel dashboard. No Python, Flask or IIS is required.

Default local URLs:

- Dashboard: `http://127.0.0.1:8080/`
- Live page: `http://127.0.0.1:8080/live`
- Health: `http://127.0.0.1:8080/health`

When Bind Address is `0.0.0.0`, the dashboard can also be reached from the LAN using the Windows PC IP if Windows Firewall permits it.

## Permanent Cloudflare Tunnel

1. In Cloudflare, create/select a remotely-managed Tunnel.
2. Add a Public Hostname such as `camera.example.com`.
3. Set the origin/service to `http://localhost:8080` (or the configured dashboard port).
4. Select **Add a replica** and copy either the tunnel token or the full `cloudflared service install ...` command.
5. In G-Cam -> **Cloudflare / Dashboard**:
   - Enable local web dashboard.
   - Enable Cloudflare Tunnel.
   - Set Public Base URL, e.g. `https://camera.example.com`.
   - Paste the token/full Add-a-replica command.
   - Click **Save + Start Tunnel**.
6. When status reports Connected/public URL healthy, click **Open Public Dashboard**.

The tunnel token is written to `%LOCALAPPDATA%\GCam\cloudflare\tunnel.token`; it is not written into `Documents\GCam\config.json`.

## Dashboard authentication

The Windows application generates a Control API Key. The key is shown only in the local Windows configuration screen. The browser dashboard asks for this key and exchanges it for an HttpOnly session cookie.

For Internet exposure, Cloudflare Access is still recommended as an additional authentication layer.

## API compatibility

Implemented endpoints include:

- `GET /health`
- `GET /api/state`
- `GET /api/controls/state`
- `GET /api/events`
- `GET /api/live.jpg`
- `POST /api/runtime/start`
- `POST /api/runtime/stop`
- `POST /api/controls/warning_audio`
- `POST /api/controls/event`
- `POST /api/controls/feature`
- `POST /api/test-event`

`/api/controls/feature` accepts old-style feature names including:

- `person_detection_enabled`
- `vehicle_detection_enabled`
- `license_plate_detection_enabled` / `lpd_detection_enabled`
- `garbage_detection_enabled`
- `person_video_recording_enabled`
- `vehicle_video_recording_enabled`
- corresponding LPD/Garbage image/video/warning flags

## Dashboard controls

The browser dashboard supports:

- Start/Stop AI runtime
- Live snapshot view
- Runtime/model/buffer/tunnel status
- Person/Vehicle/LPD/Garbage detection enable/disable
- Detection delay
- Cooldown
- Warning audio enable/disable per event
- Image capture enable/disable per event
- Video capture enable/disable per event
- Master warning audio
- Manual event tests
- Recent event list
- Open captured image
- Stream captured MP4 with HTTP range support

## Operational difference from Raspberry Pi build

The Raspberry Pi installation used `systemd` for the G-Cam service and `cloudflared.service`. The Windows standalone app intentionally runs the local dashboard and Cloudflare connector as child services of the WPF application, so Administrator rights are not required for normal use. If the G-Cam application is closed, its managed Cloudflare connector also stops.
