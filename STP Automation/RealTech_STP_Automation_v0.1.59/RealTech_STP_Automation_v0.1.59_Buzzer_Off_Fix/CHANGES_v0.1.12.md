# Changes in v0.1.12

## CSV import compatibility and data-quality handling

- Separated import-time RFID prefix validation from live lane validation.
- Added `Import:EnforceRfidPrefixValidation` and set it to `false` for the supplied master CSV.
- Added `Import:SkipInvalidRows` and set it to `true` so incomplete/conflicting rows are skipped with review notes instead of counted as failed imports.
- Normalized RFID values by removing all whitespace and converting to uppercase.
- Consolidated repeated RFID rows using a deterministic first-valid-row rule.
- Treated vehicle numbers that differ only by spacing or punctuation as equivalent for duplicate detection.
- Added import statistics for duplicate, incomplete, conflicting and outside-prefix rows.
- Updated the import result dialog to show a concise summary and review notes.
- Explicitly ignores blank CSV/Excel rows.
- Upgraded the application version to 0.1.12.

## Supplied CSV verification

For `sample-data/rf_id_import.csv` on a new empty database:

```text
Rows: 3150, Inserted: 2154, Updated: 0, Skipped: 996, Failed: 0
```

Breakdown:

- 2,154 unique non-empty RFID/vehicle mappings.
- 991 repeated RFID rows.
- 5 incomplete rows.
- 22 rows with RFID values outside the configured runtime `E2` prefix, allowed by import settings.
- 11 repeated rows with conflicting vehicle assignments, retained as review notes while the first valid assignment is kept.
