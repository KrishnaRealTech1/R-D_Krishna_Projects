# RealTech STP Automation - Server Interface Specification

**Application version:** 0.1.51  
**Protocol:** MQTT 3.1.1 for JSON messages; SFTP for captured images  
**Default character encoding:** UTF-8  
**Date/time format:** ISO 8601 with timezone offset

## 1. Practical working summary

The gate application is local-first. RFID validation, balance checking, entry-fee deduction,
image capture, trip creation, and barrier control continue even when the server is unavailable.
Completed trips remain in SQLite with `PendingSync` status until they are uploaded.

### Device-to-server trip flow

1. The IN or OUT lane reads an RFID card.
2. The application finds the local vehicle/RFID master record.
3. For a Paid IN trip, the current balance is checked and the configured entry fee is deducted.
4. The balance change and trip record are saved in one SQLite transaction.
5. The barrier sequence runs without waiting for the server.
6. In the background, the image is uploaded by SFTP when available.
7. The transaction JSON is published by MQTT with QoS 1.
8. The trip is marked `Synced` after MQTT PUBACK is received.

### Server-to-device recharge flow

1. The server publishes an RFID recharge command to the device-specific recharge topic.
2. The application validates the JSON, site, device, RFID, vehicle, card type, and amount.
3. The unique `rechargeId` is checked to prevent duplicate credit.
4. The `rechargeAmount` is added to the latest local balance in a serialized SQLite transaction.
5. A recharge audit row is stored in `rfid_recharges`.
6. The application publishes an acknowledgement JSON.
7. MQTT PUBACK is sent only after processing and acknowledgement publication complete. If a
   temporary failure occurs, reconnect/redelivery is safe because `rechargeId` is idempotent.

`MQTTX` is an MQTT client that can be used to test the interface. The production server can use
MQTTX or any MQTT 3.1.1-compatible client/library.

## 2. Default MQTT topics

| Direction | Purpose | Default topic | QoS | Retain |
|---|---|---|---:|---:|
| Device -> Server | Gate transaction | `IWS_STP-001/Device_Response` | 1 | false |
| Server -> Device | RFID recharge command | `IWS_STP-001/STP_RECHARGE_FROM_SERVER` | 1 | false |
| Device -> Server | Recharge acknowledgement | `IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE` | 1 | false |

All device topics are grouped below the device ID. In MQTTX, subscribe once to `IWS_STP-001/#`
to receive the gate transaction and recharge acknowledgement topics for this device. Use a unique
MQTTX client ID because an MQTT broker normally permits only one active connection per client ID.

## 3. Gate transaction JSON: device to server

### Sample normal IN transaction

```json
{
  "schemaVersion": "1.2",
  "transactionId": "8d1d31e9-9a38-472c-a3a0-df88f5bc52e7",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "laneId": "MAIN-GATE",
  "deviceName": "Tambaram STP 001",
  "direction": "IN",
  "rfidNumber": "E20034120123456789012345",
  "vehicleNumber": "TN11AB1234",
  "vehicleCategory": "10000",
  "accessType": "Paid",
  "entryTransactionId": null,
  "previousBalance": 500.00,
  "debitAmount": 50.00,
  "newBalance": 450.00,
  "imageCaptureStatus": "Captured",
  "imageFileName": "Tambaram STP Site 1 - 2026-07-22_10-30-15-125 - IN - 8d1d31e9-9a38-472c-a3a0-df88f5bc52e7.png",
  "imageRemotePath": "/home/ftpuser/ftp/STP/Tambaram/STP-IWS-001/Tambaram STP Site 1 - 2026-07-22_10-30-15-125 - IN - 8d1d31e9-9a38-472c-a3a0-df88f5bc52e7.png",
  "tripDate": "2026-07-22",
  "tripTime": "10:30:15.125",
  "timeZoneOffset": "+05:30",
  "processedAt": "2026-07-22T10:30:15.125+05:30",
  "notes": "Processed locally; server upload is asynchronous."
}
```

### Transaction field details

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `schemaVersion` | string | yes | Current transaction schema is `1.2`. |
| `transactionId` | UUID string | yes | Unique local trip ID; server should process idempotently. |
| `siteId` | string | yes | Site identifier from device configuration. |
| `deviceId` | string | yes | Device/controller identifier. |
| `laneId` | string | yes | Logical gate/lane identifier. |
| `deviceName` | string | yes | Human-readable device name. |
| `direction` | string | yes | `IN` or `OUT`. |
| `rfidNumber` | string | yes | Normalized uppercase RFID/EPC without whitespace. |
| `vehicleNumber` | string | yes | Registered vehicle number. |
| `vehicleCategory` | string | yes | Vehicle category/capacity captured when the transaction was created. For API data, `vehicle_capacity` is sent here. |
| `accessType` | string | yes | `Free` or `Paid`. |
| `entryTransactionId` | UUID/null | no | OUT trip's matching real IN trip ID. Omitted when IN is missing. |
| `previousBalance` | decimal | yes | Balance immediately before this local transaction. |
| `debitAmount` | decimal | yes | Entry fee deducted; normally zero for OUT and Free access. |
| `newBalance` | decimal | yes | Balance after the local transaction. |
| `imageCaptureStatus` | string | yes | Capture result such as `Captured` or an error status. |
| `imageFileName` | string/null | no | Local/remote image filename. |
| `imageRemotePath` | string/null | no | SFTP path after successful upload. |
| `tripDate` | string | yes | Local date, `yyyy-MM-dd`. |
| `tripTime` | string | yes | Local time with milliseconds. |
| `timeZoneOffset` | string | yes | Offset such as `+05:30`. |
| `processedAt` | ISO timestamp | yes | Backward-compatible local processed timestamp. |
| `notes` | string | yes | Processing and missing-trip notification notes. |

### Exceptional approval and missing-trip notification

For an approved missing IN or missing OUT, only the current physical trip is published. The payload also contains:

```json
{
  "missingTripNotification": {
    "missingDirection": "IN",
    "notificationOnly": true,
    "syntheticTripCreated": false,
    "countedAsTransaction": false,
    "message": "Expected IN trip was missing. Notification only; no separate missing trip was created, uploaded, or counted."
  },
  "exceptionalApproval": {
    "approvalMode": "Manual",
    "reason": "Missing IN Trip",
    "approverName": "Supervisor Name",
    "approverRole": "Shift Supervisor",
    "approverMobile": "9876543210",
    "approvalDate": "2026-07-22",
    "approvalTime": "10:31:12.450",
    "timeZoneOffset": "+05:30"
  }
}
```



**Counting rule:** the missing direction is information only. It does not create a Pending transaction, does not upload a second image or JSON message, and does not increase the Processed count. Only the current RFID-reader trip is counted.

## 4. RFID recharge command: server to device

### Sample recharge command

Publish this JSON to `IWS_STP-001/STP_RECHARGE_FROM_SERVER` with QoS 1 and retain disabled:

```json
{
  "schemaVersion": "1.0",
  "messageType": "rfidRecharge",
  "rechargeId": "RCH-20260722-000001",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "rfidNumber": "E20034120123456789012345",
  "vehicleNumber": "TN11AB1234",
  "previousBalance": -50.00,
  "rechargeAmount": 500.00,
  "newBalance": 450.00,
  "rechargedAt": "2026-07-22T10:35:00+05:30",
  "paymentReference": "PAY-20260722-77881",
  "operatorId": "OP-1007",
  "operatorName": "Recharge Operator",
  "remarks": "Cash recharge at site office"
}
```

### Recharge field details

| Field | Type | Required | Validation/usage |
|---|---|---:|---|
| `schemaVersion` | string | recommended | Use `1.0`. |
| `messageType` | string | yes | Must be `rfidRecharge`. |
| `rechargeId` | string | yes | Globally unique, maximum 100 characters; idempotency key. |
| `siteId` | string | recommended | When present, must match the device `siteId`. |
| `deviceId` | string | recommended | When present, must match the device `deviceId`. |
| `rfidNumber` | string | yes | RFID/EPC; whitespace is removed and value is uppercased. |
| `vehicleNumber` | string | optional | When present, must match the vehicle registered to the RFID. |
| `previousBalance` | decimal | optional | Server's balance before recharge; used for payload consistency/reconciliation. |
| `rechargeAmount` | decimal | yes | Positive amount to add to the latest local balance. |
| `newBalance` | decimal | optional | Server's expected balance after recharge. |
| `rechargedAt` | ISO timestamp | optional | Time the recharge occurred on the server. |
| `paymentReference` | string | optional | Payment/receipt/reference ID. |
| `operatorId` | string | optional | Server/recharge operator ID. |
| `operatorName` | string | optional | Server/recharge operator name. |
| `remarks` | string | optional | Free-text recharge note. |

When all three balance fields are present, the message is rejected if
`previousBalance + rechargeAmount` differs from `newBalance` by more than `0.01`.

### Balance conflict policy

The application applies `rechargeAmount` as a delta to the latest local balance. It does not blindly
overwrite the balance with `newBalance`. This is intentional because gate transactions are processed
locally while the server can be offline. A delayed server snapshot could otherwise erase a local debit.

The acknowledgement contains the actual local before/after balances and a `serverBalanceMatched`
flag. The server should reconcile when this flag is `false`.

## 5. Recharge acknowledgement: device to server

### Successful application

```json
{
  "schemaVersion": "1.0",
  "messageType": "rfidRechargeAck",
  "rechargeId": "RCH-20260722-000001",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "rfidNumber": "E20034120123456789012345",
  "vehicleNumber": "TN11AB1234",
  "status": "Applied",
  "success": true,
  "duplicate": false,
  "rechargeAmount": 500.00,
  "previousBalance": -50.00,
  "newBalance": 450.00,
  "serverExpectedNewBalance": 450.00,
  "serverBalanceMatched": true,
  "processedAt": "2026-07-22T10:35:01.240+05:30",
  "message": "Recharge applied to the local RFID balance."
}
```

### Duplicate command

A repeated `rechargeId` is never credited twice:

```json
{
  "schemaVersion": "1.0",
  "messageType": "rfidRechargeAck",
  "rechargeId": "RCH-20260722-000001",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "rfidNumber": "E20034120123456789012345",
  "vehicleNumber": "TN11AB1234",
  "status": "Duplicate",
  "success": true,
  "duplicate": true,
  "rechargeAmount": 500.00,
  "previousBalance": -50.00,
  "newBalance": 450.00,
  "serverExpectedNewBalance": 450.00,
  "serverBalanceMatched": true,
  "processedAt": "2026-07-22T10:35:01.240+05:30",
  "message": "Recharge ID was already processed with status Applied: Recharge applied to the local RFID balance."
}
```

### Rejected command

```json
{
  "schemaVersion": "1.0",
  "messageType": "rfidRechargeAck",
  "rechargeId": "RCH-20260722-000002",
  "siteId": "SITE-001",
  "deviceId": "IWS_STP-001",
  "rfidNumber": "E20000000000000000000000",
  "vehicleNumber": "",
  "status": "Rejected",
  "success": false,
  "duplicate": false,
  "rechargeAmount": 500.00,
  "serverExpectedNewBalance": 500.00,
  "processedAt": "2026-07-22T10:36:10.100+05:30",
  "message": "RFID is not registered."
}
```

Possible rejection reasons include invalid message type, missing RFID, non-positive amount, wrong
site/device, inconsistent balance arithmetic, unregistered RFID, inactive RFID, Free RFID, or vehicle
number mismatch.

## 6. MQTT configuration sample

```json
{
  "Server": {
    "Enabled": true,
    "SyncIntervalSeconds": 15,
    "MaxBatchSize": 25,
    "Mqtt": {
      "BrokerHost": "mqtt.example.com",
      "Port": 1883,
      "UseTls": false,
      "Username": "<username>",
      "Password": "<password>",
      "ClientId": "IWS_STP-001",
      "BaseTopic": "IWS_STP-001/Device_Response",
      "PublishTopic": "IWS_STP-001/Device_Response",
      "QualityOfService": 1,
      "Retain": false,
      "KeepAliveSeconds": 30,
      "ConnectTimeoutSeconds": 10,
      "PublishTimeoutSeconds": 10,
      "RechargeSyncEnabled": true,
      "RechargeSubscribeTopic": "IWS_STP-001/STP_RECHARGE_FROM_SERVER",
      "RechargeAckTopic": "IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE",
      "RechargeSubscriberClientId": "IWS_STP-001-recharge",
      "RechargeReconnectSeconds": 5
    }
  }
}
```

Changing Server Panel settings requires restarting the application so hosted MQTT workers reconnect
with the new configuration.

## 7. MQTTX test procedure

1. Start the RealTech STP Automation application and confirm the server log reports that the recharge
   subscriber is connected.
2. In MQTTX, create a broker connection using a unique client ID such as `MQTTX-TEST-001`.
3. Subscribe in MQTTX to `IWS_STP-001/#` using QoS 1.
4. Publish the recharge command sample to `IWS_STP-001/STP_RECHARGE_FROM_SERVER` using QoS 1 and retain false.
5. Verify an `Applied` acknowledgement is received.
6. Publish the exact same JSON again. Verify `status` is `Duplicate` and the card is not credited again.
7. Scan the RFID at the IN lane and verify the displayed/debited balance uses the recharged value.
8. Publish commands with an unknown RFID, Free RFID, wrong device ID, zero amount, and mismatched
   vehicle number. Verify `Rejected` acknowledgements.
9. Disconnect the application after it has subscribed, publish a QoS 1 message, then reconnect. Verify
   queued delivery if the broker supports persistent sessions.

## 8. Server retry rule

The server should keep a recharge in `Pending` state until it receives a matching acknowledgement by
`rechargeId`. On timeout it may safely republish the same command with the same `rechargeId`. It must
not generate a new recharge ID for a retry, because a new ID represents a new credit operation.

## 9. Security and production notes

- Use TLS (`UseTls: true`, normally port 8883) when supported by the broker.
- Use broker ACLs so each device can subscribe only to its recharge topic and publish only to its
  transaction and acknowledgement topics.
- Do not use production application client IDs in MQTTX.
- Do not enable retained recharge messages. Use QoS 1 plus acknowledgement/retry.
- Store broker and SFTP credentials with restricted file permissions.
- The application records applied recharges and RFID/master-data rejection results, including the original JSON, in SQLite for audit.

## 10. Current acknowledgement limitation for gate transactions

Gate transaction synchronization currently treats MQTT QoS 1 PUBACK as delivery success. It does not
wait for a separate server/application acknowledgement for `Device_Response`. The recharge interface does
have an application-level acknowledgement because balance credit must be explicitly confirmed.

## v0.1.40 direction and exceptional approval rules

- IN RFID reader detection produces `direction: "IN"`.
- OUT RFID reader detection produces `direction: "OUT"`.
- The current approved exceptional trip captures and uploads the current lane image exactly like a normal IN or OUT trip.
- `exceptionalApproval.approvalMode` is `Manual` or `Auto`.


## Vehicle master CSV API response format (v0.1.49)

The Vehicle CSV API may return the CSV body directly with this header:

```csv
sno,username,vehicle_no,rf_id,empty_weight,capacity,balance,rfid_type,rf_status
```

Example:

```csv
4482,tambaram_sullage,"TN 07 AR 3523",E2801191A50400742358EC3C,10000,10000,0,FREE,ACTIVE
4483,tambaram_sullage,"TN 02 Q 6009",E2801191A50400730F5163C3,10000,6000,0,FREE,ACTIVE
```

`capacity` becomes the local vehicle category/capacity. It is used for Paid IN price matching and is published to the server as MQTT `vehicleCategory`. `rf_status` accepts `ACTIVE` or `INACTIVE`.
