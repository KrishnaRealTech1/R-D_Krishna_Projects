# G-Cam Windows AI Standalone v1.0.5

Windows 10/11 .NET 8 WPF standalone camera application for Person, Vehicle, License Plate (LPD) and Garbage detection, warning audio, event image/video evidence, 30-second rolling buffer, 10-second pre-event video, SFTP upload, and Cloudflare-accessible remote dashboard.

## v1.0.5: Cloudflare dashboard

The Windows EXE now includes the web dashboard directly. It does not require Python, Flask, IIS, Node.js or a second application.

Default local dashboard:

`http://127.0.0.1:8080`

The build also downloads/packages `cloudflared.exe`. A permanent remotely-managed Cloudflare Tunnel can route your public hostname to:

`http://localhost:8080`

See `docs/CLOUDFLARE_DASHBOARD.md` for setup.

## Build

Requirements on the build PC:

- Windows 10/11 x64
- .NET 8 SDK
- Internet connection during the first build

Run from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-win-x64.ps1
```

The build script downloads/validates:

- FFmpeg
- Cloudflare `cloudflared` Windows x64
- MobileNetSSD Person/Vehicle fallback model

Output:

- `Publish\win-x64\GCam.Windows.exe`
- `Publish\win-x64\tools\ffmpeg.exe`
- `Publish\win-x64\tools\cloudflared.exe`
- `Publish\GCam-Windows-win-x64-v1.0.5.zip`

FFmpeg and cloudflared are also embedded in the single-file EXE as fallbacks.

## First run

1. Open **Camera / Recording** and configure the Live/AI RTSP URL.
2. Configure the Evidence/Buffer RTSP URL; if it is blank/default, the app reuses the Live RTSP URL.
3. Click **Save Settings**.
4. Click **Start**.
5. Confirm Camera, Buffer and Person/Vehicle model status.
6. Use **Detection Controls -> Test Person Event** to verify image/video evidence independently from AI.

## Cloudflare setup

1. Open **Cloudflare / Dashboard**.
2. Keep local dashboard enabled, normally on port `8080`.
3. Note/copy the generated **Control API Key**.
4. In Cloudflare Dashboard -> Networking -> Tunnels, create/select a remotely-managed tunnel.
5. Configure the tunnel Public Hostname to use service/origin `http://localhost:8080`.
6. Choose **Add a replica** and copy the token or complete `cloudflared service install ...` command.
7. Paste it into G-Cam's **Tunnel Token / Add Replica Command** field.
8. Set **Public Base URL** to your hostname, for example `https://camera.example.com`.
9. Enable **Cloudflare Tunnel** and optionally **Auto-start tunnel when G-Cam opens**.
10. Click **Save + Start Tunnel**.
11. Click **Open Public Dashboard** and sign in using the Control API Key.

The tunnel token is stored under `%LOCALAPPDATA%\GCam\cloudflare\tunnel.token`, not in the normal JSON configuration file.

## Remote dashboard capabilities

The browser dashboard provides live view/status plus controls for:

- Person detection
- Vehicle detection
- LPD detection
- Garbage detection
- Detection time delay
- Cooldown
- Warning audio per event
- Master warning audio
- Image capture per event
- Video capture per event
- Start/Stop runtime
- Manual Person/Vehicle/LPD/Garbage event tests
- Recent event status
- Captured image viewing
- Captured MP4 playback

## Models

Person/Vehicle works out-of-box through the bundled MobileNetSSD fallback if `person_vehicle.onnx` is absent.

LPD and Garbage still need compatible ONNX models configured in the **Models** tab. The original supplied scripts referenced a garbage ONNX file but did not include the model binary, and did not provide an LPD model.

## Writable data

User configuration:

`%USERPROFILE%\Documents\GCam\config.json`

Evidence:

`%USERPROFILE%\Documents\GCam\Evidence`

Cloudflare token/logs:

`%LOCALAPPDATA%\GCam\cloudflare`

Extracted embedded tools:

`%LOCALAPPDATA%\GCam\tools`

## Security

The remote dashboard requires a generated Control API Key for API/live/evidence access. For production Internet exposure, add Cloudflare Access in front of the hostname as an additional identity layer and rotate the Tunnel token if it is ever exposed.
