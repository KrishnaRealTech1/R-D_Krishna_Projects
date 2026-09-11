# v0.1.59 - Version metadata correction

The v0.1.59 source already contained the Buzzer OFF fix, but the WPF application project metadata still reported v0.1.57.

Updated in `src/RfidVehicleAccess.App/RfidVehicleAccess.App.csproj`:

- `Version`: `0.1.59`
- `AssemblyVersion`: `0.1.59.0`
- `FileVersion`: `0.1.59.0`

The About menu uses `ApplicationVersionService.GetDisplayVersion()`, which reads the assembly informational/version metadata, so after rebuilding the application it will display:

`RealTech iTOLL Automation v0.1.59`

Important: rebuild/publish the executable after this change. An older previously-built EXE will continue to show its old embedded version.
