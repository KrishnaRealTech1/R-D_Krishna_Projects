# Changes in v0.1.10

## Binary UHF RFID packet support

- Added a stream-safe decoder for the UHF reader packets observed on COM3.
- Reassembles packets when one serial read contains a partial frame or several frames.
- Extracts the EPC using the packet EPC-length byte.
- The sample packet now produces `E280691500004020ED9648B3`.
- Keeps the existing line-text mode for RFID readers that send CR/LF-delimited text.
- Suppresses identical high-frequency inventory reports for 750 ms to prevent task and status-log flooding.
- Migrates v0.1.9 configurations that do not yet contain `ReadMode`.
- Configured the supplied site ports as IN `COM3` at 115200, OUT `COM6` at 115200, and CONTROL `COM10` at 9600.
- Upgraded the application version to 0.1.10.

## Important

The UHF reader software and PowerShell test must be closed before the vehicle-access application opens the same COM port. Windows permits only one process to own a serial port at a time.
