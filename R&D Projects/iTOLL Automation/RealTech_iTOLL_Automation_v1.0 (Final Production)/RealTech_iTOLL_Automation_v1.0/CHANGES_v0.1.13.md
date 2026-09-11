# Changes in v0.1.13

## Operator UI

- Replaced the side-by-side IN/OUT presentation with two separate horizontal flow rows.
- Each flow row now includes the lane camera, identification result, signal lamp and barrier state.
- Removed the Simulation / Hardware Test panel from the main dashboard.
- Added a dedicated Admin Controls window containing sensor simulation, RFID injection, barrier, signal and buzzer commands for both lanes.

## Internet status

- Added an independent internet connectivity worker.
- Server synchronization disabled state no longer controls the INTERNET dashboard card.
- Uses active network-interface detection plus configurable HTTP connectivity probes.
- Keeps the status as network-connected when public probes are blocked by a proxy or firewall, avoiding false Offline indications.

## Capture storage

- Added `%REALTECH_SYSTEMS%` path support, resolving to the current user’s `Documents\Realtech_systems` folder.
- Stores snapshots in `Documents\Realtech_systems\Images`.
- Stores status and server logs in `Documents\Realtech_systems\Logs`.
- Copies missing files from legacy application-local `Data\Images` and `Logs` folders on startup.
- Added commands to open the capture folder from the menu and dashboard.
