# RealTech iTOLL Automation v0.1.50 Changes

## Cross-lane same-RFID collision guard

Fixed a race where the same RFID could be matched by both the IN and OUT lane at nearly the same time and both lane processes could continue independently.

### New behavior

- The RFID is atomically claimed across the whole application immediately after a lane has a valid sensor + RFID match.
- Only one lane can own/process a given RFID at a time.
- If IN and OUT attempt to start the same RFID concurrently, the first successful claim continues and the other lane ignores that sensor cycle.
- Any buffered copy of the same RFID on the opposite lane is discarded when the winning lane starts processing.
- A second active-process check after lane buffering closes the race where the opposite lane claims the RFID while this lane is buffering it.
- The global RFID claim remains held through vehicle lookup, debit/trip processing, camera/hardware flow and lane completion, then is released in `finally`.
- Different RFID values can still process on IN and OUT concurrently.
- Existing per-lane `DuplicateReadSeconds` behavior is unchanged.

### Safety impact

For a PAID vehicle, simultaneous IN/OUT antenna detection can no longer create two independent processing flows or two debits for the same RFID at the same time.

### Version

Application, assembly and file version updated to `0.1.50`.
