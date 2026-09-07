# Testing v0.1.52 - IND Bluetooth Failover

1. Pair the IND Bluetooth serial adapter in Windows and note its SPP/serial COM port.
2. Open **Connectivity > COM Port Settings**.
3. Keep the wired Control Unit port set to the normal IND port (for example COM10).
4. Enable **Bluetooth Backup** and select the paired Bluetooth COM port. Save & Connect.
5. With wired control healthy, confirm the terminal shows `Active: Wired COMx` and normal barrier/signal commands still work.
6. Disconnect/disable the wired control COM port. On the next IND command, confirm the terminal reports the wired error, opens Bluetooth, and reports `Active: Bluetooth COMx`.
7. Confirm barrier/signal commands and `IN/OUT Detected/Realeased` sensor messages continue over Bluetooth.
8. Reconnect the wired control port. After the configured reconnect cycle and a short command-idle window, confirm the active transport returns to wired.
9. Enter a harmless IND test command in the embedded terminal and press **Send**. Confirm exactly one TX line is shown and no duplicate command is transmitted.
10. Confirm IN RFID and OUT RFID readers continue to operate throughout control-link failover.

Note: source-level validation was performed in the delivery environment, but a Windows WPF compile could not be run there because the .NET SDK/MSBuild is not installed. Build on the target Windows development machine before deployment.
