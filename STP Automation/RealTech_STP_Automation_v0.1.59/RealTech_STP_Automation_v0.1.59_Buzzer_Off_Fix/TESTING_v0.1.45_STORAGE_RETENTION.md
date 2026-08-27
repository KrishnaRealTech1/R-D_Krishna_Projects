# v0.1.45 Storage Retention Test Checklist

1. Open **Server Panel > Storage & Import** and confirm the Storage section shows:
   - Enable Automatic Image / Log Deletion
   - Image Retention Days
   - Log Retention Days
2. Confirm the default values are enabled, 30 days for images, and 30 days for logs.
3. Enter `0`, a negative number, or a value above `3650` and verify **Save & Apply** rejects it.
4. Create test files under image folders such as `Images/2026/01/01/IN/` and log folders such as `Logs/Status/2026/01/01/`.
5. Set retention to a value that makes those test dates expired, select **Save & Apply**, and confirm the files and empty date folders are deleted.
6. Confirm current-date image and log files remain.
7. Place a file directly under the image/log root without a `YYYY/MM/DD` path and confirm it is not deleted.
8. Disable automatic deletion, save, restart, and confirm old test files remain.
9. Confirm the database file remains unchanged during every cleanup test.
10. Import `sample-data/vehicle_import_liter_capacity_sample.csv` after configuring matching payment categories and confirm the paid vehicles receive their category prices.
