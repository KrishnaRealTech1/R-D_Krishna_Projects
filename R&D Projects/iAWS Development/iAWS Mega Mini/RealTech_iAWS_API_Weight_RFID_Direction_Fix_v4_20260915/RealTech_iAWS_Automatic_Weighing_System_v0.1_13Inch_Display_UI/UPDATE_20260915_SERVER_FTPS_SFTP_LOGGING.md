# Update 2026-09-15 - Server / FTPS / SFTP Response Logging

## Scope
Only server synchronization diagnostics and transfer-error reporting were expanded. Existing transaction order, retry policy, camera order, database state handling, MQTT/API payloads, UI layout, and weighing workflow are unchanged.

## Changes

- Server log now records image-transfer connection details before every camera upload:
  - transaction and camera number
  - SFTP / FTP / FTPS protocol
  - host and port
  - username (password is never logged)
  - security mode
  - remote directory
  - local file name and byte count
- SFTP success now records a clear server/session result and whether the existing SFTP connection was reused.
- FTP/FTPS uses a temporary WinSCP session log and copies the latest textual server replies (for example 220/234/230/226 responses when available) into the normal iAWS Server log. The temporary WinSCP file is deleted after the operation.
- FTPS certificate failures now distinguish:
  - no trusted/pinned certificate configured
  - configured SHA-256 fingerprint rejected/mismatched
  - TLS failure even while unsafe accept-any-certificate mode is enabled
- FTP/FTPS non-certificate failures now include the WinSCP response plus captured FTP server replies.
- HTTP API success now logs the returned response body, truncated to a safe log length.
- MQTT publish completion now logs the broker-side publish outcome available to the application.
- Server connectivity status changes are now written to the Server log as well as the Status log.

## Example Server Log

```text
TXN <id> CAM1 SFTP CONNECT | host=gcam-ftp.rtsiot.com | port=22 | user=ftpuser | security=SSH/SFTP | remote-dir=/... | local=CAM1.jpg | bytes=123456.
TXN <id> CAM1 SFTP SERVER RESPONSE | SFTP session connected and authenticated; upload completed successfully; remote path=/.../CAM1.jpg.
TXN <id> CAM1 UPLOADED | protocol=SFTP | remote=/.../CAM1.jpg | bytes=123456 | elapsed=1.42s | connection=new.
```

FTPS failures now produce entries similar to:

```text
TXN <id> CAM1 FTPS SERVER RESPONSE | FAILED | FTPS server certificate validation failed... FTPS server reply: 220 ... | 234 ... WinSCP response: ... Peer certificate rejected ...
```

## Configuration baseline
The packaged default remains SFTP on port 22 (`Mode=Sftp`, `UseTls=false`). Existing user-specific settings under `Documents\\Realtech_iAWS\\Config\\appsettings.json` are intentionally not overwritten by this project update.
