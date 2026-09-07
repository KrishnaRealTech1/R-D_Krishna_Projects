# v1.0 - FTP Explicit TLS/SSL (FTPS) support

## Why this change was needed

Some FTP servers use the normal FTP protocol on port 21 but require **TLS/SSL Explicit encryption** (AUTH TLS). WinSCP shows this as:

- File protocol: FTP
- Encryption: TLS/SSL Explicit encryption
- Port: 21

That is commonly called **explicit FTPS**. Plain FTP support alone cannot upload to such a server.

## Changes

- Added `Server.ImageUpload.UseTls` setting.
- Added **Use Explicit TLS/SSL (FTPS)** checkbox in Server Panel > Server > Image Upload.
- FTP requests now set `FtpWebRequest.EnableSsl` from the setting.
- FTP remains passive and binary.
- Existing SFTP behavior is unchanged.
- Plain FTP remains available by leaving the checkbox cleared.
- Logs/status display `FTPS` when FTP + TLS is enabled.

## Configuration for WinSCP "TLS/SSL Explicit encryption"

- Mode: `Ftp`
- Use Explicit TLS/SSL (FTPS): checked
- Port: `21`
- Host / Username / Password: same as WinSCP
- Remote Directory: the desired FTP-visible directory

The server certificate is validated by Windows/.NET. The implementation does not disable TLS certificate validation.
