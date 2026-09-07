# Changes in v0.1.38

## Server synchronization performance and reliability

- Reuses the authenticated SFTP connection between image uploads instead of reconnecting for every transaction.
- Adds a total wall-clock timeout for each SFTP upload.
- Creates/checks the configured remote directory once per SFTP connection.
- Stores the remote image path and image upload timestamp in SQLite.
- Does not upload an image again when SFTP succeeded but MQTT failed later.
- Removes queue head-of-line blocking: one failed transaction no longer stops newer eligible transactions.
- Adds persisted retry attempt count, last error, last attempt time, next retry time, and exponential retry delay.
- Adds automatic SQLite migration for existing installations.

## Detailed logs and timers

- Server log now records every synchronization stage: cycle start, transaction start, image decision, file size, SFTP result, payload size, MQTT publish, MQTT acknowledgement, database update, failure stage, retry time, and total elapsed time.
- Status log now prefixes active IN/OUT processing messages with a live elapsed process timer and RFID correlation.
- Status log now records vehicle lookup, balance validation, trip-history checks, wait timer completion, camera capture, local database transaction, and each barrier command stage.

## Configuration

- Added `Server.InitialRetryDelaySeconds`.
- Added `Server.MaxRetryDelaySeconds`.
- Added `Server.ImageUpload.TotalTimeoutSeconds`.
- Default synchronization interval changed from 15 seconds to 5 seconds.
- Default SFTP operation/total timeout changed to 45/60 seconds.
