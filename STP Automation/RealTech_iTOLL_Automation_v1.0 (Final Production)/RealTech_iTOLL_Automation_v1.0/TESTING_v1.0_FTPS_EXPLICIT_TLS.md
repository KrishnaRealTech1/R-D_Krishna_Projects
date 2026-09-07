# Testing - FTP Explicit TLS/SSL

1. Open Server Panel > Server > Image Upload.
2. Set Mode to `Ftp`.
3. Enable `Use Explicit TLS/SSL (FTPS)`.
4. Set port to `21` unless the server uses a custom explicit-FTPS port.
5. Enter the same host, username, password and remote directory used by WinSCP.
6. Save & Apply.
7. Trigger a transaction that captures an image.
8. Confirm Server Log contains `IMAGE UPLOAD COMPLETE | protocol=FTPS`.
9. Confirm the uploaded image exists in the configured remote directory.
10. Clear the TLS checkbox and test against a plain FTP server to verify backward compatibility.
11. Set Mode to `Sftp` and verify existing SFTP uploads still work.

If upload fails with a certificate trust/name error, fix the FTP server certificate or Windows trust chain rather than disabling certificate validation.
