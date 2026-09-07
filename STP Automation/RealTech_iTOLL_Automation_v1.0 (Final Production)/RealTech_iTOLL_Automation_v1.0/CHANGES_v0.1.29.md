# Changes in v0.1.29

## Admin exceptional approval controls

- Added separate **Enable IN Exceptional Approval** and **Enable OUT Exceptional Approval** options to the password-protected Admin Controls window.
- Added a **Save Settings** action that applies the changes immediately and persists them to the runtime `appsettings.json`.
- Added new `ExceptionalApproval.InEnabled` and `ExceptionalApproval.OutEnabled` configuration values. Both default to `true` to preserve the existing v0.1.28 behavior.
- Added backward-compatible configuration loading for installations whose existing configuration does not yet contain the new section.
- When IN exceptional approval is disabled, a repeated IN with a missing OUT trip is rejected without opening the approval popup or creating reconciliation records.
- When OUT exceptional approval is disabled, an OUT with a missing IN trip is rejected without opening the approval popup or creating reconciliation records.
- Added status-log entries and clear lane rejection messages when an approval path is disabled by Admin.
- Unsaved checkbox changes are discarded when the Admin Controls window is reopened, and failed saves restore the previous live settings.
