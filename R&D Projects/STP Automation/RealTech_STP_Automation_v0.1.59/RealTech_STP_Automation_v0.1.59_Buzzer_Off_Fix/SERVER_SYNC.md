# MQTT and SFTP synchronization

## Processing order

1. Read pending trips from SQLite.
2. When the local image exists and SFTP mode is enabled, upload the image.
3. Publish the transaction JSON to the configured MQTT topic.
4. With QoS 1, wait for the broker PUBACK packet.
5. Mark the local trip as synced.

If SFTP or MQTT fails, the trip remains pending and is retried during the next cycle.
A missing local image is reported in the server log but does not prevent the transaction JSON
from being published.

## MQTT topic

`Server.Mqtt.PublishTopic` is used when it is non-empty. Otherwise `Server.Mqtt.BaseTopic` is used.
The supplied configuration publishes to `IWS_STP-001/Device_Response`. MQTTX can subscribe once to `IWS_STP-001/#` to monitor every topic for this device.

## JSON payload shape

```json
{
  "schemaVersion": "1.2",
  "transactionId": "00000000-0000-0000-0000-000000000000",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "laneId": "MAIN-GATE",
  "deviceName": "Tambaram STP 001",
  "direction": "IN",
  "rfidNumber": "E2...",
  "vehicleNumber": "TN00AA0000",
  "vehicleCategory": "10000",
  "accessType": "Paid",
  "entryTransactionId": null,
  "previousBalance": 500.00,
  "debitAmount": 50.00,
  "newBalance": 450.00,
  "imageCaptureStatus": "Captured",
  "imageFileName": "transaction-guid.png",
  "imageRemotePath": "/home/ftpuser/ftp/STP/Tambaram/STP-IWS-001/transaction-guid.png",
  "tripDate": "2026-07-20",
  "tripTime": "15:00:00.125",
  "timeZoneOffset": "+05:30",
  "processedAt": "2026-07-20T15:00:00.125+05:30",
  "notes": "Processed locally; server upload is asynchronous."
}
```

Every normal IN and OUT transaction contains the vehicle category/capacity, local trip date, local trip time, timezone
offset, and `processedAt`. CSV fields such as `vehicle_capacity` are stored and published as `vehicleCategory`. The duplicate `tripTimestamp` and `tripTimestampUtc` fields are no longer
published. The `exceptionalApproval` property is omitted for normal trips.

For a missing IN or missing OUT approval, only the current physical trip is queued.
The payload contains `missingTripNotification` plus `exceptionalApproval`. No synthetic trip is
queued, uploaded or counted:

```json
{
  "missingTripNotification": {
    "missingDirection": "IN",
    "notificationOnly": true,
    "syntheticTripCreated": false,
    "countedAsTransaction": false
  },
  "exceptionalApproval": {
    "approvalMode": "Manual",
    "reason": "Missing IN Trip",
    "approverName": "Supervisor Name",
    "approverRole": "Shift Supervisor",
    "approverMobile": "9876543210",
    "approvalDate": "2026-07-20",
    "approvalTime": "15:01:12.450",
    "timeZoneOffset": "+05:30"
  }
}
```

The structured approval data is saved in SQLite before synchronization, so an MQTT
retry after an application restart sends the same original approval date and time. The duplicate
`approvedAt` and `approvedAtUtc` fields are no longer published. Legacy synthetic reconciliation rows created by earlier builds are archived locally and excluded from synchronization and dashboard counts. Pending
exceptional physical trips created by the immediately previous build are also supported:
the sync worker reads their original structured values from the legacy `notes` format.

## Acknowledgement behavior

This version treats MQTT QoS 1 PUBACK as successful delivery to the MQTT broker. It does not wait
for a separate application/server acknowledgement topic. If the server requires a custom response,
its acknowledgement topic and payload must be added before treating that response as the final sync.

## RFID recharge synchronization (v0.1.37)

The application maintains a persistent MQTT subscription for server-originated Paid RFID recharges.
The default command topic is `IWS_STP-001/STP_RECHARGE_FROM_SERVER` and the default acknowledgement
topic is `IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE`.

A recharge command uses `messageType: "rfidRecharge"`, a globally unique `rechargeId`, an RFID number,
and a positive `rechargeAmount`. The amount is added to the latest local balance in an atomic,
serialized database operation. The command's `previousBalance` and `newBalance` are reconciliation
values; they do not overwrite newer local debits.

Processed recharge IDs are stored in `rfid_recharges`. Re-delivery of the same ID produces a
`Duplicate` acknowledgement and does not credit the card again. See `SERVER_INTERFACE.md` for the full
JSON contract and MQTTX test procedure.

## v0.1.38 reliability and timing behavior

The transaction worker now records image-upload completion separately from the final MQTT synchronization state. A successfully uploaded image is reused on later MQTT retries by reading `remote_image_path` from SQLite.

Pending transactions are eligible when `next_sync_attempt_at` is empty or has elapsed. A failed item receives exponential backoff from `Server.InitialRetryDelaySeconds` up to `Server.MaxRetryDelaySeconds`; processing then continues with the other eligible items in the batch.

SFTP uses a reusable authenticated connection, prepares the remote directory once per connection, and applies `Server.ImageUpload.TotalTimeoutSeconds` as the wall-clock limit for one image attempt. After a connection failure, a short in-memory cooldown prevents the rest of the same batch from repeatedly waiting for the identical unavailable server.

The server log records cycle, transaction, image, payload, MQTT acknowledgement, database-state, failure, retry, and elapsed-time details. The status log prefixes active lane processing with an elapsed timer such as `[IN PROCESS 00:04.218 | RFID ...]`.

## Reader direction and approved exception flow

- Data received from the configured IN RFID port is processed as `direction: "IN"`.
- Data received from the configured OUT RFID port is processed as `direction: "OUT"`.
- A Manual or Auto approved exception continues through the same current-lane camera capture, SFTP upload and `Device_Response` publish process as a normal trip.
- Approved exception JSON includes `approvalMode`, `reason`, `approverName`, `approverRole`, `approverMobile`, approval date/time and time-zone offset.

