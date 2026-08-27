# v0.1.38 Test Checklist

## Database migration

1. Back up `%REALTECH_SYSTEMS%\Data\vehicle-access.db`.
2. Start v0.1.38 with the existing database.
3. Confirm startup completes without a database error.
4. Confirm `trips` contains: `remote_image_path`, `image_uploaded_at`, `sync_attempt_count`, `last_sync_attempt_at`, `next_sync_attempt_at`, and `last_sync_error`.

## Status process timer

1. Trigger an IN vehicle cycle.
2. Confirm active process lines contain a prefix similar to:
   `[IN PROCESS 00:03.284 | RFID 1234567890]`.
3. Confirm logs include vehicle lookup, balance validation, wait completion, camera capture, database commit, barrier steps, release, and final total process time.
4. Repeat for OUT.

## Successful server synchronization

1. Capture two transactions.
2. Confirm server log contains `SYNC CYCLE START`.
3. For each transaction, confirm logs contain `START`, `IMAGE UPLOAD START`, `IMAGE UPLOAD COMPLETE`, `PAYLOAD READY`, `MQTT PUBLISH START`, `MQTT ACK RECEIVED`, and `COMPLETE`.
4. Confirm the second image reports `connection=reused` when the same SFTP connection remains healthy.

## SFTP failure and retry

1. Temporarily block SFTP port 22 or use an invalid host.
2. Confirm the upload stops by `TotalTimeoutSeconds` rather than hanging indefinitely.
3. Confirm the failed transaction records a `FAILED` line with stage, elapsed time, exact error, and next retry time.
4. Confirm another eligible transaction is attempted instead of being permanently blocked.
5. Restore SFTP and confirm retry succeeds.

## MQTT failure after successful image upload

1. Keep SFTP working and temporarily make MQTT unavailable.
2. Confirm image upload completes and MQTT fails.
3. Restore MQTT.
4. Confirm the next retry logs `IMAGE REUSED` and does not upload the same image again.
