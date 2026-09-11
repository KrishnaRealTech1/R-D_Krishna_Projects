# Testing v0.1.50 - Cross-lane same-RFID guard

## Test 1 - Same RFID, both lane sensors HIGH

1. Register the same valid RFID in the vehicle database.
2. Make both IN and OUT vehicle sensors HIGH.
3. Deliver the same RFID to both readers as close together as possible.

Expected:
- Exactly one lane obtains the RFID processing claim.
- Only the winning lane performs vehicle lookup/payment/trip/barrier processing.
- The losing lane logs `simultaneous cross-lane RFID ignored`.
- Only one transaction/debit is created for that processing cycle.

## Test 2 - Opposite lane buffered first

1. Send the RFID to OUT while OUT sensor is not yet HIGH so it is buffered.
2. Match the same RFID with the IN sensor and let IN start processing.
3. Raise the OUT sensor afterward within the RFID pairing window.

Expected:
- IN continues.
- The buffered OUT copy is discarded.
- OUT does not later start a stale process for that RFID.

## Test 3 - Buffer/claim race

Send the same RFID repeatedly to both readers while both sensors are active.

Expected:
- The pre-buffer check, post-buffer check and atomic processing claim prevent dual processing regardless of callback ordering.

## Test 4 - Different vehicles at the same time

Use RFID-A on IN and RFID-B on OUT simultaneously.

Expected:
- Both lanes are allowed to process concurrently because the guard is per RFID, not a global lane lock.

## Test 5 - RFID can be used again after completion

After the winning process fully completes and a new physical sensor cycle starts, present the RFID again normally.

Expected:
- The active cross-lane claim has been released.
- Normal lane and duplicate-read rules apply.
