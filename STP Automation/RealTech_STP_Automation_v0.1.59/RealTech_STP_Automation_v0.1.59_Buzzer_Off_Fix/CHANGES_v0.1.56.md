# RealTech STP Automation v0.1.56

## Wi-Fi serial terminal failover

- Added third-level IND control failover over raw TCP/Wi-Fi serial terminal.
- Failover priority: Wired COM -> Bluetooth COM -> Wi-Fi TCP.
- Default Wi-Fi terminal: `192.168.22.102:23`.
- Added Hardware Settings controls to enable Wi-Fi backup and edit host/IP + TCP port.
- Wi-Fi receives the same line-based control/sensor messages and sends the same IND commands with the configured line terminator.
- Reconnect loop prefers restoring Wired first, then Bluetooth, then Wi-Fi.

## Validation

- appsettings.json parsed successfully.
- HardwareSettingsWindow.xaml parsed successfully as XML.
- C# brace-balance checks passed.
- Full `dotnet build` could not be run in this environment because the .NET SDK is not installed.
