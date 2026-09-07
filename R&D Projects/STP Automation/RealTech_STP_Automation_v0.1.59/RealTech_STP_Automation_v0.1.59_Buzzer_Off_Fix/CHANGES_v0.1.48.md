# RealTech STP Automation v0.1.48 Changes

## Multiple Vehicle CSV APIs

- Added editable multiple Vehicle CSV API sources in **Server Panel > Storage & Import**.
- Each source has an enabled flag, priority, name, endpoint, username and request timeout.
- The application posts JSON in the existing format: `{ "username": "value" }`.
- Priority `1` is highest. Lower-priority sources are imported first so higher-priority values win conflicts.
- Legacy single-source settings are retained and migrated automatically.

## Vehicle API automatic synchronization

- Added Vehicle API auto sync with a default interval of 30 seconds.
- The interval is editable and is separate from each source's network timeout.
- Manual and automatic API synchronization share a lock and cannot overlap.
- Failure of one source does not stop the remaining enabled sources.

## Safe repeated imports

- Existing vehicle values are preserved when an API omits optional columns.
- Unchanged repeated rows are skipped instead of rewriting the same vehicle record every 30 seconds.
- Missing balance, access type, active status, empty weight, source site and category/capacity no longer reset local values during repeated sync.
- `vehicle_capacity`, `capacity`, `capacity_liters` and `capacity_litres` continue to map to the payment category/capacity value.

## MQTT payload

- Added `vehicleCategory` to transaction MQTT JSON payloads.
- If an API supplies `vehicle_capacity`, its value is stored as the vehicle category/capacity and is sent in `vehicleCategory`.
- MQTT transaction schema version increased from `1.1` to `1.2`.
- Added a trip database migration so each transaction preserves its category/capacity at processing time.

## Version

- Updated application, assembly and file versions to `0.1.48`.
