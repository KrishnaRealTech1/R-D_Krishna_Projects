# iAWS Update - Barrier Arm, Parallel Signal Commands, Minimal Vehicle API

## 1. Boom barrier arm visual
- IN and OUT boom arms now use an odd number of alternating segments.
- The arm starts red and ends red, so the far end no longer looks cut off by a white segment.
- No barrier sizing, animation angle, open/close state, or layout logic was changed.

## 2. Common RED / ORG / GRN commands
Whenever a lane-specific signal command is sent, the common signal command is sent immediately after it in the same locked control-write cycle:

- `IN RED` / `OUT RED` / automatic `IN RED <vehicle>` / `OUT RED <vehicle>` -> also sends `RED`
- `IN ORG` / `OUT ORG` -> also sends `ORG`
- `IN GRN` / `OUT GRN` -> also sends `GRN`

This applies to automatic operation, Admin Controls, simulation, wired COM, Bluetooth failover, and Wi-Fi failover because the behavior is implemented centrally in `HardwareGateway`.

## 3. Registered vehicle API minimal format
Vehicle API synchronization now consumes only the fields supplied by the iAWS vehicle API:

- `username`
- `vehicle_no` / `vehicle_number` / supported compact aliases
- `rf_id`
- `empty_weight`
- `sno` may be present and is ignored

Paid/Free access type, balance, category, contractor and unrelated fields are not consumed from API data. Existing local values are preserved. A new API-only vehicle row uses neutral `Both` internally because payment/access type is not part of the API contract and does not control the iAWS weighing flow.

The normal manual CSV/XLSX vehicle import remains unchanged and can still use its existing additional fields.
