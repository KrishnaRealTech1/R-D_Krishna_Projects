# Testing - v1.0 FTPS WinSCP Transfer Engine

## Reproduction that motivated the fix

1. Explicit FTPS connection on port 21 reaches the server.
2. Certificate is accepted using the configured SHA-256 fingerprint.
3. Previous `FtpWebRequest` upload fails with FTP 426 after part of the image is written.
4. Remote partial image is smaller than the local source.
5. Manual WinSCP upload of the same source file succeeds at full size.

## Required verification after building

1. Restore NuGet packages and build/publish the application.
2. Keep Mode=`Ftp`, Explicit TLS/SSL enabled, port 21, and the verified FTPS certificate fingerprint configured.
3. Delete any old partial copy of the test image on the server.
4. Generate a new IN/OUT transaction.
5. Confirm Server Log reaches image upload success and then proceeds to the remaining transaction sync stages.
6. In WinSCP, verify the remote image exists and its byte size exactly matches the local image.
7. Repeat with at least three images larger than 600 KB.
8. Restart the application and confirm pending retry processing also succeeds.
9. Confirm SFTP mode still uploads normally on a known SFTP endpoint if available.
10. Confirm plain FTP mode still works on a test FTP endpoint if available.

## Expected behavior

- No `(426) Connection closed; transfer aborted` from the previous `FtpWebRequest` path.
- Successful image upload reports protocol `FTPS`.
- If a transfer is interrupted, the transaction remains pending and retries overwrite from byte 0.
- A remote size mismatch is treated as failure and logged; it is never treated as a successful image upload.
