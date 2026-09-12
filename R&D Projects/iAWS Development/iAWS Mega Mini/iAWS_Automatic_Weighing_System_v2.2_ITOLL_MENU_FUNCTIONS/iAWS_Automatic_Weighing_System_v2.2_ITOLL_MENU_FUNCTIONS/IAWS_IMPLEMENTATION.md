# iAWS implementation changes

## Removed from runtime and UI

- IN/OUT vehicle-presence sensor workflow
- Separate IN and OUT lanes
- Payment configuration, entry fees, balances, recharge and authorization
- MQTT publish/subscribe
- FTP/SFTP/FTPS image/server synchronization
- Server panel and legacy server connectivity workers
- Vehicle CSV/API import and local access validation
- Local IN/OUT trip determination

## Added

- One RFID reader
- One weighbridge serial input
- Configurable trigger and reset weights
- Four independent RTSP cameras
- Four-image synchronized capture per weighing event
- REST JSON submission using the MEGA raw insert payload
- Weight-first cycle state machine
- New white iAWS dashboard and startup screen
- New iAWS configuration window
- Local image/API/status logging under `Documents\iAWS`
- Simulation controls for RFID and weight testing

## Trigger state machine

`READY -> RFID READY / WAITING FOR RFID -> CAPTURING 4 CAMERAS -> SENDING API -> PROCESSED/API FAILED -> wait for scale clear -> READY`

The client intentionally does not infer IN/OUT. The backend API owns that logic.
