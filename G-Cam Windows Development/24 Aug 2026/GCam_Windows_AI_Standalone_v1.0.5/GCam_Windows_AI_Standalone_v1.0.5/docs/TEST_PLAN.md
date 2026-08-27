# Field Test Plan

1. Camera connectivity
   - Verify Live/AI RTSP opens for at least 30 minutes.
   - Verify Evidence/Main RTSP remains stable while live detection is active.

2. Person
   - Enable Person detection, warning, image and video.
   - Set delay to 2 seconds.
   - Confirm no event before 2 seconds and one event after the delay.
   - Confirm JPG, warning tone, MP4 and optional upload.

3. Vehicle
   - Repeat with car/motorcycle/bus/truck.
   - Validate cooldown prevents excessive duplicate clips.

4. LPD
   - Confirm the chosen license-plate ONNX class order matches license_plate.txt.
   - Validate small/far plates and day/night conditions.

5. Garbage
   - Confirm garbage model labels match garbage.txt.
   - Validate expected garbage types, clean-ground false positives and low light.

6. Rolling buffer
   - Let the application run at least 1 minute before triggering.
   - Trigger an event at a visible timestamp/action.
   - Confirm final MP4 contains roughly 10 seconds before the trigger and the configured post-event period.
   - Confirm only one final MP4 is stored/uploaded for that event.

7. Controls
   - Disable each detector individually and verify it cannot trigger.
   - Disable image/video/warning independently and verify the remaining actions still work.
   - Disable master warning and verify all warning playback stops.

8. Upload
   - Test with valid SFTP credentials and paths.
   - Disconnect network during an event and verify local evidence is retained.
   - Current v1.0 reports failed upload in the Events grid; persistent retry queue is a recommended next enhancement.

9. Long run
   - 24-hour test for RTSP reconnect, FFmpeg buffer stability, disk growth and memory usage.
