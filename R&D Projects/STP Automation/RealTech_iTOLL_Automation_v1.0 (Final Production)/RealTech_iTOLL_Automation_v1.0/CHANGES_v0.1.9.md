# Changes in v0.1.9

## 13-inch square-display layout

- Reworked the dashboard for 4:3 and 5:4 industrial displays.
- Primary target is 1024x768; the same layout scales cleanly to 1280x1024.
- Reduced the fixed right status panel to 278 pixels.
- Rebalanced camera, identification, log, and hardware-test areas so no dashboard section is clipped on a square display.
- Kept both RTSP streams live and independent.
- Lane lights continue to show only the active colour glow without RED/STOP, ORANGE/WAIT, or GREEN/GO text.

## Working COM-port configuration

- Added `Connectivity > COM Port Settings...`.
- Detects Windows COM ports and also allows a COM name to be typed manually.
- Supports independent port and baud-rate selection for:
  - IN RFID reader
  - OUT RFID reader
  - control unit
- `Save & Connect` writes the settings to `appsettings.json` and reconnects immediately.
- Application restart is not required after changing a COM port.
- Added automatic serial reconnect attempts using `Hardware.ReconnectSeconds`.
- Added validation preventing the same COM port from being assigned to multiple devices.
- Added a visible hardware/COM status card on the main dashboard.
- Hardware mode is enabled by default (`SimulationEnabled: false`). Simulation can still be selected from the COM settings window.
