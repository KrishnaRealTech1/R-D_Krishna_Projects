# Changes in v0.1.30

## IN/OUT auto approval mode

- Added independent **Auto Approve IN Missing OUT** and **Auto Approve OUT Missing IN** settings in the password-protected Admin Controls window.
- When an auto approval option is enabled, the lane skips the exceptional approval popup, creates the same reconciliation records used by manual exceptional approval, and continues the current trip.
- Auto approval takes priority over the corresponding exceptional approval popup setting.
- When auto approval is disabled and exceptional approval is enabled, the existing approval popup is shown.
- When both modes are disabled for a lane, the missing-trip movement is rejected.
- Auto-approved records are marked with `SYSTEM` / `IN AUTO APPROVAL` or `OUT AUTO APPROVAL` audit details and are included in the existing exceptional approval sync payload.
- Added backward-compatible `AutoApproval.InEnabled` and `AutoApproval.OutEnabled` configuration values. Both default to `false`, preserving v0.1.29 behavior after upgrade.

## Taskbar icon

- Changed all WPF windows and the runtime branding service to use the embedded multi-resolution `.ico` file instead of a PNG.
- Reapplies the icon when the native window source is initialized to prevent the generic WPF icon from remaining on single-file builds.
- Updated application version to `0.1.30`.
