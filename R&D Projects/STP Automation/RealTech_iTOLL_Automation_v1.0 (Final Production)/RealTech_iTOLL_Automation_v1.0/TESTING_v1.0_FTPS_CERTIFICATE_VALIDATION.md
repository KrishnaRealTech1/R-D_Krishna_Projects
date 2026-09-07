# Testing - v1.0 FTPS Certificate Validation

## Static validation performed

- Verified `appsettings.json` parses as valid JSON.
- Verified new FTPS certificate settings are present in options, Server Panel load/save code, and default configuration.
- Verified fingerprint input validation accepts a 64-hex SHA-256 fingerprint with common separators.
- Verified strict certificate validation remains the default.
- Verified certificate pinning accepts only an exact SHA-256 match when normal TLS validation fails.
- Verified the unsafe bypass is opt-in and defaults to disabled.
- Verified the process-wide `ServicePointManager.ServerCertificateValidationCallback` is serialized for FTPS operations and restored in `finally`.
- Verified certificate rejection produces an operator-facing message containing TLS policy errors and the presented SHA-256 fingerprint.

## Runtime test recommended on the target Windows PC

1. Set Mode = `Ftp`, port = `21`, and enable Explicit TLS/SSL (FTPS).
2. Leave fingerprint blank and unsafe bypass off. Confirm the existing invalid certificate fails and the log displays the presented SHA-256 fingerprint.
3. Verify that fingerprint independently in WinSCP.
4. Paste the verified fingerprint into the Server Panel, Save & Apply, and retry.
5. Confirm `IMAGE UPLOAD COMPLETE | protocol=FTPS` and confirm the image exists in the configured remote directory.
6. Change one character of the pinned fingerprint and confirm the upload is rejected.
7. Restore the correct fingerprint and confirm upload works again.

## Build limitation

The .NET SDK is not installed in the artifact-generation environment, so a full `dotnet build` could not be executed here. Build and runtime FTPS verification should be performed on the Windows development/target machine.
