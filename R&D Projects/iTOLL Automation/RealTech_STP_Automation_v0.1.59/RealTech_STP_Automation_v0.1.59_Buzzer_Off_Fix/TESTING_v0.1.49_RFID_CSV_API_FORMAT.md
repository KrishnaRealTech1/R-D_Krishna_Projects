# Testing v0.1.49 RFID CSV/API format

1. Import `sample-data/rfid_api_exact_format_sample.csv`.
2. Confirm all rows are inserted or updated without missing-column errors.
3. Confirm `capacity` appears as the vehicle category/capacity.
4. Confirm `PAID` and `FREE` map to the corresponding access types.
5. Confirm `ACTIVE` vehicles are enabled and `INACTIVE` vehicles are disabled.
6. Configure payment prices for categories `10000`, `6000` and `10500`; process a Paid IN and verify the expected debit.
7. Confirm MQTT schema `1.2` publishes the imported capacity in `vehicleCategory`.
8. Rename a valid `.xlsx` workbook containing the same headers to `.csv` and import it manually; confirm content detection selects the Excel reader.
9. Run Vehicle CSV API Sync and verify a direct plain-text CSV response with the exact headers imports successfully.
