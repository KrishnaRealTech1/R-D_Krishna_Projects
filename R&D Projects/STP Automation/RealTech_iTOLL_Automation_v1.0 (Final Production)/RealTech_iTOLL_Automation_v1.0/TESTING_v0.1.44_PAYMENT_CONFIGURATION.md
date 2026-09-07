# v0.1.44 Payment Configuration Test Checklist

1. Start with an older configuration that has only `Processing.EntryFee`; verify one `Default` payment category is created in memory with that price.
2. Open **Payment Configuration** and verify an incorrect password is rejected and `7799` opens the window.
3. Add `10000 Liter Capacity` with `1000` and `6000 Liter Capacity` with `600`.
4. Edit a name and price, select a default category, save, close and reopen; verify all values persist.
5. Verify deleting the only remaining category is blocked.
6. Verify blank names, duplicate names, negative prices and invalid price text are blocked.
7. Import PAID vehicles using `vehicle_category` or `capacity` and verify the category is stored.
8. Process a PAID IN for each configured category and verify the matching amount is debited.
9. Process a vehicle with a blank or unconfigured category and verify the selected default price is debited.
10. Process a FREE vehicle and verify the debit remains zero.
11. Open an existing database and verify startup adds `vehicles.vehicle_category` without losing records.
12. Build in Debug and Release configurations.
