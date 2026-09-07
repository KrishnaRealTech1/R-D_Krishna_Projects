# Changes in v0.1.27

- Added Windows x64 self-contained single-file publishing.
- Added native-library and content extraction settings required by SQLite and LibVLC/VLC plugins.
- Added a single-file-aware LibVLC runtime locator so bundled camera libraries and plugins load from the .NET extraction directory.
- Embedded the default `appsettings.json` inside the executable.
- Added first-run configuration extraction to `Documents\Realtech_systems\Config`.
- Moved the default SQLite database to `Documents\Realtech_systems\Data`.
- Added migration support for legacy configuration, database, image and log files.
- Added a Visual Studio `StandaloneWinX64` publish profile.
- Added `build-standalone-exe.bat` and a strict PowerShell publisher that verifies the output contains one EXE.
