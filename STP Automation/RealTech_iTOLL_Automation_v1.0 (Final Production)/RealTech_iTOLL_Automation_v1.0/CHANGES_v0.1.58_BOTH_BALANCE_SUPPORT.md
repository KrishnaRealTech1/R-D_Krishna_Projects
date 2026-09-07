# v0.1.58 - Concurrent RFID + Contractor Balance Support

- Added `BOTH` to Server Panel **Balance Support Mode**.
- `BOTH` is now the default mode.
- Contractor-linked vehicles use the shared Contractor/User balance.
- Legacy vehicles without a contractor assignment continue to use their existing RFID/vehicle balance.
- `RFID` remains available to force all vehicles through legacy RFID/vehicle balance.
- `CONTRACTOR` remains available to force contractor balance and reject vehicles without a valid active contractor cache record.
- A vehicle that is explicitly linked to a contractor will not silently fall back to RFID balance when that contractor record is missing/inactive. This avoids bypassing contractor-account controls.
- Online authorization payload now reports the actual selected balance source (`RFID` or `CONTRACTOR`) when the panel is configured as `BOTH`.
- Existing one-negative-trip offline guard remains atomic for both RFID and Contractor balances.

## BOTH selection rule

```text
Vehicle has ContractorCode -> CONTRACTOR balance
Vehicle has no ContractorCode -> RFID/Vehicle balance
```

This does **not** debit both accounts for one trip. It allows both balance systems to operate side-by-side in the same installation.
