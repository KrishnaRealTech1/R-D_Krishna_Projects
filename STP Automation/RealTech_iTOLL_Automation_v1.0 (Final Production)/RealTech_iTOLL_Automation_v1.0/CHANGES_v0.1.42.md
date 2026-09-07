# RealTech iTOLL Automation v0.1.42

## 1. Complete device identity in MQTT responses

All device-to-server MQTT payloads use the same device identity fields:

- `siteId`
- `deviceId`
- `laneId`
- `deviceName`

This applies to normal IN/OUT trips, Manual/Auto exceptional approval trips and Applied/Rejected/Duplicate recharge acknowledgements.

## 2. Server connectivity status

- Added a **SERVER** status card next to the existing **INTERNET** status card.
- The worker checks reachability of the configured MQTT endpoint.
- When SFTP mode is enabled, it also checks the configured SFTP endpoint.
- Dashboard states:
  - Green: all required endpoints are reachable.
  - Orange: only one required endpoint is reachable.
  - Red: endpoints are offline, disabled or not configured.

The status is a reachability indicator. Normal MQTT and SFTP operations still perform their own authentication and protocol checks.

## 3. Clear Pending Data

- Added **Data > Clear Pending Data...**.
- Displays the current number of pending transactions before deletion.
- Requires an explicit Yes/No confirmation.
- Permanently deletes all `PendingSync` trip records.
- Deletes their local captured image files when the files still exist.
- Does not delete synchronized trip history.
- Removes obsolete entry links safely before database deletion.
- Uses a shared synchronization lock so the background server worker cannot publish a pending transaction during the clear operation.

## 4. Startup loading screen

- Added an orange startup window that appears while the main application is loading.
- Displays a progress percentage and the current startup activity:
  - Loading settings
  - Preparing storage
  - Preparing services
  - Initializing the database
  - Loading the dashboard
  - Starting background connections
  - Opening the main application
- The screen closes immediately after startup completes; no artificial delay is added.

## 5. Version and documentation

- Application version updated from `0.1.41` to `0.1.42`.
- Updated orange MQTT/SFTP guide added to the project `docs` folder.
