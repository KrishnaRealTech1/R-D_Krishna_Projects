# v0.1.28 exceptional approval test plan

## Build and About version

1. Build and run the application.
2. Open **About**.
3. Confirm the disabled menu row displays `RealTech iTOLL Automation v0.1.28`.
4. Change the project version for a test build and confirm About follows the assembly version without changing XAML.

## Missing OUT trip at the IN lane

1. Enable Simulation mode and import a registered active vehicle.
2. Complete one IN transaction but do not complete its OUT transaction.
3. Start another IN sensor/RFID cycle for the same vehicle.
4. Confirm the Exceptional Approval popup opens automatically and shows `MISSING OUT TRIP`.
5. Confirm submitting with blank Name, Role or Mobile Number is blocked.
6. Confirm a mobile number outside 7-15 digits is blocked.
7. Enter valid approval details and click **Exceptional Approval**.
8. Confirm the lane shows `EXCEPTIONAL IN APPROVED - MISSING OUT RECONCILED`.
9. Confirm the current IN barrier sequence runs normally.
10. Confirm SQLite contains a reconciliation OUT linked to the old IN and a new current IN.
11. Confirm both records contain approval details in `notes`, dedicated exceptional-approval columns and are `PendingSync`.
12. Confirm both MQTT payloads contain `tripDate`, `tripTime`, `tripTimestamp`, `tripTimestampUtc`, `timeZoneOffset` and the structured `exceptionalApproval` object.
13. Complete an OUT for the current trip and confirm no stale active IN remains.

## Missing IN trip at the OUT lane

1. Use a registered active vehicle with no active IN trip.
2. Start an OUT sensor/RFID cycle.
3. Confirm the Exceptional Approval popup opens automatically and shows `MISSING IN TRIP`.
4. Enter valid approval details and click **Exceptional Approval**.
5. Confirm the lane shows `EXCEPTIONAL OUT APPROVED - MISSING IN RECONCILED`.
6. Confirm the OUT barrier sequence runs normally.
7. Confirm SQLite contains a zero-debit reconciliation IN and an OUT linked to that IN.
8. Confirm both records contain approval details in `notes`, dedicated exceptional-approval columns and are `PendingSync`.
9. Confirm both MQTT payloads contain the trip date/time fields and the same original exceptional approval timestamp.
10. Confirm no active IN remains after the paired records are saved.

## Cancellation

1. Trigger either missing-trip case.
2. Close the popup or click **Cancel**.
3. Confirm the lane is rejected, the barrier remains closed and the lane returns to green/idle.
4. Confirm no reconciliation or current trip record is added.

## Normal IN and OUT server timestamps

1. Complete one normal IN transaction and its matching normal OUT transaction.
2. Inspect both MQTT JSON payloads.
3. Confirm each payload contains `tripDate` in `yyyy-MM-dd` format.
4. Confirm each payload contains `tripTime` in `HH:mm:ss.fff` format.
5. Confirm `tripTimestamp` includes the local UTC offset.
6. Confirm `tripTimestampUtc` represents the same instant in UTC.
7. Confirm `processedAt` is still present for backward compatibility.
8. Confirm `exceptionalApproval` is omitted/null for normal trips.

## Existing database migration

1. Start with a database created by the previous v0.1.28 build.
2. Run the updated application.
3. Confirm startup succeeds and the existing vehicle/trip data remains available.
4. Confirm the five exceptional approval columns are added to `trips` only once.
5. Restart the application and confirm no duplicate-column migration error occurs.
