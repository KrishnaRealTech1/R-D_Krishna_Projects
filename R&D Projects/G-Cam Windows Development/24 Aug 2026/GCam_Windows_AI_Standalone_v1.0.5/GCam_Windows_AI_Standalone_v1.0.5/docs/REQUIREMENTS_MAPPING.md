# Requirement Mapping

| Requirement | Implementation |
|---|---|
| Person AI | `AiCoordinator` + `YoloOnnxDetector`, COCO label `person` |
| Vehicle AI | Same detector, vehicle labels car/truck/bus/motorcycle/motorbike |
| LPD AI | Dedicated `license_plate.onnx` detector |
| Garbage AI | Dedicated `garbage.onnx` detector |
| Warning audio | `WarningAudioService`, master and per-event control |
| Control panel | WPF `Detection Controls` tab + camera/models/upload tabs |
| Detection delay | `DetectionGate` per event type |
| Image capture | `EventCoordinator` annotated JPG evidence |
| Video capture | `RollingBufferService.CreateEventVideoAsync` |
| 30s overwrite | FFmpeg segment ring with `segment_wrap` |
| 10s before event | event copies recent buffer segments before recording post-event |
| Single output clip | FFmpeg concat/remux, fallback re-encode |
| Server upload | `SftpUploader`, after final image/video is ready |


## Remote dashboard / Cloudflare

- Embedded Windows web dashboard on port 8080.
- Cloudflare Tunnel integration routes a permanent public hostname to the local dashboard.
- Browser control panel exposes detection, warning, image/video capture, delays, runtime status and event evidence.
