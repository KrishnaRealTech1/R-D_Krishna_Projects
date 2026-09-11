# Changes in v0.1.23

- IN and OUT processing is now order-independent: either the vehicle sensor or
  the RFID may be detected first.
- RFID-first reads are buffered independently for each lane. Processing starts
  only when the same lane also has an active sensor HIGH state.
- Sensor-first detection continues waiting for RFID without rejecting or losing
  the later RFID read.
- An RFID received before the sensor is buffered for
  `Processing.SensorValiditySeconds` (30 seconds in the packaged configuration).
  A sensor HIGH remains active until the matching release command.
- Repeated RFID frames do not start a process without the matching lane sensor.
- One sensor HIGH cycle can start only one lane process, preventing repeated UHF
  frames from creating duplicate transactions for the same vehicle presence.
- IN and OUT matching, processing locks, pending RFIDs, and release handling are
  fully independent.
- Release events that arrive during validation, snapshot capture, database save,
  or the barrier-open transition remain remembered for the active lane cycle.
- Status and server logs are now written to separate date-wise folders:
  - `Logs/Status/YYYY/MM/DD/status.log`
  - `Logs/Server/YYYY/MM/DD/server.log`
- Log folder naming and year/month/day nesting follow the snapshot storage
  structure while preserving the configured status/server filenames.
