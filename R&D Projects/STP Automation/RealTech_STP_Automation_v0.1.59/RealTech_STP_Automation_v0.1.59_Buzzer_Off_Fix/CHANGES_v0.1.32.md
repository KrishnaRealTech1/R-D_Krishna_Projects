# Changes in v0.1.32

## Password-protected Server Panel

- Replaced the main-window `Open Configuration File` menu action with a dedicated `Server Panel` menu.
- Added password authentication for the Server Panel.
- Default Server Panel password: `rts123!@#`.
- Added `Security.ServerPanelPassword` to `appsettings.json` with backward-compatible default handling for existing installations.

## Configuration editor

- Added an in-application tabbed editor for all application configuration sections:
  - Device and trip processing
  - Hardware and sensor messages
  - IN and OUT cameras
  - MQTT and SFTP server synchronization
  - Storage, connectivity, and vehicle import
  - Exceptional approval, auto approval, and panel passwords
- Added typed validation for numeric ranges, COM ports, RTSP URLs, MQTT settings, SFTP settings, paths, and required values.
- Added masked editors for MQTT, SFTP, Admin Controls, and Server Panel passwords.
- Settings are saved atomically through the existing configuration service.
- Hardware settings are reconnected immediately.
- Camera settings are reloaded immediately.
- Approval settings refresh immediately in the Admin Controls panel.
- Database path changes are saved but explicitly require an application restart.

## Version

- Updated application version to `0.1.32`.
