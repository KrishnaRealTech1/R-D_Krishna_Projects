# RealTech iTOLL Automation v1.0 - FTPS WinSCP Transfer Engine

## Reason for change

A production FTPS server accepted manual WinSCP uploads but uploads performed with .NET `FtpWebRequest` were interrupted with FTP status 426 (`Connection closed; transfer aborted`). The failed application transfer left a partial remote image (about 464 KB from a 655,253-byte source), while the same file uploaded completely through WinSCP.

This isolates the failure to FTPS data-channel/TLS interoperability in the previous .NET FTP transport rather than credentials, remote path, certificate pinning, or server write permissions.

## Implementation

- Replaced FTP/Explicit-FTPS image transfer transport in `ImageUploadService` with the WinSCP .NET assembly/transfer engine.
- Added NuGet dependency `WinSCP` 6.5.6.
- SFTP mode continues to use SSH.NET and is otherwise unchanged.
- FTP remains passive mode.
- Explicit FTPS remains available on the configured FTP port (normally 21).
- Existing SHA-256 FTPS certificate fingerprint configuration is passed to WinSCP as the trusted TLS host certificate fingerprint.
- Existing `Allow Invalid FTPS Certificate` setting is preserved as an emergency fallback only.
- Each retry overwrites the destination from byte 0 and disables transfer-resume behavior for deterministic transaction uploads.
- After upload, the remote file size is read back and compared to the local source size. A partial remote file can no longer be reported as success.
- Application total timeout/cancellation aborts the active WinSCP session.

## Production configuration for the current server

Keep the existing settings:

- Mode: `Ftp`
- Port: `21`
- Explicit TLS/SSL (FTPS): enabled
- Passive mode: used by the transfer engine
- SHA-256 certificate fingerprint: the verified fingerprint already entered in Server Panel
- Allow Invalid FTPS Certificate: disabled

## Distribution note

The WinSCP NuGet package includes the WinSCP transfer executable required by its .NET assembly. The project already publishes as self-contained single-file with `IncludeAllContentForSelfExtract=true`, so content dependencies are included/extracted with the published application.
