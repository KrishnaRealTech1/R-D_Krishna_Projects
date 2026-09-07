# RealTech iTOLL Automation v0.1.40

## Changes

- Fixed both configured and code-level default MQTT transaction topics to `IWS_STP-001/Device_Response`.
- Added startup migration from legacy `STP_COM` transaction topics to `Device_Response`.
- Confirmed direction comes from the physical RFID reader source: IN reader -> IN, OUT reader -> OUT.
- Persisted exceptional approval mode as `Manual` or `Auto`.
- Added `exceptionalApproval.approvalMode` to the device-to-server trip JSON.
- Kept the current approved exception trip on the normal current-lane camera capture, SFTP upload and MQTT publish flow.
- Added the updated orange simple MQTT/SFTP PDF and DOCX to `docs`.
