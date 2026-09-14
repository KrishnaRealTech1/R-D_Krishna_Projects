# RealTech iAWS v0.1 - Standalone Windows EXE

Run `build-standalone-exe.bat` on a Windows x64 PC with the .NET 8 SDK installed.

The publish script targets:

```text
Publish\StandaloneWinX64\RealTechiAWS.exe
```

The project is configured as self-contained, win-x64 and single-file. Native LibVLC content is bundled/extracted by .NET at runtime for the four RTSP camera streams.

If Windows shows a cached old application icon, unpin the previous shortcut, launch the new `RealTechiAWS.exe`, and pin the running application again.
