# Changes in v0.1.16

- Replaced the combined IN and OUT identification text with independent vehicle detail cards.
- Each lane now displays its own current vehicle number, RFID, access type, and balance.
- Added a separate lane-status badge for waiting, detected, processing, approved, rejected, and error states.
- Added state-based status badge colors without mixing the status message into the vehicle details.
- Increased the identification-card width while keeping the camera, signal, and barrier in the same flow row.
- Added stale-result protection so a delayed database lookup cannot overwrite a newer lane vehicle.
- Updated application, assembly, and file versions to 0.1.16.
