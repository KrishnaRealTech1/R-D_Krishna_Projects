# Design Notes

## Thread/process model

- CameraService: RTSP decode / latest frame.
- AiCoordinator: throttled inference.
- RollingBufferService: separate FFmpeg process continuously maintaining a 30-second ring.
- EventCoordinator: still capture, audio and event recording jobs.
- SftpUploader: asynchronous evidence upload.

## Event lifecycle

DETECTED -> delay gate -> TRIGGERED -> still/audio -> copy pre-event buffer -> record post-event -> merge one MP4 -> upload -> COMPLETE.

Each event type has its own delay and cooldown. This avoids one vehicle/person suppressing an unrelated LPD or garbage event.

## Why a segment ring instead of storing decoded frames in RAM?

The 30-second buffer remains compressed and bounded on disk, which is much more memory-efficient for a long-running Windows deployment. At trigger time, the previous 10 seconds can be copied immediately so later ring overwrites do not destroy the event pre-roll.

## ONNX assumptions

The generic detector supports two common formats:

- `[1, 4 + classes, N]` / `[1, N, 4 + classes]`: YOLOv8/YOLO11 raw export.
- `[1, N, 6]`: end-to-end detections in `x1,y1,x2,y2,score,class` form.

If a chosen LPD or garbage model exports a different tensor format, adapt only `YoloOnnxDetector.ParseOutput`; the rest of the application stays unchanged.
