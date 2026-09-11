# RealTech STP Automation v0.1.48 Testing

## 1. Multiple API configuration

1. Open **Server Panel > Storage & Import**.
2. Add two API rows and configure endpoint, username, priority and timeout.
3. Save and reopen the panel.
4. Confirm both rows are retained.
5. Disable one source and confirm manual/API auto sync only calls the enabled source.

## 2. Automatic synchronization

1. Enable Vehicle API Auto Sync.
2. Set the interval to 30 seconds.
3. Monitor the Status Log for repeated completed cycles.
4. Change the interval and save; confirm later cycles use the new value.
5. Start a manual API sync during an automatic cycle and confirm the operations do not overlap.

## 3. Safe partial update

1. Import a full vehicle row containing Paid access, balance, active status and category `10000`.
2. Import a later CSV containing only `rf_id`, `vehicle_no`, `empty_weight`.
3. Confirm Paid access, balance, active status and category remain unchanged.
4. Confirm empty weight is updated.

## 4. Capacity debit

1. Configure payment category `10000` with price `1000`.
2. Import a Paid vehicle with `vehicle_capacity=10000`.
3. Process an IN transaction.
4. Confirm debit amount is `1000`.
5. Repeat with category/capacity `6000` and price `600`.

## 5. MQTT payload

1. Process a vehicle with category/capacity `10000`.
2. Inspect the published MQTT JSON.
3. Confirm `schemaVersion` is `1.2`.
4. Confirm `vehicleCategory` is `10000`.
5. Upgrade an existing database and confirm the `trips.vehicle_category` migration completes.
