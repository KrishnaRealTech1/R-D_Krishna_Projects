# Changes in v0.1.8

- Removed the fixed 1366 × 768 minimum and maximum window constraints that caused the dashboard to extend beyond smaller displays.
- The main window now starts maximized inside the Windows working area and can be resized or restored.
- Added responsive width limits for the right operational dashboard and the simulation/hardware-test section.
- The right operational dashboard scales down as one unit on lower-resolution and high-DPI displays, while the LibVLC camera surfaces remain outside the scaling transform.
- Reduced fixed dashboard row heights so the camera, identification, logs, signals and barrier panels remain visible at 1024 × 768.
- Added explicit per-monitor DPI-awareness declarations for Windows display scaling.
- Preserved the v0.1.7 RTSP software-decoding and frozen-stream watchdog changes.
