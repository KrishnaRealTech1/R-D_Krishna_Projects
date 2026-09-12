# iAWS 13-inch fixed dashboard UI

This build changes the iAWS presentation to follow the same compact desktop layout used by the original RealTech iTOLL production application.

## Main screen

- Designed around the common 13-inch 1366x768 Windows display class.
- Maximized, non-resizable production dashboard (`ResizeMode=CanMinimize`).
- 25px desktop menu bar, matching the iTOLL application pattern.
- Main working area uses the iTOLL two-column structure:
  - Large left monitoring area.
  - Fixed 330px right-side status dashboard.
- Four cameras remain visible at all times in a fixed 2x2 layout.
- Right dashboard contains iAWS branding, system clock, live weighbridge weight, trigger/reset weights, RFID, process state, API/hardware status and simulation/operator controls.
- Bottom area uses the compact iTOLL-style log row with System/API log and last 4-camera payload.
- No payment, MQTT, FTP/SFTP, server-sync or old sensor controls were reintroduced.

## Configuration screen

- Fixed 860x650 size for 13-inch displays.
- Restyled to match the iTOLL hardware-settings window: gray application background, white bordered configuration panel, compact fields and bottom status/action bar.
- Keeps Process/API, RFID & Weighbridge and 4 Cameras configuration only.

## Build note

This package includes the previous CS8864 EventArgs build fix. No .NET SDK is installed in the packaging environment, so the final Windows WPF compilation must be run with `build-standalone-exe.bat` on a Windows machine with the .NET 8 SDK.
