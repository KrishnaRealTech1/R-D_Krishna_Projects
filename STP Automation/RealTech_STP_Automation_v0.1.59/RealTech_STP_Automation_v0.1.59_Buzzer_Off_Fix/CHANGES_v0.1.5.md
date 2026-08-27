# RFID Vehicle Access Starter v0.1.5 dashboard counter and signal changes

- Removed the **REGISTERED** counter from the live dashboard.
- The dashboard now shows only:
  - **PROCESSED**: transactions successfully uploaded/synchronized today.
  - **PENDING**: locally saved transactions still waiting for upload.
- A transaction moves out of **PENDING** after `MarkSyncedAsync` changes its status to `Synced`.
- **PROCESSED** is based on `synced_at` for the current local calendar day and resets at local midnight.
- Added an explicit midnight refresh in the dashboard clock timer.
- Combined the processed/pending dashboard query into one SQLite query.
- Removed signal colour/status text from the lane-signal cards.
- Retained only the IN and OUT lane labels.
- Added a glow effect to the exact RED, ORANGE, or GREEN lamp corresponding to the last command sent to the control unit.
- `ALL OFF` dims all three lamps.

The server transport remains a placeholder until the MQTT/API and image-upload protocol is supplied. The counter transition is already tied to the persisted `PendingSync` and `Synced` statuses.
