# v0.1.57 - Online authorization + contractor balance offline fallback

## Added
- Preserved existing RFID/vehicle balance processing.
- Added selectable offline balance mode: RFID or CONTRACTOR.
- Added selectable processing mode: OFFLINE_ONLY or ONLINE_OFFLINE.
- Added MQTT request/response trip authorization for IN and OUT.
- Server REJECT is final; only unavailable/timeout falls back to offline processing.
- Added local contractor master and shared contractor balance.
- Added local vehicle-category debit-price cache populated by the Vehicle API import.
- Added API import columns: contractor_id/code, contractor_name, contractor_balance, contractor_active, debit_amount.
- Preserved one-negative-trip rule in offline mode with atomic SQLite balance update.
- Added contractor/category/authorization fields to local trip records and MQTT sync payload.
- Added Server Panel controls for processing mode, balance mode, authorization timeout, and MQTT request/response topics.

## New API CSV columns/aliases
- contractor_id / contractor_code / user_id / user_code
- contractor_name / user_name / contractor
- contractor_balance / user_balance / account_balance
- contractor_active / user_active
- vehicle_category (existing aliases retained)
- debit_amount / category_debit_amount / trip_amount / price

## Operational rule
ONLINE_OFFLINE: valid server approval/rejection is authoritative. MQTT/network timeout or connection failure uses local cached data. OFFLINE_ONLY never waits for online authorization. All physical trips are still stored locally and the existing sync worker uploads pending records after connectivity returns.
