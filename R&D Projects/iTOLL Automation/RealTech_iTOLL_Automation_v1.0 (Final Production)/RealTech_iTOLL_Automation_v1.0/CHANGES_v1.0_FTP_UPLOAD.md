# v1.0 - FTP image upload support

## Added

- Added `Ftp` to **Server Panel > Server > Image Upload > Mode** alongside `Disabled` and `Sftp`.
- Added standard passive-mode FTP image upload using the existing image-upload Host, Port, Username, Password, Remote Directory, and timeout settings.
- The Server Panel indicates the normal ports: SFTP 22 and FTP 21, and automatically swaps the normal default when the protocol selection changes while preserving custom ports.
- FTP remote directories are created recursively when the server permits it. An existing-directory FTP 550 response is tolerated and the final upload determines whether the path is usable.
- Server connectivity now checks and reports the selected upload protocol (`SFTP` or `FTP`).
- Image-upload completion logs include the protocol used.

## Preserved

- Existing SFTP configuration remains backward compatible, including legacy `Enable`/`Enabled` mode aliases.
- Successful image uploads are still stored in `remote_image_path` and reused on later MQTT retries.
- Total upload timeout and retry cooldown behavior applies to both protocols.

## Security note

`Ftp` means standard FTP, not FTPS. FTP credentials and image data are not encrypted in transit. Use `Sftp` where encrypted transport is required.
