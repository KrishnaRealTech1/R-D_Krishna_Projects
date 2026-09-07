# RFID Vehicle Access v0.1.17

- Removed the duplicate Current Vehicle and RFID/Type/Balance cards from the right dashboard.
- IN and OUT lane identification cards remain independent inside their respective flow rows.
- Both signal indicators now start green.
- Both boom-barrier indicators now start closed.
- The hardware controller sends CLOSE and GREEN commands for both lanes whenever the control COM port starts or reconnects.
- Rejected/error lanes return to the safe idle state: barrier closed and signal green.
- Added configurable `Cameras.ImageFilePrefix` and `Cameras.ImageTimestampFormat`.
- Local and SFTP image filenames now include the configured prefix, timestamp, lane and transaction ID.
