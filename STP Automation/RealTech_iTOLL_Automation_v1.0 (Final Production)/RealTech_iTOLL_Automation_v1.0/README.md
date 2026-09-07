<p align="center">
  <img src="src/RfidVehicleAccess.App/Assets/RealTechiTollAutomationLogo.png" alt="RealTech iTOLL Automation logo" width="220" />
</p>

# RealTech iTOLL Automation

## v1.0 release version

- Application display version is **v1.0**.
- Assembly version is `1.0.0.0`.
- File version is `1.0.0.0`.
- Product branding remains **RealTech iTOLL Automation**.

## v0.1.51 OUT-after-IN RFID handoff fix

- Fixed the v0.1.50 cross-lane guard holding the same RFID until the entire barrier completion sequence finished.
- The winning lane now owns the RFID only while its physical vehicle sensor cycle is active.
- On `IN Realeased`, the IN cross-lane claim is released immediately, so the same RFID can be detected and processed by OUT without waiting for the IN post-release barrier delay.
- The same behavior applies in reverse from OUT to IN.
- Simultaneous same-RFID processing is still blocked while the first lane sensor remains active.
- Claim cleanup is owner-aware so the first lane cannot accidentally remove a newer claim acquired by the opposite lane.
- Application version is `0.1.51`.

## v0.1.50 cross-lane same-RFID collision guard

- The same RFID can no longer be processed by IN and OUT at the same time.
- IN and OUT still operate independently for different vehicles/RFIDs.
- When both lanes match the same RFID concurrently, the first lane that atomically claims the RFID continues; the other lane ignores that cycle.
- A pending copy of the same RFID buffered on the opposite lane is discarded when a lane starts processing it.
- Existing per-lane duplicate-read timing remains unchanged; the new guard only prevents concurrent cross-lane processing of the same RFID.
- Application version is `0.1.50`.

## v0.1.49 exact RFID CSV/API format support

- Supports the exact header order `sno,username,vehicle_no,rf_id,empty_weight,capacity,balance,rfid_type,rf_status` for manual import and Vehicle CSV API synchronization.
- `capacity` is stored as the vehicle category/capacity, used for Paid IN debit price matching, and sent in MQTT as `vehicleCategory`.
- `rfid_type` accepts `FREE` and `PAID`; `rf_status` accepts `ACTIVE` and `INACTIVE`.
- `sno` is accepted as an informational serial-number column and is intentionally ignored by the database import.
- Excel XLSX/XLSM content accidentally renamed with a `.csv` extension is detected automatically during manual import.
- Application version is `0.1.49`.

## v0.1.48 multiple vehicle APIs, auto sync and MQTT category/capacity

- Multiple Vehicle CSV API sources can be added and edited in **Server Panel > Storage & Import**.
- Every enabled source is called with the existing JSON payload `{ "username": "value" }`.
- Vehicle API auto sync is enabled by default and runs at an editable 30-second interval.
- Each source has its own editable request timeout; timeout and sync interval are separate settings.
- Repeated API imports preserve existing local values when optional columns are omitted, and unchanged rows are not rewritten.
- `vehicle_capacity` and related capacity headers are used for category-based Paid IN debit configuration.
- Transaction MQTT payload schema `1.2` includes `vehicleCategory`; a `vehicle_capacity` API value is published in that field.
- Application version is `0.1.48`.

## v0.1.45 automatic storage retention

- The protected **Server Panel > Storage & Import > Storage** section now includes automatic image/log deletion.
- Automatic deletion is enabled by default.
- Image retention defaults to `30` days and log retention defaults to `30` days.
- Both retention values are editable from the Server Panel and accept 1 to 3650 days.
- Cleanup runs when the application starts, after **Save & Apply**, and every 24 hours while the application remains open.
- Only files stored inside the application's `YYYY/MM/DD` image and log folders are considered for deletion; the database is never deleted.
- Added `sample-data/vehicle_import_liter_capacity_sample.csv` with `10000 Liter Capacity` and `6000 Liter Capacity` examples.
- Application version is `0.1.45`.

## v0.1.44 vehicle-category payment configuration

- A new password-protected **Payment Configuration** menu manages vehicle categories and IN entry prices.
- The default password is `7799`.
- The price list starts with one editable `Default` category using the previous fixed `Processing.EntryFee` value.
- Operators can add, edit and delete categories, choose the fallback default category, and save changes without restarting.
- Vehicle imports accept a category/capacity column. Exact names, spacing-insensitive names, and unambiguous numeric capacity values are matched; blank or unmatched categories use the selected default price.
- Paid IN access now debits the price configured for the vehicle category; FREE RFID access remains zero.
- Existing databases are upgraded automatically with a `vehicle_category` column.
- Application version is `0.1.44`.

## v0.1.43 missing-trip notification only

- A missing IN or missing OUT is no longer created as a separate synthetic trip.
- Only the physical trip detected by the IN/OUT RFID reader is saved as `PendingSync`, uploaded and counted.
- The physical trip MQTT JSON includes `missingTripNotification` and `exceptionalApproval` so the server is clearly informed about the missing direction and approval details.
- `missingTripNotification.notificationOnly` is `true`, `syntheticTripCreated` is `false`, and `countedAsTransaction` is `false`.
- When a new IN is approved while an earlier IN is still active, the earlier IN is closed internally without creating an OUT transaction.
- When an OUT is approved without an active IN, the OUT is saved with no synthetic `entryTransactionId`.
- Legacy `NotCaptured-ExceptionalReconciliation` rows are archived locally during database startup and excluded from Pending and Processed dashboard counts.
- Application and documentation version is `0.1.43`.

## v0.1.42 server status, pending-data clear and startup loading

- Every device-to-server MQTT response includes `siteId`, `deviceId`, `laneId` and `deviceName`.
- The dashboard now shows **SERVER** status next to **INTERNET** status.
- Server status checks the configured MQTT port and the enabled SFTP port and shows connected, partially connected or offline.
- **Data > Clear Pending Data...** permanently removes pending transactions and their local captured images after an operator confirmation.
- Pending-data maintenance is serialized with the background synchronization worker so the same pending transaction is not published while it is being cleared.
- A visible orange startup loading window reports configuration, storage, database, services and dashboard progress while the application opens.
- The application and documentation version is `0.1.42`.

## v0.1.40 Device_Response and exceptional approval identification

- The default vehicle transaction topic is fixed to `IWS_STP-001/Device_Response`.
- RFID direction is taken directly from the physical reader: the IN reader creates an `IN` trip and the OUT reader creates an `OUT` trip.
- After Manual or Auto exceptional approval, the current detected IN/OUT trip follows the normal camera capture, SFTP upload and MQTT publish flow.
- The `exceptionalApproval` JSON object now includes `approvalMode` with value `Manual` or `Auto`, together with reason, approver name, role, mobile and approval time.
- Existing databases are upgraded automatically with the `exceptional_approval_mode` trip column.

This repository contains the Windows standalone RealTech STP automated vehicle access application.

## v1.0 FTP image upload support

- Server Panel > Server > Image Upload now supports `Disabled`, `Sftp`, and `Ftp`.
- Existing SFTP behavior remains available and backward compatible.
- FTP uses the same Host, Port, Username, Password, Remote Directory, and timeout fields; the normal FTP port is 21.
- Server connectivity and server logs now report the active image-upload protocol.
- Standard FTP is passive-mode and unencrypted; use SFTP when encrypted transport is required.


## What this baseline includes

- Responsive Windows WPF desktop dashboard that starts maximized and adapts to common controller-display resolutions.
- Separate **Status Log** and **Server Log** panels.
- Local SQLite database.
- CSV/XLSX/XLSM vehicle master import.
- Mapping from RFID number to vehicle number.
- Registered/inactive/unregistered handling.
- FREE and PAID RFID types.
- Local paid-wallet debit logic.
- One-time entry when the current paid balance is zero or positive but lower than the entry fee.
- Rejection when the current paid balance is already negative.
- Active IN-trip validation.
- Lane-specific, admin-configurable exceptional approval for missing IN or OUT trips.
- Approver name, role and mobile-number capture for exceptional trips.
- Missing-trip notification is included in the current physical trip; no synthetic trip is created.
- OUT processing links to a real unmatched IN trip when available; missing-IN approval is notification-only.
- IN/OUT vehicle-presence sensor requirement.
- Configurable RFID prefix validation.
- Configurable duplicate-read interval.
- Configurable process wait, signal duration, barrier close and display reset times.
- Serial-port adapters for the two RFID readers and one control unit.
- Admin-only manual barrier, signal-light and buzzer controls.
- Separate enable/disable controls for IN and OUT exceptional approval.
- Hardware simulation controls in a separate **Admin Controls** window.
- Local transaction queue marked `PendingSync`.
- Live IN/OUT RTSP rendering with automatic reconnect and transaction snapshot capture.
- Server worker publishes MQTT transaction payloads and uploads images by the configured SFTP or FTP mode.

## Important architecture rule

The gate process is local-first and does not wait for the server:

```text
Vehicle sensor + RFID (either may arrive first) -> local vehicle lookup -> local decision
-> snapshot -> local transaction -> barrier control -> asynchronous server upload
```

An internet, MQTT, FTP or SFTP failure must not stop normal local gate processing.

## Development environment

- Windows 10 or Windows 11
- Visual Studio 2022 with **.NET desktop development** workload
- .NET 8 SDK

WPF is a Windows-only desktop UI framework on .NET. The project targets `net8.0-windows`.

## Open and run

1. Extract the ZIP.
2. Open `RfidVehicleAccess.sln` in Visual Studio 2022.
3. Restore NuGet packages.
4. Build the solution.
5. Run `RfidVehicleAccess.App`.

The first run creates all writable configuration, database, image and log files in the current Windows user’s Documents folder:

```text
<Documents>/Realtech_systems/Config/appsettings.json
<Documents>/Realtech_systems/Data/vehicle-access.db
<Documents>/Realtech_systems/Images/
<Documents>/Realtech_systems/Logs/Status/YYYY/MM/DD/status.log
<Documents>/Realtech_systems/Logs/Server/YYYY/MM/DD/server.log
```

Existing files from the legacy application-side `appsettings.json`, `Data` and `Logs` locations are migrated when the destination file does not already exist.

## Build one standalone EXE

1. Install the .NET 8 SDK on the Windows build computer.
2. Double-click `build-standalone-exe.bat`.
3. Distribute `Publish\StandaloneWinX64\RealTechiTOLLAutomation.exe`.

The resulting Windows x64 application is self-contained, so the destination computer does not need a separate .NET installation. The embedded default configuration is extracted to `Documents\Realtech_systems\Config\appsettings.json` on first launch. Native camera/VLC components are bundled and may be extracted to the user’s temporary folder while the program runs. See `STANDALONE_EXE.md` for details.

## First simulation test

Real hardware mode is enabled by default. To test without connected devices:

1. Open **Connectivity -> COM Port Settings...**.
2. Select **Simulation mode (do not open COM ports)**.
3. Click **Save & Connect**.
4. Select **Data -> Import Registered Vehicles**.
5. Import `sample-data/rf_id_import.csv`.
6. The existing file has no `rfid_type` column, so records use the configured default type, which is `Free`.
7. Copy one RFID value from the imported file.
8. Open **Admin Controls -> Open Simulation / Hardware Test Panel...**.
9. Click the IN **Detect Vehicle** button.
10. Paste the RFID into the IN simulation box and click **Send RFID**.
11. Observe the vehicle number, status log and simulated barrier commands.
12. After entry completes, click the OUT **Detect Vehicle** button.
13. Paste the same RFID into the OUT simulation box and click **Send RFID**.

## Vehicle import columns

Recognized column aliases are:

| Purpose | Accepted headers |
|---|---|
| RFID | `rf_id`, `rfid`, `rfid_number`, `tag_number` |
| Vehicle number | `vehicle_no`, `vehicle_number`, `vehicleno` |
| Source site | `username`, `site`, `source_site` |
| Vehicle category / capacity | `vehicle_category`, `category`, `vehicle_capacity`, `capacity`, `capacity_liters`, `capacity_litres` |
| Access type | `rfid_type`, `access_type`, `type` |
| Balance | `balance`, `wallet_balance`, `amount` |
| Empty weight | `empty_weight`, `tare_weight` |
| Active status | `rf_status`, `active`, `is_active`, `status` |

Valid access types:

```text
FREE
PAID
```

When `rfid_type` is missing, `Import.DefaultAccessType` from `appsettings.json` is used.

Import behavior is intentionally separate from live-lane validation:

- `Import.EnforceRfidPrefixValidation` defaults to `false`, so the supplied master file can register non-`E2` identifiers without weakening the live reader rule.
- `Import.SkipInvalidRows` defaults to `true`, so incomplete rows and conflicting repeated RFID assignments are reported as review notes and skipped instead of failing the whole import.
- Repeated rows for the same RFID and equivalent vehicle number are consolidated. The first valid row is retained.
- RFID values are normalized to uppercase and all embedded whitespace is removed before storage and lookup.

For the supplied `sample-data/rf_id_import.csv`, a new empty database should report:

```text
Rows: 3150, Inserted: 2154, Updated: 0, Skipped: 996, Failed: 0
```

The 996 skipped rows consist of 991 repeated RFID rows and 5 incomplete rows. Eleven of the repeated rows assign the same RFID to a different vehicle and are listed for review.

## Current command strings

```text
OPEN IN BB
CLOSE IN BB
OPEN OUT BB
CLOSE OUT BB
IN RED
IN GRN
IN ORG
IN Buzzer
IN Buzzer OFF
IN ALL OFF
OUT RED
OUT GRN
OUT ORG
OUT Buzzer
OUT Buzzer OFF
OUT ALL OFF
```

Control-unit sensor input strings:

```text
IN Detected
OUT Detected
```

The exact command casing, terminator and serial packet framing must be confirmed with the hardware samples before field deployment.

## Main configuration

Edit `src/RfidVehicleAccess.App/appsettings.json` while developing. The standalone EXE embeds this file as the first-run default. At runtime, the editable configuration is stored at `Documents\Realtech_systems\Config\appsettings.json`.

Key sections:

- `Device`: site/device identity.
- `Processing`: timers, legacy fee fallback and prefix rules.
- `Payment`: default vehicle category and editable category-price list.
- `Hardware`: three serial-port settings and simulation mode.
- `Cameras`: IN/OUT RTSP URLs, TCP/caching/reconnect settings and local image folder.
- `Server`: MQTT plus SFTP/FTP image-upload configuration.
- `Import`: default FREE/PAID behavior, import-only prefix checking and invalid-row handling.
- `Storage`: database and log paths.
- `ExceptionalApproval`: independent `InEnabled` and `OutEnabled` switches.

Restart the program after changing configuration.

## Database rules implemented

### IN

- Sensor detection must be valid.
- RFID prefix must be valid.
- RFID must be registered and active.
- A second unmatched IN trip is treated as a **Missing OUT Trip**.
- When `ExceptionalApproval.InEnabled` is enabled, the Exceptional Approval popup opens automatically and requires approver name, role and mobile number.
- When IN exceptional approval is disabled, the second unmatched IN is rejected immediately without opening the popup.
- When approved, the earlier active IN is closed internally, the current IN continues, and the MQTT JSON reports `Missing OUT Trip`. No OUT trip row is created.
- Cancelling the popup rejects the current IN trip and keeps the barrier closed.
- FREE RFID: debit `0`.
- PAID RFID with balance `< 0`: rejected.
- PAID RFID with balance `>= 0`: entry allowed and the configured vehicle-category price is deducted.
- This naturally allows one transaction to move a non-negative insufficient balance into negative.
- Trip and balance update are saved in one SQLite transaction.

### OUT

- Sensor detection must be valid.
- RFID must be registered and active.
- A valid unmatched IN trip is used when available.
- When no IN trip exists and `ExceptionalApproval.OutEnabled` is enabled, the case is treated as a **Missing IN Trip** and the Exceptional Approval popup opens automatically.
- When OUT exceptional approval is disabled, the unmatched OUT is rejected immediately without opening the popup.
- When approved, only the current physical OUT is saved and its MQTT JSON reports `Missing IN Trip`. No IN trip row is created.
- Cancelling the popup rejects the OUT trip and keeps the barrier closed.
- No amount is debited.
- OUT trip references the corresponding real IN trip when available. For approved missing-IN cases, `entryTransactionId` is omitted.

## Still pending before production use

1. Exact RFID reader model and packet samples.
2. Exact COM-port line terminators and framing.
3. Final control-unit command syntax.
4. Confirm camera stream stability, latency and snapshot quality on the site network.
5. Confirm production MQTT broker ACLs, TLS certificates and final topic naming.
6. FTP/SFTP image path and credential requirements.
7. Confirm server-side reconciliation behavior when `serverBalanceMatched` is false.
8. Full editable settings window instead of opening JSON.
9. Admin transaction correction screens and audit trail UI.
10. Installer, automatic startup, database backup and recovery.
11. Hardware-in-loop and site acceptance testing.

## Recommended implementation order

1. Verify this local simulator and import workflow.
2. Integrate RFID readers.
3. Integrate vehicle sensors and control commands.
4. Integrate RTSP cameras.
5. Add full settings UI.
6. Integrate MQTT and FTP/SFTP.
7. Add reconciliation/admin screens.
8. Create signed installer and perform endurance testing.

## Build fix in v0.1.1

Added an explicit global `System.IO` import for WPF builds. This provides `Path`, `File`, `Directory`, `StreamReader`, and `FileNotFoundException` throughout the project.


## v0.1.2 UI behavior

The original v0.1.2 dashboard used a fixed 1366 × 768 window. That restriction is superseded by the responsive display handling introduced in v0.1.8.

## v0.1.4 fixed dashboard alignment

The right-side operational dashboard now spans the full usable window height. IN and OUT have independent standard RED/ORANGE/GREEN signal-light graphics, while the network and boom-barrier states use consistent green/red status colours. Camera feeds and hardware controls have been resized and aligned to keep the full controller visible without page-level scrolling.


## v0.1.5 dashboard counters and signal lamps

- The live dashboard no longer displays the registered-vehicle total.
- `PROCESSED` counts only transactions whose server synchronization completed today (`status = Synced` and `synced_at` is within the current local day).
- `PENDING` counts transactions still marked `PendingSync`.
- The processed counter refreshes at local midnight and therefore starts each day at zero.
- Lane signal cards no longer display colour/status text. The lamp matching the last `IN/OUT RED`, `ORG`, or `GRN` command glows; `ALL OFF` dims every lamp.

The supplied server worker is still a protocol placeholder. Once the real uploader calls `MarkSyncedAsync` after a confirmed server upload, the transaction automatically moves from the pending count into the processed count.


## v0.1.6 live RTSP cameras

- Uses `LibVLCSharp.WPF` for the two WPF video surfaces.
- Uses the Windows LibVLC runtime delivered through NuGet.
- RTSP-over-TCP is enabled by default.
- Each stream retries automatically after a configured delay.
- A transaction snapshot is captured from the live player; a one-pixel placeholder is saved only when the stream is unavailable.
- Camera credentials are stored in `Documents\Realtech_systems\Config\appsettings.json`; restrict access to that configuration folder.

## v0.1.7 RTSP frozen-frame fix

- Both gate cameras use software decoding by default because some Windows graphics drivers show the first Hikvision H.264 frame but do not continue rendering reliably with hardware decoding.
- `NetworkCachingMilliseconds` defaults to `1200` for stable local-network playback.
- `StreamWatchdogSeconds` defaults to `12`; a stream whose playback timestamp stops advancing is automatically restarted.
- Hardware decoding can be tested later by changing `EnableHardwareDecoding` to `true` for one camera at a time.

## v0.1.8 responsive display sizing

- The application starts maximized within the Windows working area instead of forcing a 1366 × 768 window beyond the screen edges.
- The window can be restored and resized.
- The right operational dashboard scales down on smaller or high-DPI displays without applying a transform to the LibVLC camera views.
- Main dashboard columns and the hardware-test panel use bounded responsive widths.
- The layout is intended to remain fully accessible at 1024 × 768 and larger display work areas.

## v0.1.9 square display and COM configuration

The dashboard is optimized for 13-inch 4:3/5:4 screens, especially 1024x768 and 1280x1024.

To configure serial hardware while the application is running:

1. Open **Connectivity > COM Port Settings...**.
2. Select separate ports for IN RFID, OUT RFID, and the control unit.
3. Select each baud rate.
4. Keep **Simulation mode** unchecked for real hardware.
5. Click **Save & Connect**.

The program saves the values to `Documents\Realtech_systems\Config\appsettings.json`, closes old serial connections, and opens the selected ports immediately. Failed connections are retried automatically.

## v0.1.10 binary UHF reader packets

The supplied UHF reader does not send a CR/LF-delimited RFID string. It continuously emits binary inventory frames beginning with `CF`. The application now buffers the serial byte stream and extracts the EPC from complete frames, including when Windows splits a frame across reads or combines several frames into one read.

The observed frame:

```text
CF 00 00 01 12 00 FD 8A 01 00 0C E2 80 69 15 00 00 40 20 ED 96 48 B3 50 57
```

is decoded as:

```text
E280691500004020ED9648B3
```

RFID serial settings now include `ReadMode`:

- `UhfCfFrame` for the binary UHF readers.
- `LineText` for newline-delimited devices such as the control unit.

Close the vendor UHF utility and any PowerShell serial test before starting this application, otherwise the application cannot open the same COM port.


## v0.1.11 operator dashboard alignment

The live camera area is shorter so the Vehicle Identification IN/OUT panels have more room. Both identification panels now use the same bordered display layout and larger text. The right-side RFID, TYPE and BALANCE card is wider and uses larger labels and values.

The supplied `sample-data/rf_id_import.csv` contains the provided 3,150 vehicle rows. It does not yet contain `rfid_type` or `balance`, so imports use `Import:DefaultAccessType` (`Free`) and `Import:DefaultOpeningBalance` (`0`). Because `Import:UpdateExistingRecords` is enabled, importing the later PAID/FREE list will update matching RFID records.

## v0.1.12 tolerant master-data import

- Import prefix checking is now independent from live RFID lane validation. The provided CSV can register its numeric, test and ANPR-style identifiers while the real-time reader can continue enforcing the configured `E2` prefix.
- RFID normalization removes all whitespace and stores uppercase values, matching the EPC format emitted by the binary UHF decoder.
- Empty RFID/vehicle rows are skipped with review notes when `Import:SkipInvalidRows` is enabled.
- Duplicate RFID rows are consolidated deterministically. Equivalent vehicle numbers with spacing differences are treated as the same vehicle; conflicting assignments keep the first valid row and are shown for review.
- The supplied 3,150-row CSV now completes with 0 failed rows on an empty database, producing 2,154 unique registrations and 996 skipped duplicate/incomplete rows.



## v0.1.13 separate lane flows, internet status and capture storage

- The operator dashboard now presents IN and OUT as separate horizontal flow rows. Each row contains its own live camera, vehicle identification, traffic signal and barrier state.
- The Simulation / Hardware Test area was removed from the main operator screen. All sensor simulation, RFID injection, barrier, signal and buzzer commands are available from **Admin Controls**.
- The INTERNET card now monitors Windows network availability and lightweight public connectivity endpoints. Server synchronization being disabled no longer forces the application to display Offline.
- Camera snapshots are stored under `Documents\Realtech_systems\Images`, organized by year, month, day and IN/OUT lane.
- Status and server logs are stored separately under `Documents\Realtech_systems\Logs\Status` and `Documents\Realtech_systems\Logs\Server`, organized by year, month, and day. Existing legacy images and logs are copied into the new location without overwriting files.
- **File -> Open Capture Folder** and the dashboard **Open Folder** button open the storage location directly.


## v0.1.15 MQTT and SFTP synchronization

The server worker now uploads captured images by SFTP and publishes transaction JSON by MQTT.
With QoS 1, a local transaction is marked synced only after the broker returns PUBACK. The
configuration can select the publish topic, QoS, retain flag, timeouts and maximum batch size.

The current generic JSON payload is documented in `CHANGES_v0.1.15.md`. When the server has a
separate application-level acknowledgement topic, extend the worker to wait for that acknowledgement
before calling `MarkSyncedAsync`.


## v0.1.37 RFID recharge synchronization

The application can now receive Paid RFID recharge commands from the server through MQTT, apply the
recharge to the latest local balance, store an audit record, and publish an application-level
acknowledgement. Duplicate delivery is safe because every command uses a unique `rechargeId`.

Default topics:

- MQTTX wildcard subscription: `IWS_STP-001/#`
- Gate transaction: `IWS_STP-001/Device_Response`
- Recharge command: `IWS_STP-001/STP_RECHARGE_FROM_SERVER`
- Recharge acknowledgement: `IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE`

The complete JSON formats and MQTTX test procedure are documented in `SERVER_INTERFACE.md`.

## v0.1.39 device-root MQTT topics and leaner timestamps

All MQTT topics for this controller now use the device ID as their first topic level. Subscribe to
`IWS_STP-001/#` in MQTTX instead of maintaining separate `Device_Response/#` and `STP_RECHARGE/#`
subscriptions. Transaction JSON no longer publishes the duplicate `tripTimestamp`,
`tripTimestampUtc`, `approvedAt`, or `approvedAtUtc` properties.

## v0.1.17 lane vehicle cards

The IN and OUT flow rows now contain separate current-vehicle cards. Each card shows vehicle number,
RFID, access type, balance, and a separate state-colored access-status badge. Lane values are updated
independently, so an OUT event does not replace the IN card and vice versa.

## Image filename prefix (v0.1.17)

Edit these values in `src/RfidVehicleAccess.App/appsettings.json` before publishing, or after first launch in `Documents\Realtech_systems\Config\appsettings.json`:

```json
"Cameras": {
  "ImageFilePrefix": "Tambaram iTOLL Site 1",
  "ImageTimestampFormat": "yyyy-MM-dd_HH-mm-ss-fff"
}
```

A saved and uploaded capture is named like:

```text
Tambaram iTOLL Site 1 - 2026-07-16_16-44-57-123 - IN - <transaction-id>.png
```

Invalid Windows filename characters are replaced automatically. The same filename is used in `Documents\Realtech_systems\Images` and on SFTP.

At startup, the controller commands both IN and OUT signals to green and both boom barriers to closed. The dashboard no longer duplicates the current-vehicle/RFID/type/balance cards in the right sidebar.

## UNO release-command barrier completion (v0.1.22)

The approved vehicle sequence is:

1. The UNO sends `IN Detected` or `OUT Detected`, and the RFID is read.
2. Existing validation, image capture, local save, and asynchronous server-sync
   preparation complete.
3. The orange signal and buzzer turn on, and the boom barrier opens.
4. The app waits specifically for `IN Realeased` or `OUT Realeased`. Raw LOW,
   CLEAR, OFF, NOT DETECTED, and NO VEHICLE messages are ignored.
5. When the matching release command arrives, the configured completion delay
   starts. Orange, buzzer, and the open barrier remain active during the delay.
6. After `Processing.BarrierAndGreenDelaySeconds`, the boom barrier closes, all
   lane outputs are cleared, and the green signal turns on.

The release cycle is armed before the OPEN command and completed only after OPEN
has been issued. Therefore, a release command arriving during the processing/open
transition is remembered without allowing the close timer to start before the
barrier is open. Duplicate release commands cannot repeat the same lane cycle.

Configure the common close/green delay in `appsettings.json`:

```json
"Processing": {
  "BarrierAndGreenDelaySeconds": 5
}
```

The default control-unit messages can also be changed to match the connected UNO:

```json
"Hardware": {
  "SensorMessages": {
    "InHigh": "IN Detected",
    "InReleased": "IN Realeased",
    "OutHigh": "OUT Detected",
    "OutReleased": "OUT Realeased"
  }
}
```

The app also accepts the correctly spelled `IN Released` and `OUT Released` aliases.


### Admin Controls password

Opening **Admin Controls** now requires a password. The default is `7799` and is stored
in `appsettings.json`:

```json
"Security": {
  "AdminControlsPassword": "7799"
}
```

## Sensor/RFID order-independent matching and dated logs (v0.1.23)

A lane no longer requires the sensor event to arrive before the RFID event. The
application keeps the first available condition and starts processing only after
both conditions are present for the same lane:

```text
Sensor HIGH first -> wait for RFID -> both available -> process
RFID first -> buffer RFID -> wait for Sensor HIGH -> both available -> process
```

IN and OUT maintain separate matching state. A sensor HIGH remains active until
the matching release command. An RFID received first is buffered for
`Processing.SensorValiditySeconds`; the packaged value is 30 seconds. Repeated
RFID frames do not start processing without the matching lane sensor, and one
sensor HIGH cycle can start only one lane process.

Status and server logs now use separate date-wise folders, following the same
year/month/day pattern as snapshots:

```text
<Documents>/Realtech_systems/Logs/Status/YYYY/MM/DD/status.log
<Documents>/Realtech_systems/Logs/Server/YYYY/MM/DD/server.log
```


## Reliable UNO release-event completion (v0.1.24)

The UNO lane release line is treated as an explicit completion event for the
active lane transaction. The application does not require another local sensor
state transition before unblocking the approved barrier sequence.

```text
Processing complete -> ORG ON + barrier OPEN
UNO sends IN/OUT Realeased -> configured delay starts
Delay expires -> CLOSE barrier -> ALL OFF -> GRN ON
```

The event remains valid if it arrives before the barrier has finished opening.
Repeated release lines are safe and do not execute the close sequence twice.
Recognized control-unit lines are written to the status log for field diagnosis.


## Vehicle-number RED commands and full-width logs (v0.1.25)

For a registered RFID, the automatic RED command now carries the complete
vehicle number to the control unit, including embedded spaces:

```text
IN RED TN 56 H 8658
OUT RED KA 01 AB 1234
```

The UI continues to mark the correct lane signal as red for both plain manual
RED commands and automatic RED commands containing a vehicle number.

The dashboard layout now keeps Capture Storage compact at the lower-right edge
of the IN/OUT flow area. Status Log and Server Log occupy the complete width
below the lane flows and dashboard.

## Unregistered RFID RED command (v0.1.26)

When an RFID has no matching vehicle registration, the application sends a display-friendly placeholder as the vehicle number:

```text
IN RED NOT Registered
OUT RED NOT Registered
```

This keeps unregistered scans compatible with the control unit's two-stage RED display flow.



## Exceptional approval and dynamic About version (v0.1.28 update)

The About menu now reads the executable assembly version at runtime instead of
using a hard-coded version string. Updating the version in the project file is
enough for future builds.

Missing-trip validation now follows this workflow:

```text
Missing OUT: new IN scan finds an earlier unmatched IN
-> popup opens automatically (or Auto approval is used)
-> approval details are recorded
-> the earlier IN is closed internally
-> only the current physical IN is saved, uploaded and counted
-> MQTT reports `missingDirection: OUT`

Missing IN: OUT scan finds no unmatched IN
-> popup opens automatically (or Auto approval is used)
-> approval details are recorded
-> only the current physical OUT is saved, uploaded and counted
-> no synthetic IN and no synthetic `entryTransactionId`
-> MQTT reports `missingDirection: IN`
```

Only the current physical trip is committed as a transaction. For a missing OUT,
the earlier active IN is marked internally as exceptionally closed in the same SQLite
transaction. Approval details, reason and approval time are stored in both the
current trip notes and dedicated SQLite columns. Every IN and OUT server payload carries
local and UTC trip timestamps. Exceptional transactions additionally carry a
structured approval object with its own local and UTC approval timestamps.
Closing or cancelling the popup rejects the current movement.

Exceptional Approval input validation requires the approver name to contain
alphabets and spaces only. The mobile number accepts ASCII digits only and must
contain between 7 and 15 digits. Invalid typed or pasted characters are rejected
before approval is submitted.
