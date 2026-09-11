# RealTech STP Automation v0.1.46 Changes

## Payment Configuration authentication

- Removed the visible `7799` password hint from the Payment Configuration authentication dialog.
- The dialog now only asks for the configured payment-configuration password.
- Password validation behavior remains unchanged.

## Vehicle CSV API import

- Added **Data > Sync Registered Vehicles from API...**.
- Added configurable API fields under **Server Panel > Storage & Import > Vehicle Import**:
  - Vehicle CSV API Endpoint
  - Vehicle CSV API Username
  - Vehicle CSV API Timeout Seconds
- Default request:

```http
POST https://gov.igps.io/aws_admin/Server/csv_link.php
Content-Type: application/json
```

```json
{"username":"tambaram"}
```

- Supports these API response styles:
  - CSV content returned directly.
  - Plain HTTP/HTTPS CSV URL.
  - JSON containing `csv_link`, `csv_url`, `file_url`, `download_url`, `download_link`, `url`, `link`, `file` or `path`.
  - Relative CSV URLs returned by the configured endpoint.
- Downloads are limited to 50 MB.
- The downloaded file must contain an RFID column and a vehicle-number column before import begins.
- Temporary API CSV files are deleted after success or failure.
- Existing file-import behavior, RFID normalization, duplicate handling, category/capacity import and update rules are reused.
- Updated application version to `0.1.46`.
