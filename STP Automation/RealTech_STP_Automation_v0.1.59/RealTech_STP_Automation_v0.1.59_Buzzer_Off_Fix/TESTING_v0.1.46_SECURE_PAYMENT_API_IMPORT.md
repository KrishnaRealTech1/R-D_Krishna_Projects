# v0.1.46 Secure Payment Prompt and Vehicle API Import Test Checklist

## Payment authentication

1. Open **Payment Configuration**.
2. Confirm the dialog does not display `7799` or any other password value.
3. Confirm the password field masks entered characters.
4. Confirm an incorrect password is rejected.
5. Confirm the configured password unlocks Payment Configuration.

## API configuration

1. Open **Server Panel > Storage & Import**.
2. Confirm Vehicle Import contains API Endpoint, API Username and API Timeout fields.
3. Confirm the default endpoint is `https://gov.igps.io/aws_admin/Server/csv_link.php`.
4. Confirm the default username is `tambaram`.
5. Confirm timeout values outside 1 to 300 seconds are rejected.
6. Confirm an invalid API URL is rejected by **Save & Apply**.

## API import

1. Select **Data > Sync Registered Vehicles from API...**.
2. Confirm the application asks before downloading and importing.
3. Verify the request method is POST.
4. Verify `Content-Type` is `application/json`.
5. Verify the request body is `{"username":"tambaram"}` using the configured username.
6. Test an API response containing a direct CSV file.
7. Test a plain CSV URL response.
8. Test JSON responses using `csv_link`, `csv_url`, `download_url`, `url` and `link`.
9. Test a relative CSV URL.
10. Confirm a non-success HTTP response shows the HTTP status and a short server-response preview.
11. Confirm a file without RFID and vehicle-number headers is rejected before database import.
12. Confirm matching RFID records update when `Import.UpdateExistingRecords` is enabled.
13. Confirm matching RFID records are skipped when `Import.UpdateExistingRecords` is disabled.
14. Confirm vehicle category/capacity values are imported with the existing rules.
15. Confirm the result dialog reports inserted, updated, skipped and failed rows.
16. Confirm temporary CSV files are removed from `%TEMP%\RealTechSTPAutomation\VehicleApiImports` after processing.
17. Confirm a timeout or cancelled request does not leave a temporary file.
