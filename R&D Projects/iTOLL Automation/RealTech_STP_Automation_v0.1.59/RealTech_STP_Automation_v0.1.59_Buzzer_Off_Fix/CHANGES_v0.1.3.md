# RFID Vehicle Access Starter v0.1.3 UI changes

- Removed the right-side dashboard `ScrollViewer` and replaced it with a fixed Grid layout.
- Removed the simulation-panel `ScrollViewer` and compacted the controls into fixed IN/OUT rows.
- Added a colored network card:
  - Green when the server is online.
  - Orange when operating offline/local.
- Added a graphical three-light indicator whose color follows the current lane state.
- Added graphical IN and OUT boom barriers.
  - Red card and horizontal arm when closed.
  - Green card and raised arm when open.
- Added `HardwareGateway.ControlCommandSent` so manual and automatic boom-barrier commands update the dashboard.
- Retained the original 1366 x 768 fixed application window.

The displayed barrier position represents the last control command successfully issued by the application. Physical position confirmation requires barrier limit-switch/PLC feedback in the control protocol.
