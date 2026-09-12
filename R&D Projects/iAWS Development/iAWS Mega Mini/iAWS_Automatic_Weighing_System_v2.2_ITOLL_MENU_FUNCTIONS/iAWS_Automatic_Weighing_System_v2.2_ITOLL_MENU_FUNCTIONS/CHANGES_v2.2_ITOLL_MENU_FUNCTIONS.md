# iAWS v2.2 - Exact iTOLL Menu + Production Functions

This build keeps the existing RealTech iAWS four-camera automatic weighing dashboard and adds the requested iTOLL production menu/function set.

## Top menu

The application now uses the same top-level menu structure as the supplied iTOLL reference:

- File
- Server Panel
- Payment Configuration
- Data
- Connectivity
- Admin Controls
- About

## Functions ported from iTOLL

- Server configuration panel with password protection
- Payment category / price configuration with password protection
- Registered-vehicle CSV import
- Registered-vehicle API synchronization
- Pending transaction clearing
- COM port / IN RFID / OUT RFID / control-unit settings
- Admin simulation and hardware test panel
- Local SQLite vehicle / contractor / trip / recharge repositories
- Vehicle API auto-sync worker
- MQTT publish and recharge synchronization
- Server synchronization worker
- Internet and server connectivity workers
- SFTP / FTP / FTPS image upload support
- Storage cleanup / retention worker
- Exceptional approval and auto-approval settings
- Frontend indicator settings
- iTOLL lane-processing backend and hardware-control commands

## iAWS functions preserved

- Four RTSP camera streams: Front / Back / Left / Right
- Weighbridge serial input
- iAWS RFID reader input
- Weight-triggered capture flow
- Four-camera image capture
- Direct iAWS REST API submission
- Existing iAWS settings panel
- RealTech iAWS logo, application icon, taskbar branding and window titles

## Compatibility

The iTOLL production option model was merged with the iAWS option model instead of replacing it. Therefore the iAWS weighbridge, four-camera and REST settings remain available while the iTOLL Server / Payment / Data / Connectivity / Admin functions use the same configuration object.
