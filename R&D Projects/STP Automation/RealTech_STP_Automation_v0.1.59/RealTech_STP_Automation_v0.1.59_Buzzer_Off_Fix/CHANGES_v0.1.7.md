# Changes in v0.1.7

- Fixed RTSP streams that displayed one decoded frame and then appeared frozen.
- Software video decoding is now the default for both Hikvision gate cameras to avoid Direct3D/hardware-decoder first-frame stalls.
- Increased RTSP/live buffering from 500 ms to 1200 ms for more stable LAN playback.
- Added a per-camera playback watchdog. If an active stream timestamp stops advancing for 12 seconds, the application reconnects that lane automatically.
- Added configurable `EnableHardwareDecoding` and `StreamWatchdogSeconds` camera settings.
- Preserved RTSP-over-TCP, live transaction snapshots, and all v0.1.5 dashboard behavior.
