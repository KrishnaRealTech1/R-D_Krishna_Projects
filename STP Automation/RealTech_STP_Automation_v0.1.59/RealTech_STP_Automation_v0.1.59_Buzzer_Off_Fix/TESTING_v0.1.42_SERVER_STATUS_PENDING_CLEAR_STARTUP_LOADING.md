# Testing v0.1.42

## A. Startup loading screen

1. Close the application completely.
2. Start the application.
3. Confirm the orange loading window appears before the dashboard.
4. Confirm the status text and percentage change through configuration, storage, database, service and dashboard stages.
5. Confirm the loading window closes after the main dashboard opens.
6. Confirm no fixed/artificial delay is added.

## B. Server connectivity status

1. Confirm **SERVER** appears next to **INTERNET** on the dashboard.
2. With MQTT and SFTP reachable, confirm the card is green and shows `Connected (MQTT + SFTP)`.
3. Block SFTP port 22 only and confirm the card becomes orange with `MQTT online; SFTP offline`.
4. Block MQTT port 1883 only and confirm the card becomes orange with `SFTP online; MQTT offline`.
5. Block both endpoints and confirm the card becomes red with `Server offline`.
6. Disable server synchronization and confirm the card shows `Disabled`.

## C. Clear Pending Data

1. Create at least two pending trips with local image files.
2. Open **Data > Clear Pending Data...**.
3. Confirm the dialog displays the number of pending transactions.
4. Click **No** and confirm nothing is deleted.
5. Repeat and click **Yes**.
6. Confirm the pending dashboard count becomes zero.
7. Confirm pending rows are removed from SQLite.
8. Confirm their local image files are deleted when present.
9. Confirm already synchronized trip rows remain unchanged.
10. Confirm a second clear attempt displays `There are no pending transactions to clear.`
11. Run a server sync at the same time and confirm the clear operation waits for the active sync cycle instead of racing with it.

## D. MQTT identity fields

For each response below, confirm the payload contains `siteId`, `deviceId`, `laneId` and `deviceName`:

- Normal IN trip
- Normal OUT trip
- Manual exceptional approval trip
- Auto exceptional approval trip
- Recharge Applied acknowledgement
- Recharge Rejected acknowledgement
- Recharge Duplicate acknowledgement
