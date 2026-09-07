# v0.1.54 - Silent Bluetooth Failover compile fix

- Fixed CS0103 in `MainViewModel.cs`.
- Removed stale `_hardware.ControlTraffic -= OnControlTraffic;` cleanup reference left after the terminal UI was removed.
- Silent wired COM -> Bluetooth failover remains unchanged.
- No IND terminal pane is shown in the main UI.
