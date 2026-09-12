# RealTech iAWS - Automatic Weighing System v0.1

Windows WPF application for one shared weighbridge, two directional RFID readers (IN / OUT), four editable RTSP cameras, lane hardware control and server synchronization.

## Final v0.1 operating rule

There is **one physical weighbridge** shared by IN and OUT.

1. Weight is continuously received from the configured weight COM port.
2. A sample line such as `wn009011 kg` is parsed as `9011 kg`.
3. When `Live Weight >= Target Weight`, the weighing cycle becomes **ARMED**.
4. The **first valid RFID** received from either IN or OUT atomically owns the cycle.
5. If IN RFID wins, all OUT RFID events are ignored for that cycle. If OUT wins, all IN RFID events are ignored.
6. The winning direction captures Camera 1-4 images and stores the weight/RFID transaction locally.
7. Transaction data and captured images are synchronized to the configured server asynchronously.
8. The selected boom barrier/signal/buzzer sequence is operated.
9. The direction lock remains active until the vehicle leaves and the live weight falls to `Reset Weight` or below.
10. Only after reset can the next vehicle arm a new cycle.

This version does **not** use toll/payment processing.

## Four editable cameras

Camera 1, Camera 2, Camera 3 and Camera 4 each have their own editable RTSP URL and enable/disable switch in **Server Panel > Cameras**. Camera settings are not hard-coded.

Camera 3 and Camera 4 are disabled by default until their actual RTSP URLs are configured.

## Main dashboard layout

The dashboard follows the supplied iAWS reference layout:

- Camera 1 / Camera 2 on the first row.
- Camera 3 / Camera 4 on the second row.
- Right side: iAWS logo, date/time, internet/server status, hardware COM status and large live weight display.
- IN section: RFID, R/O/G signal indication, buzzer and boom barrier state.
- OUT section: RFID, R/O/G signal indication, buzzer and boom barrier state.
- Bottom: Server Log and Status Log.

Reference images are included under `docs/`.

## Weight configuration

The following are editable from the Hardware Settings and/or Server Panel:

- Weight COM port
- Baud rate
- Target weight (kg)
- Reset weight (kg)
- Weight prefix (`wn` by default)
- Unit (`kg` by default)
- Stable read count

Default configuration uses `COM12`, `9600` baud, prefix `wn` and unit `kg`.

## Important source files

- `Services/WeighbridgeService.cs` - serial weight reader/parser.
- `Services/LaneProcessor.cs` - one-weighbridge arm/first-RFID-wins/reset logic.
- `Services/ApplicationCoordinatorService.cs` - routes weight and RFID events to the processor.
- `Services/CameraStreamService.cs` - four live RTSP camera streams.
- `Services/CameraSnapshotService.cs` - four-image transaction capture.
- `Services/ServerSyncWorker.cs` - image upload + MQTT transaction push.
- `MainWindow.xaml` - iAWS dashboard.
- `HardwareSettingsWindow.xaml` - COM and target/reset weight settings.
- `ServerPanelWindow.xaml` - device/weight/camera/server configuration.
- `appsettings.json` - default v0.1 configuration.

## Build / run

Requirements on the Windows development PC:

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 is optional

PowerShell:

```powershell
./run-app.ps1
```

Or open:

```text
RealTech_iAWS_v0.1.sln
```

### Build standalone Windows EXE

Double-click:

```text
build-standalone-exe.bat
```

or run:

```powershell
./publish-win-x64.ps1
```

Expected output:

```text
Publish\StandaloneWinX64\RealTechiAWS.exe
```

## Version

- Product: **RealTech iAWS**
- Description: **Automatic Weighing System**
- Version: **v0.1**
- Assembly/File version: `0.1.0.0`
