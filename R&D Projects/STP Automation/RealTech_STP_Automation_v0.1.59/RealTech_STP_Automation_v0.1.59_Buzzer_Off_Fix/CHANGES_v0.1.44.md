# RealTech STP Automation v0.1.44 Changes

## Vehicle-category payment configuration

- Added a new top-menu **Payment Configuration** option.
- Protected the option with the default password `7799`.
- Added an editable category/price grid with add, delete, edit and default-category selection.
- Persisted payment categories in the runtime `appsettings.json` file.
- Preserved backward compatibility by converting the former fixed `Processing.EntryFee` into the first default category when an older configuration is loaded.
- Added `vehicle_category` to the local vehicle database and automatic migration for existing databases.
- Added vehicle category/capacity import aliases for CSV/XLSX/XLSM files.
- Changed PAID IN processing to resolve the debit using the vehicle category. FREE access remains zero.
- Blank or unknown vehicle categories safely use the configured default category price.
