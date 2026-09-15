# RealTech iAWS v0.1 - 13-inch Display UI Alignment

This build keeps the existing iAWS v0.1 weighing/RFID/server/hardware logic unchanged and updates only display sizing/alignment.

## Main dashboard
- Optimized for common fixed 13-inch Windows panels, especially 1366x768.
- DPI-aware layout retained (PerMonitorV2 from the existing manifest).
- Menu and footer use fixed compact heights.
- Camera/system, IN/OUT lane and log areas use minimum-safe proportional rows so the dashboard remains aligned at smaller working heights and still expands cleanly at larger resolutions.
- Four camera panels remain fully visible.
- Right information panel uses fixed internal row heights to prevent branding, date/time, PROCESSED/PENDING, connectivity and live-weight text clipping.
- IN/OUT RFID display remains prominent with strong colored strokes.
- Signal lamps, buzzer and barrier are aligned inside each lane without overlap.
- Raised boom-barrier artwork has a larger internal drawing canvas and lower pivot so the arm no longer clips at the top when OPEN.
- Server/Status logs stay visible at the bottom.

## Configuration windows
- Control Panel, Hardware Configuration and Server Panel default sizes were reduced to fit the same 13-inch working area.
- Existing ScrollViewer behavior is retained, so no configuration field or function was removed.

## Logic preserved
No changes were made to:
- single weighbridge processing
- target/reset weight logic
- first RFID wins / opposite RFID ignore behavior
- RFID readers
- 4 editable camera processing
- signal/buzzer/boom hardware commands
- manual Control Panel reflection to the main screen
- processed/pending calculations
- MQTT/server/image synchronization
- local storage/logging
