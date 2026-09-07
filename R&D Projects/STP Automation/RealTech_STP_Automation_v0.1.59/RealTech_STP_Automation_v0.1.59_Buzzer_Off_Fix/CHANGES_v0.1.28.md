# Changes in v0.1.28

- Added a dedicated RealTech taskbar icon generated from the supplied company logo.
- Added taskbar-optimized 16, 20, 24, 32, 40 and 48 pixel icon frames for clear Windows display.
- Retained the complete STP Automation logo for larger 64, 96, 128 and 256 pixel Explorer icon views.
- Applied the branded icon to the main window, admin windows and hardware settings window.
- Added a centralized Windows branding service that assigns the icon to all WPF windows.
- Added an explicit `RealTech.STP.Automation` Windows AppUserModelID for stable taskbar grouping.
- Explicitly enabled the native app host so the standalone EXE contains the application icon.

## Exceptional approval update

- Fixed the About menu so it reads the executable assembly version dynamically instead of displaying the hard-coded `v0.1.26` value.
- Added an Exceptional Approval popup that opens automatically when an IN or OUT trip is missing.
- Added required approver Name, Role and Mobile Number fields with validation.
- Added an `Exceptional Approval` submit button that records the approval and continues the lane process.
- Added Missing OUT reconciliation: a synthetic zero-debit OUT record closes the previous unmatched IN before the current IN is saved.
- Added Missing IN reconciliation: a synthetic zero-debit IN record is created and linked to the current OUT.
- Saved the missing-trip reason, approver details and approval timestamp in transaction notes for server synchronization.
- Added atomic SQLite repository operations so reconciliation and current-trip records cannot be partially committed.
- Cancelling or closing the popup rejects the current lane movement and keeps the barrier closed.

## Server date/time stamp update

- Added `tripDate`, `tripTime`, `tripTimestamp`, `tripTimestampUtc` and `timeZoneOffset` to every IN and OUT MQTT transaction payload.
- Retained the existing `processedAt` field for backward compatibility.
- Added a structured `exceptionalApproval` payload containing reason, approver details, approval date, approval time, offset-aware approval timestamp and UTC approval timestamp.
- Persisted exceptional approval fields in dedicated SQLite columns so queued transactions keep the original approval timestamp across retries and application restarts.
- Added automatic database migration for existing installations without deleting or recreating their current database.
- Added a legacy-note fallback so exceptional trips already queued by the previous build still send structured approval timestamps.
- Updated the MQTT payload schema version from `1.0` to `1.1`.
