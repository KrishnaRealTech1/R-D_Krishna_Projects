# v0.1.37 Implementation Summary

## Existing behavior observed

- Local-first IN/OUT gate processing.
- Paid IN balance deduction and trip insertion are stored in SQLite.
- Pending trips are uploaded asynchronously: image by SFTP and transaction JSON by MQTT.
- MQTT transaction synchronization is publish-only and considers QoS 1 PUBACK successful delivery.

## Recharge synchronization added

- Persistent MQTT 3.1.1 subscription for server-originated recharge commands.
- Device-specific command and acknowledgement topics.
- Positive delta recharge applied to the latest local Paid RFID balance.
- Unique `rechargeId` protection against duplicate credit.
- `rfid_recharges` audit table with server/local balances, metadata, result, timestamps, and raw JSON.
- Applied, Duplicate, and Rejected acknowledgement JSON.
- Serialized balance mutations so a recharge received during IN processing is not overwritten by a stale debit calculation.
- Serialized MQTT publishing to prevent simultaneous connections using the same publish client ID.
- Server Panel fields for recharge settings.

## Verification performed in this environment

- `appsettings.json` JSON validation passed.
- Project file XML validation passed.
- SQLite initializer schema executed successfully in an in-memory database.
- All seven JSON samples in `SERVER_INTERFACE.md` parsed successfully.
- Recharge-plus-entry-debit SQL behavior was simulated; expected final balance was produced.
- C# delimiter/static structure scan passed for all source files.
- The DOCX interface specification was rendered to seven pages and visually reviewed.

## Build verification still required

The execution container does not include the .NET SDK, MSBuild, or a C# compiler. Open
`RfidVehicleAccess.sln` in Visual Studio 2022 or run `dotnet build -c Release` on a machine with the
.NET 8 Windows SDK to complete compiler and WPF runtime verification.
