# RFID Vehicle Access v0.1.18

- Removed the transaction GUID from local and SFTP image filenames.
- Image filenames now contain only the configured prefix, timestamp and lane direction.
- Example: `Tambaram iTOLL Site 1 - 2026-07-16_17-31-18-739 - OUT.png`.
- The identical simplified filename is used for local storage and SFTP upload.
