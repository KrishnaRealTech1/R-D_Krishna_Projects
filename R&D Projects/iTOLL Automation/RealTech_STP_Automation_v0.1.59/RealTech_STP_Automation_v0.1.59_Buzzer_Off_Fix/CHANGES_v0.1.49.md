# RealTech STP Automation v0.1.49

## Exact RFID CSV/API format

The vehicle master importer now explicitly supports this format:

```csv
sno,username,vehicle_no,rf_id,empty_weight,capacity,balance,rfid_type,rf_status
4482,tambaram_sullage,TN 07 AR 3523,E2801191A50400742358EC3C,10000,0,0,FREE,ACTIVE
```

Field mapping:

- `sno`: accepted and ignored as an informational row number.
- `username`: source site.
- `vehicle_no`: vehicle number.
- `rf_id`: RFID/EPC.
- `empty_weight`: empty/tare weight.
- `capacity`: vehicle category/capacity for payment matching and MQTT `vehicleCategory`.
- `balance`: wallet opening/current balance according to normal insert/update preservation rules.
- `rfid_type`: `FREE` or `PAID`.
- `rf_status`: `ACTIVE` or `INACTIVE`.

Manual import also detects XLSX/XLSM ZIP content even when the file was accidentally renamed with a `.csv` extension.
