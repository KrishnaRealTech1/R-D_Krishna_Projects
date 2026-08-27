# Changes in v0.1.37

- Added server-to-device MQTT subscription for RFID recharge commands.
- Added device-to-server recharge acknowledgement JSON.
- Added device-specific recharge topic, acknowledgement topic, subscriber client ID, and reconnect settings.
- Added atomic Paid RFID recharge application using `rechargeAmount` as a delta.
- Added `rfid_recharges` SQLite audit table and unique `rechargeId` duplicate protection.
- Added Applied, Duplicate, and Rejected processing results.
- Serialized local entry-debit and recharge balance mutations to prevent lost balance updates.
- Entry trip balance values are refreshed from the database at commit time so a recharge received during lane processing is retained.
- Serialized MQTT publishes to prevent simultaneous connections using the same publishing client ID.
- Added Server Panel controls for recharge synchronization settings.
- Added `SERVER_INTERFACE.md` with transaction, recharge, acknowledgement, and MQTTX examples.
