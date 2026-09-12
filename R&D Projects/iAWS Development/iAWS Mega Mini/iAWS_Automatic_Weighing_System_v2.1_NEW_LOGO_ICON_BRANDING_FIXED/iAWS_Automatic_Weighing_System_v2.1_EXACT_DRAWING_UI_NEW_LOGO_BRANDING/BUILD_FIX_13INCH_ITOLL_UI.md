# Build update - 13-inch iTOLL-style UI

This package is based on the CS8864 build-fix package and keeps the EventArgs class correction.

## UI changes

- Main dashboard rebuilt to mirror the compact production layout of the original RealTech iTOLL application.
- Target display class: 13-inch Windows terminals, commonly 1366x768.
- Maximized fixed production window with resizing disabled except minimize.
- iTOLL-style top menu and two-column dashboard.
- Four live cameras always visible in a fixed 2x2 monitoring area.
- Fixed 330px right-side dashboard for branding, time, live weight, trigger/reset thresholds, RFID, processing, API/hardware status and test controls.
- Compact bottom System/API log plus last four-camera payload panel.
- Configuration window reduced to a fixed 860x650 iTOLL-style dialog suitable for the same display.
- Gray/white desktop palette and compact borders/margins match the existing iTOLL visual language while retaining iAWS green/orange branding.

## Functional scope unchanged

- 1 weighbridge input.
- 1 RFID input.
- 4 cameras: front/back/left/right.
- Weight-triggered process.
- Direct REST API submission.
- No payment integration.
- No old MQTT/FTP/SFTP/server-sync workflow.
- No old vehicle-presence sensor trigger.
