# RealTech STP Automation v0.1.45 Changes

## Automatic local storage cleanup

- Added `Storage.AutoDeleteEnabled`, enabled by default.
- Added editable `Storage.ImageRetentionDays`, default `30`.
- Added editable `Storage.LogRetentionDays`, default `30`.
- Added the settings to **Server Panel > Storage & Import > Storage**.
- Cleanup runs at application startup, after **Save & Apply**, and every 24 hours.
- Image cleanup targets the configured camera image root and its `YYYY/MM/DD` folders.
- Log cleanup targets the configured Status and Server log roots and their `YYYY/MM/DD` folders.
- Files outside the application's date-folder layout are left untouched.
- Empty date folders are removed after their old files are deleted.
- The SQLite database and vehicle records are not part of automatic cleanup.

## Vehicle import sample

- Added `sample-data/vehicle_import_liter_capacity_sample.csv`.
- The sample contains `vehicle_category` values for `10000 Liter Capacity` and `6000 Liter Capacity`.
- Category names should match the names configured in **Payment Configuration**. Numeric capacity-only values are also accepted when they match one configured category unambiguously.

## Version

- Updated application version to `0.1.45`.
