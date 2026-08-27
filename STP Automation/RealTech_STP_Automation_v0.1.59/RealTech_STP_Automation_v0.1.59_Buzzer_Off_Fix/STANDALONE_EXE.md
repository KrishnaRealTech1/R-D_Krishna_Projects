# RealTech STP Automation — Standalone EXE

## Result

The project is configured to publish one Windows x64 executable:

```text
Publish\StandaloneWinX64\RealTechSTPAutomation.exe
```

The target computer does not need the .NET runtime installed because the executable is published as self-contained.

## Build with one double-click

1. Install the .NET 8 SDK on the Windows build computer.
2. Extract the project ZIP.
3. Double-click `build-standalone-exe.bat`.
4. Wait for package restore and publishing to finish.
5. Copy `Publish\StandaloneWinX64\RealTechSTPAutomation.exe` to the target computer.

The build script validates that the publish folder contains only the EXE. If any required external file remains, the script stops and lists it instead of reporting a false single-file build.

## Visual Studio publishing

A Visual Studio publish profile is included:

```text
src\RfidVehicleAccess.App\Properties\PublishProfiles\StandaloneWinX64.pubxml
```

In Visual Studio 2022:

1. Right-click `RfidVehicleAccess.App`.
2. Select **Publish**.
3. Select **StandaloneWinX64**.
4. Select **Publish**.

## Windows taskbar logo

The standalone EXE contains a multi-resolution RealTech icon and the WPF windows also load a dedicated taskbar image. If Windows still shows an older cached icon after replacing a previous build, unpin the old taskbar shortcut, start the new `RealTechSTPAutomation.exe`, and pin the running application again.

## First launch storage

The EXE contains the default `appsettings.json`. On first launch, the application creates an editable copy and all writable data under:

```text
Documents\Realtech_systems\Config\appsettings.json
Documents\Realtech_systems\Data\vehicle-access.db
Documents\Realtech_systems\Images\
Documents\Realtech_systems\Logs\
```

This prevents configuration and database writes from failing when the EXE is launched from a protected folder such as `Program Files`.

## Native VLC camera files

The application uses LibVLC for the two RTSP camera streams. The publish configuration bundles the native VLC libraries and plugin content into the EXE. At runtime, .NET can extract those bundled native files into the current user’s temporary single-file extraction directory. This is normal; distribution still requires only `RealTechSTPAutomation.exe`.

A runtime locator is included so LibVLC loads its native DLLs and plugin folder from the extracted bundle instead of assuming that loose VLC files exist beside the EXE.

## Publish settings

The project and publish profile use:

```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
<PublishTrimmed>false</PublishTrimmed>
```

Trimming remains disabled because WPF, ClosedXML, dependency injection and native camera libraries can use reflection or runtime discovery.
