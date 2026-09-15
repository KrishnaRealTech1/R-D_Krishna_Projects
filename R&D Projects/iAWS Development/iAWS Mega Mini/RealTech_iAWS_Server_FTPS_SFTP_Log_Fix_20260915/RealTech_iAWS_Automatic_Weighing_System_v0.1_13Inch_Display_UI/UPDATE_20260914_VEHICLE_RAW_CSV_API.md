# iAWS vehicle API correction - 2026-09-14

## Vehicle details import API

The registered-vehicle import uses this endpoint only:

`https://gov.igps.io/aws_madurai/api/csv_link.php`

The endpoint is requested directly with HTTP GET and the response is consumed as raw CSV (`text/csv`). No JSON request body is sent for vehicle import.

Expected/used vehicle columns are:

- `username`
- `vehicle_no` (or supported vehicle-number aliases)
- `rf_id` (or supported RFID aliases)
- `empty_weight`

Other payment/access/balance/category fields are ignored by API-minimal import.

Legacy iAWS configurations using the old 404 `aws_admin/Server/csv_link.php` endpoint are migrated in memory to the Madurai endpoint. Legacy auto-generated six-source lists (`Primary Vehicle CSV API`, `Vehicle CSV API 2`, etc.) are collapsed to one confirmed vehicle API source so one sync performs one vehicle API request.

## Weighing transaction upload API

The transaction upload API remains separate and unchanged:

`http://dev.igps.io/aws_raw_data/iAWS_universalAPi.php`

This endpoint is for uploading completed weighing transaction data and is not used to download registered vehicles.
