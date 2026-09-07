# Changes in v0.1.39

## Device-root MQTT topic layout

- MQTTX can monitor this device with one wildcard subscription: `IWS_STP-001/#`.
- Gate transactions publish to `IWS_STP-001/STP_COM`.
- Recharge commands are received from `IWS_STP-001/STP_RECHARGE_FROM_SERVER`.
- Recharge acknowledgements publish to `IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE`.
- The MQTT client IDs and configured device ID now use `IWS_STP-001`.
- Existing v0.1.38 default settings are migrated in memory at startup, so an upgrade does not keep publishing to the old topic layout.

## Transaction JSON cleanup

- Removed `tripTimestamp` and `tripTimestampUtc` from gate transaction JSON.
- Removed `approvedAt` and `approvedAtUtc` from `exceptionalApproval`.
- Retained `tripDate`, `tripTime`, `processedAt`, `approvalDate`, `approvalTime`, and timezone offsets.
- Local SQLite approval timestamps are unchanged; only the MQTT JSON contract is simplified.
