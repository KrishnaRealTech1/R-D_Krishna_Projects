# RealTech STP Automation v0.1.43

## Missing trip is notification-only

- Removed creation of synthetic missing-IN and missing-OUT trip records from the live processing path.
- Only the physical trip detected by the IN or OUT RFID reader is inserted as `PendingSync`.
- The physical trip continues through normal camera capture, SFTP upload and MQTT publish.
- Added `missingTripNotification` to exceptional MQTT payloads:
  - `missingDirection`: `IN` or `OUT`
  - `notificationOnly`: `true`
  - `syntheticTripCreated`: `false`
  - `countedAsTransaction`: `false`
- The existing `exceptionalApproval` object continues to carry Manual/Auto mode, reason, approver name, role, mobile and approval time.

## Dashboard counting

- Missing-trip notifications do not increase Pending or Processed counts.
- Legacy rows with `imageCaptureStatus=NotCaptured-ExceptionalReconciliation` are moved to internal `ReconciliationRequired` status during startup and excluded from synchronization and counts.

## Pairing behavior

- Missing OUT: the earlier active IN is closed internally using `exceptionally_closed_at` and `exceptionally_closed_by_trip_id`; no OUT transaction is created.
- Missing IN: the current OUT is saved without a synthetic IN and without `entryTransactionId`.

## Version

- Application version updated to `0.1.43`.
- Updated orange MQTT/SFTP guide added to `docs`.
