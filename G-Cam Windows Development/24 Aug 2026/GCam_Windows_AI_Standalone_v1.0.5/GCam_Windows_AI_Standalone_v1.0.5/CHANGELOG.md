# Changelog

## v1.0.5 - Cloudflare / Web Dashboard

- Added embedded ASP.NET Core/Kestrel web dashboard to the Windows EXE.
- Added local dashboard on configurable bind address/port (default `0.0.0.0:8080`).
- Added authenticated browser dashboard with live camera snapshot, runtime status and recent events.
- Added remote Person / Vehicle / LPD / Garbage controls:
  - detection enable/disable
  - detection delay
  - cooldown
  - warning audio
  - image capture
  - video capture
- Added remote Start/Stop runtime and manual event test controls.
- Added old-Python-compatible `/api/controls/state`, `/api/controls/feature`, `/api/controls/warning_audio`, and `/api/state` patterns.
- Added event image/video HTTP endpoints; MP4 supports range requests for browser playback.
- Added permanent Cloudflare Tunnel integration using packaged `cloudflared.exe`.
- Added secure token-file launch (`--token-file`) instead of placing the token directly in the command line.
- Added Cloudflare connector watchdog and local/public health checks.
- Added automatic `cloudflared` Windows x64 download during build.
- Added Cloudflare configuration UI and local/public dashboard launch buttons.
- Added Control API Key authentication with HttpOnly browser session cookie.

## v1.0.4

- Added MobileNetSSD Person/Vehicle fallback and event-pipeline diagnostics/test controls.

## v1.0.3

- Fixed FFmpeg packaging/runtime discovery.

## v1.0.2

- Fixed missing System.IO imports.

## v1.0.1

- Fixed WPF/OpenCvSharp `Window`/`Point` namespace collisions and build failure handling.
