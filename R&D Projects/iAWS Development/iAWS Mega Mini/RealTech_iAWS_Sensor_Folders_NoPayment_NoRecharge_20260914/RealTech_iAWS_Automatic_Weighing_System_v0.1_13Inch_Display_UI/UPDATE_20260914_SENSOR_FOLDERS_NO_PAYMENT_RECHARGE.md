# iAWS update - 2026-09-14

- Removed Payment Configuration menu/window and payment configuration options.
- Removed all MQTT recharge controls/options and the legacy `rfid_recharges` table.
- iAWS now stores Config/Data/Images/Logs under `Documents\Realtech_iAWS`, separate from iTOLL.
- File menu has separate **Open Logs Folder** and **Open Capture Folder** actions.
- Added **Enable Sensor Interface** in Server Panel > Device & Processing.
  - Enabled: target weight + matching lane sensor + RFID are required.
  - Disabled: target weight + RFID are required (existing iAWS behavior).
- Camera overlays show `Front - CAM 1`, `Rear - CAM 2`, `Left - CAM 3`, `Right - CAM 4`.
