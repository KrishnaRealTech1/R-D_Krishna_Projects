# Changes in v0.1.33

## Windows taskbar icon

- Reworked the RealTech taskbar icon so the RTS mark remains visible at small Windows taskbar sizes.
- Added native `WM_SETICON` handling for both small and large window icons.
- Added native window-class icon handling as a fallback for self-contained single-file WPF publishing.
- Reapplies the native icon during source initialization, load, content rendering, and the first idle UI cycle so Windows does not retain the generic WPF icon.
- Keeps the managed WPF `Window.Icon` and explicit AppUserModelID behavior.

## IN and OUT buzzer indicators

- Added a graphical buzzer symbol directly below each IN and OUT traffic signal.
- The relevant buzzer indicator illuminates whenever an `IN Buzzer` or `OUT Buzzer` command is sent.
- The visual pulse remains active for 1.5 seconds and restarts when another buzzer command is received.
- `IN ALL OFF` and `OUT ALL OFF` immediately clear the corresponding buzzer display.
- The indicator works for automatic lane processing and commands sent from Admin Controls.

## Version

- Updated application version to `0.1.33`.
