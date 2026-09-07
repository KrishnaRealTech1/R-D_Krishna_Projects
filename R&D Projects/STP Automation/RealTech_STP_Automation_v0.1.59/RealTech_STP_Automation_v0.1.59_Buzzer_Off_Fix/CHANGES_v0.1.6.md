# Changes in v0.1.6

- Enabled the supplied IN and OUT RTSP camera URLs.
- Added live WPF RTSP playback using LibVLCSharp and the packaged Windows LibVLC runtime.
- Added TCP RTSP transport, configurable network caching and automatic reconnect attempts.
- Replaced the camera placeholder panels with real live `VideoView` controls.
- Added camera connection status overlays for both lanes.
- Captures transaction snapshots from the active live stream, with a placeholder fallback when a camera is unavailable.
- Preserved the v0.1.5 processed/pending counter rules and command-driven signal lamps.
