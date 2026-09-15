# Server Log Visible Response Fix - 15 Sep 2026

This update fixes the issue where the on-screen SERVER LOG showed only the innermost generic WinSCP message (for example `Peer certificate rejected`) and hid the useful FTPS/SFTP server diagnostic.

## Changes

- The newest transaction `FAILED` row now preserves the outer protocol-specific upload exception and server response.
- All server-log messages are collapsed to a single line so one error does not consume the whole small SERVER LOG panel.
- FTPS failures now capture both:
  - FTP/FTPS server reply lines (`220`, `234`, etc.) from WinSCP when available.
  - Safe TLS/connection diagnostic lines, including certificate/fingerprint details when WinSCP provides them.
- WinSCP failures from any relevant exception type are wrapped consistently instead of only `SessionRemoteException`.
- SFTP failures continue to show SSH.NET exception details through the same outer-exception-preserving log path.
- Password/client command lines are not copied into the application SERVER LOG.
- Existing transaction order, retry timing, weighing logic, camera capture, MQTT/API flow, database logic, and UI layout are unchanged.

## Why the previous screen still showed the generic error

`ServerSyncWorker.GetUsefulErrorMessage` walked to the innermost exception. `ImageUploadService` had already created a more useful outer exception containing the FTPS server reply, but that outer message was discarded in the newest `TXN ... FAILED` row. Because the SERVER LOG viewport is small and newest rows are inserted at the top, the generic multiline row also visually hid the earlier `CAMx ... SERVER RESPONSE` row.

## Visible on-screen order after a transfer failure

Because the UI inserts newest log rows at the top, a failed image transfer now ends with compact rows in this order:

1. `<protocol> SERVER/PROTOCOL RESPONSE` - TLS/WinSCP or SSH/SFTP diagnostic, plus next retry time.
2. `<protocol> SERVER REPLY` - FTP/FTPS numeric server replies when available; for SFTP, an explanatory SSH/SFTP message.
3. `TXN ... FAILED` - transaction-level failure summary.

This ensures the response requested by the operator is visible immediately in the SERVER LOG panel.

## FTPS certificate diagnostic improvement

When WinSCP reports `Peer certificate rejected`, the application now performs a best-effort WinSCP SHA-256 fingerprint scan and places the presented server fingerprint at the beginning of the FTPS protocol response when the scan succeeds. This does **not** silently trust the certificate; it gives the operator the exact fingerprint to verify and save in the existing FTPS Certificate SHA-256 Fingerprint field.
