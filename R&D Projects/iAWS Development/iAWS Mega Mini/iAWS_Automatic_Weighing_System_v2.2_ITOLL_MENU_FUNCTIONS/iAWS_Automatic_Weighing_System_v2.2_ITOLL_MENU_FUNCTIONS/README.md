# iAWS - Automatic Weighing System

This is the API-only weighbridge version of the former gate/toll application.

## Runtime workflow

1. Read live weight from one weighbridge serial port.
2. Read the vehicle RFID from one RFID reader.
3. When `TriggerWeightKg` is reached and the RFID is fresh, lock the cycle.
4. Capture four camera snapshots: Front, Back, Left, Right.
5. Create filenames in the backend-compatible format:
   `<SitePrefix>_CAM_<1..4>_<HHmmss>_<DDMMYYYY>.jpg`
6. POST one JSON event to the configured `/iaws_raw/mega/insert` endpoint.
7. Keep the cycle locked until weight falls to/below `ResetWeightKg`.
8. Re-arm for the next vehicle.

There is no sensor-based trigger, payment/balance workflow, MQTT, FTP/SFTP transaction sync, server panel, recharge sync, or local IN/OUT decision logic in this build.

## API payload

```json
{
  "rf_id": "RF00123456",
  "action": "WEIGH",
  "weight": "4500",
  "front": "AMMAN_KOIL_THAMBARAM_CAM_1_153743_16032026.jpg",
  "back": "AMMAN_KOIL_THAMBARAM_CAM_2_153743_16032026.jpg",
  "left": "AMMAN_KOIL_THAMBARAM_CAM_3_153743_16032026.jpg",
  "right": "AMMAN_KOIL_THAMBARAM_CAM_4_153743_16032026.jpg",
  "dates": "2026-03-16 15:37:43",
  "material_type": "Municipal Solid Waste"
}
```

`material_type` is omitted when it is blank.

## Configuration

Open **Settings** from the main dashboard and configure:

- Full REST API URL
- Site/FTP prefix expected by the backend
- Trigger weight and reset weight
- RFID reader COM port / baud / read mode
- Weighbridge COM port / baud / numeric regex
- Front / Back / Left / Right RTSP camera URLs
- Local image folder

The default configuration is embedded in `src/RfidVehicleAccess.App/appsettings.json` and is copied to:

`Documents\iAWS\Config\appsettings.json`

Images and logs are stored under `Documents\iAWS`.

## Build

Requires Windows and the .NET 8 SDK.

Run:

```bat
build-standalone-exe.bat
```

The self-contained single-file executable is written to:

`Publish\StandaloneWinX64\iAWS.exe`

## Important deployment note

The supplied API specification contains only the relative path `/iaws_raw/mega/insert`, not the server host. Replace `https://YOUR-SERVER/iaws_raw/mega/insert` in Settings with the actual production URL before deployment.
