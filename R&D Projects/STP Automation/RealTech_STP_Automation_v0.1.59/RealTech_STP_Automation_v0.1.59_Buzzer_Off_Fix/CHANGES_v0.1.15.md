# Changes in v0.1.15

- Replaced the placeholder server synchronization worker with real synchronization logic.
- Added MQTT 3.1.1 publishing over TCP or TLS without an additional MQTT package.
- Added QoS 0/1 support; QoS 1 waits for broker PUBACK before marking a transaction as synced.
- Added SFTP image upload through SSH.NET.
- Added configurable MQTT topic, QoS, keep-alive, connection timeout, publish timeout, and batch size.
- Added configurable SFTP connection and operation timeouts.
- Transactions remain pending when MQTT or SFTP fails and are retried automatically.
- Missing local images no longer block transaction publishing; the payload records image capture status.
- Reduced repeated server-log noise when no transactions are pending.
- Updated the default deployment configuration for the Tambaram STP device settings supplied for this release.

## MQTT payload

The configured topic receives UTF-8 JSON containing transaction ID, site/device/lane metadata,
IN/OUT direction, RFID, vehicle number, access type, balances, image status/path, processing time,
and notes.

## Synchronization completion rule

A transaction is marked `Synced` only after any available local image is uploaded successfully by
SFTP and the MQTT broker acknowledges the QoS 1 publish. This release does not wait for a separate
application-level acknowledgement topic because no acknowledgement contract was supplied.
