# Changes v0.1.52 - IND Bluetooth Failover / Terminal

## Added
- Added Bluetooth SPP virtual-COM backup settings for the IND control unit.
- Wired CONTROL remains the primary IND transport.
- If the wired control port cannot open or a wired write fails, the application automatically activates the configured Bluetooth COM port and retries through the backup link.
- Background reconnect probes the wired control link and returns to wired after a short command-idle window.
- Added an embedded **IND Control Terminal (Wired / Bluetooth Failover)** to the bottom of the main panel.
- Terminal displays live link, TX, RX, warning and error traffic.
- Manual terminal commands use the same single active IND transport as automation, preventing duplicate simultaneous automatic commands.
- Added Bluetooth Backup COM port and baud-rate controls under **Connectivity > COM Port Settings**.

## Safety / behavior
- RFID IN and OUT ports remain separate and unchanged.
- Only the active IND control path can publish sensor transitions into the automation flow.
- Bluetooth is used as failover; wired and Bluetooth are not used to transmit the same automatic command at the same time.
- Bluetooth support expects a Windows-paired Bluetooth Serial Port Profile (SPP) device that exposes a virtual COM port.

## Default configuration
- Wired IND CONTROL: COM10 @ 9600 baud.
- Bluetooth backup: enabled, COM11 @ 9600 baud.
- Change COM11 to the actual Windows Bluetooth outgoing/serial COM port for the paired IND Bluetooth adapter.
