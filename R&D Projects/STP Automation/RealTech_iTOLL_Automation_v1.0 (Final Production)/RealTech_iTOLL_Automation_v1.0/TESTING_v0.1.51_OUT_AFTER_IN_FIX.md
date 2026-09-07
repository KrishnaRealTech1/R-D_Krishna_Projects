# Testing v0.1.51 - OUT-after-IN RFID handoff

## Test 1 - Same RFID detected on IN and OUT simultaneously

1. Make both lane sensors HIGH.
2. Present the same registered RFID to both readers at nearly the same time.

Expected:
- Only one lane obtains the cross-lane RFID claim.
- Only that lane creates the transaction / debit / barrier flow.
- The losing lane logs that the same RFID is already processing.
- No duplicate simultaneous trip is created.

## Test 2 - Normal IN then immediate OUT

1. Process a registered RFID normally on IN.
2. Wait until the controller sends `IN Realeased`.
3. While the IN post-release barrier completion delay is still running, make OUT sensor HIGH and present the same RFID to the OUT reader.

Expected:
- The log contains `IN cross-lane RFID claim released on physical lane release`.
- OUT detects the RFID immediately.
- OUT lookup finds the active IN trip and creates the matching OUT trip.
- OUT does not wait for the IN post-release barrier delay to finish.

## Test 3 - Reverse direction handoff

1. Process an RFID on OUT.
2. Send `OUT Realeased`.
3. Present the same RFID on IN while OUT is still finishing its post-release delay.

Expected:
- OUT releases its cross-lane claim at `OUT Realeased`.
- IN can detect and process the RFID.

## Test 4 - Release/claim race

Trigger the sensor `Realeased` event at nearly the same instant the RFID/sensor pair starts processing.

Expected:
- No stale global claim remains.
- The opposite lane can subsequently acquire the RFID.

## Test 5 - Owner-safe cleanup

1. Complete IN far enough to receive `IN Realeased`.
2. Immediately start OUT with the same RFID.
3. Allow the older IN async process to finish after OUT has already started.

Expected:
- IN final cleanup does not remove OUT's newer claim.
- A third concurrent attempt for the same RFID remains blocked while OUT owns it.
