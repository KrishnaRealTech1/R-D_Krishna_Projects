# iAWS FTPS Certificate Mismatch + Server Log Fix v3

Date: 15-09-2026

This update is based on the runtime server log where FTPS on `dev.igps.io:21` presented a different SHA-256 certificate fingerprint from the one saved in the iAWS configuration.

## Root cause confirmed

The application had a pinned FTPS SHA-256 fingerprint that did not match the certificate currently presented by the FTPS server. The server also reported a self-signed certificate / hostname validation problem. The application correctly refused to upload instead of silently accepting an unknown certificate.

## Changes in v3

1. Added **Detect FTPS Certificate...** in Server Panel > Server > Image Upload.
   - Scans the certificate currently presented by the configured FTPS server.
   - Shows the current configured fingerprint and the presented fingerprint.
   - Requires an explicit Yes confirmation before copying the presented fingerprint into the settings field.
   - Automatically leaves `Allow Invalid FTPS Certificate` disabled when a fingerprint is selected.
   - User must still choose **Save & Apply** to persist the new fingerprint.

2. Fixed misleading server connectivity status.
   - A TCP connection to port 21 is no longer sufficient to report FTPS as connected when a pinned fingerprint is configured.
   - The connectivity worker scans the presented FTPS SHA-256 fingerprint and compares it to the configured fingerprint.
   - A mismatch is reported as `FTPS certificate mismatch` instead of `Connected (API + FTPS)`.

3. Kept raw server / protocol diagnostics in dedicated SERVER LOG rows while making transaction failure rows concise.
   - `CAMx FTPS SERVER RESPONSE | FAILED | ...` contains the useful summary.
   - `FTPS SERVER REPLY | ...` contains the server reply.
   - `FTPS SERVER/PROTOCOL RESPONSE | ...` contains the WinSCP/TLS diagnostic.
   - The same large diagnostic is no longer duplicated inside the normal transaction failure row.

4. SFTP failures now keep a concise phase-specific message while retaining the detailed SSH/SFTP diagnostic in the dedicated server-response row.

## Important server-side recommendation

The long-term preferred fix is to install a valid FTPS certificate on the server that is issued for the actual FTP hostname and has a valid chain. If a self-signed/private certificate is intentionally used, verify its SHA-256 fingerprint with the server administrator and pin that exact fingerprint using the Server Panel.

Do not enable `Allow Invalid FTPS Certificate` as a permanent solution.

## Existing behavior preserved

No changes were made to weighing logic, RFID logic, barriers, camera capture sequencing, transaction creation, retry timing, database schema, MQTT/API payload logic, or the normal SFTP/FTPS upload sequence.
