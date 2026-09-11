# Testing - v1.0 FTP image upload support

## Server Panel

1. Open **Server Panel > Server > Image Upload**.
2. Verify Mode offers `Disabled`, `Sftp`, and `Ftp`.
3. Select `Ftp` while Upload Port is `22`; verify it changes to `21`.
4. Select `Sftp` while Upload Port is `21`; verify it changes to `22`.
5. Enter a custom port such as `2121`, switch modes, and verify the custom port is preserved.
6. Verify Save & Apply rejects an enabled SFTP/FTP mode when Host or Username is empty.

## FTP upload

1. Configure a reachable FTP server and set Mode=`Ftp`, normally Port=`21`.
2. Configure Username, Password, and a writable Remote Directory.
3. Generate an IN or OUT trip with a captured image.
4. Verify the image appears on the FTP server with the same filename as the local capture.
5. Verify `remote_image_path` is stored and the Server Log reports `protocol=FTP` on completion.
6. Force MQTT publication to fail after a successful image upload, then retry; verify the existing remote image path is reused instead of uploading the file again.

## Failure handling

1. Use an unreachable FTP host/port and verify the trip stays pending for retry.
2. Use invalid FTP credentials and verify the upload fails without stopping local gate processing.
3. Use a non-writable remote directory and verify the transfer failure is logged and retried later.
4. Reduce Total Upload Timeout Seconds, simulate a stalled FTP endpoint, and verify the attempt times out and the worker continues after the configured retry/cooldown behavior.

## Regression - SFTP

1. Restore a known-good Mode=`Sftp` configuration.
2. Verify existing SFTP uploads still succeed.
3. Verify server status reports `Connected (MQTT + SFTP)` when both endpoints are reachable.
