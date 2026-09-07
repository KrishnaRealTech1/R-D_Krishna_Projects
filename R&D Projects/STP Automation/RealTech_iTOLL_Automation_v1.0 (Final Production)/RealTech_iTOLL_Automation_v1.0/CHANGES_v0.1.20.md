# Changes in v0.1.20

- Fixed IN and OUT approved-lane completion after the vehicle sensor changes to LOW.
- The configured `Processing.BarrierAndGreenDelaySeconds` timer now starts at sensor
  LOW while ORG, buzzer, and the open barrier remain active.
- When the timer expires, the lane sends barrier CLOSE, ALL OFF, and GRN commands so
  orange is explicitly cleared and green is enabled.
- Improved sensor-message normalization to tolerate punctuation, tabs, and repeated
  separators in control-unit messages.
- Added password authentication before the Admin Controls window opens.
- Added `Security.AdminControlsPassword` to `appsettings.json`, defaulting to `7799`.
