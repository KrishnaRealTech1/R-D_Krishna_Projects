# v0.1.53 - Silent Bluetooth Failover UI cleanup

- Removed the embedded IND Control Terminal from the main dashboard.
- Removed live IND TX/RX terminal log, manual command entry, Send and Clear controls.
- Restored the bottom dashboard area to Status Log + Server Log only.
- Kept wired CONTROL COM as the primary IND connection.
- Kept automatic Bluetooth SPP COM failover and wired reconnect/failback logic in the background.
- Bluetooth backup COM configuration remains available in hardware/connectivity settings.
