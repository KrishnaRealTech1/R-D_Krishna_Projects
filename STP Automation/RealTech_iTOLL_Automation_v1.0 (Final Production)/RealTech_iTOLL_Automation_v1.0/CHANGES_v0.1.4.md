# RFID Vehicle Access Starter v0.1.4 UI changes

- Realigned the fixed 1366 × 768 dashboard into two primary areas:
  - Left: camera feeds, lane identification, logs and hardware-test controls.
  - Right: a full-height operational status dashboard.
- Reduced and balanced the IN and OUT camera feed areas so both cameras remain equal in size.
- Replaced the single shared indicator with independent **IN SIGNAL** and **OUT SIGNAL** graphics.
- Signal graphics now use only the standard traffic-light colours:
  - RED — stop.
  - ORANGE — wait/processing.
  - GREEN — go.
- Manual and automatic `IN/OUT RED`, `ORG`, `GRN` and `ALL OFF` commands update the corresponding signal graphic independently.
- Changed network graphics to green for online and red for offline/local mode.
- Kept independent IN and OUT boom-barrier graphics with green/open and red/closed status.
- Rebuilt the hardware-test controls with fixed grid alignment instead of wrapping controls.
- No page-level `ScrollViewer` is used; the main dashboard remains fixed.

The displayed signal and barrier states represent the last control commands successfully issued by the application. Independent physical feedback still requires PLC/controller status or limit-switch messages.
