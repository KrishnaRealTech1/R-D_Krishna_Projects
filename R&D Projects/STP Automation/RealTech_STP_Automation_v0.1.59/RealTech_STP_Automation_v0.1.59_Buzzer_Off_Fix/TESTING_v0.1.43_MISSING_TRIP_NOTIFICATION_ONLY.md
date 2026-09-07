# Testing v0.1.43 - Missing Trip Notification Only

## T01 - Missing IN approved at OUT reader

1. Ensure the RFID has no active IN trip.
2. Scan it at the OUT reader and approve manually or automatically.
3. Confirm one current OUT row is created.
4. Confirm the OUT image is captured and uploaded.
5. Confirm one MQTT message is published.
6. Confirm JSON contains `missingTripNotification.missingDirection = "IN"`.
7. Confirm `syntheticTripCreated = false` and `countedAsTransaction = false`.
8. Confirm `entryTransactionId` is omitted.
9. Confirm Pending increases by one only before sync and Processed increases by one only after sync.

## T02 - Missing OUT approved at IN reader

1. Create an IN trip and leave it unmatched.
2. Scan the same RFID again at the IN reader and approve.
3. Confirm only the new physical IN is created as Pending.
4. Confirm the old IN is marked internally as exceptionally closed.
5. Confirm no synthetic OUT row is created.
6. Confirm one image and one MQTT message for the new IN.
7. Confirm JSON contains `missingTripNotification.missingDirection = "OUT"`.
8. Confirm Pending/Processed increase by one only.

## T03 - Legacy database migration

1. Start with a database containing `NotCaptured-ExceptionalReconciliation` rows.
2. Start v0.1.43.
3. Confirm those rows change to internal `ReconciliationRequired` status.
4. Confirm they are not returned by pending sync queries.
5. Confirm they are excluded from Pending and Processed dashboard counts.

## T04 - Normal IN/OUT regression

1. Process a normal IN then matching OUT.
2. Confirm the OUT contains the real IN `entryTransactionId`.
3. Confirm neither payload contains `missingTripNotification` or `exceptionalApproval`.
4. Confirm one count per physical trip.
