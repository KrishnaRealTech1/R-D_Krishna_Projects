# Changes in v0.1.11

## Dashboard layout and import sample

- Reduced the vertical space used by the two live RTSP camera panels.
- Increased the Vehicle Identification IN/OUT row height and replaced the read-only text boxes with equal bordered display panels so both lanes align consistently.
- Increased the Vehicle Identification value font for easier gate-operator viewing.
- Widened the right dashboard and enlarged the RFID, TYPE and BALANCE labels and values.
- Added tooltips so a full RFID or identification value remains available if the window becomes too narrow.
- Included the supplied 3,150-row `rf_id` CSV as `sample-data/rf_id_import.csv`.
- Kept missing `rfid_type` values compatible with the configured default (`Free`). A later PAID/FREE file can be imported again to update existing records.
- Upgraded the application version to 0.1.11.

## Build note

The binary UHF reader build correction from v0.1.10 is included.
