# Testing v0.1.39

## MQTTX topic test

1. Connect MQTTX with a client ID different from the application client IDs.
2. Subscribe to `IWS_STP-001/#` with QoS 1.
3. Process an IN or OUT vehicle and verify a message arrives on `IWS_STP-001/STP_COM`.
4. Publish a valid recharge command to `IWS_STP-001/STP_RECHARGE_FROM_SERVER`.
5. Verify the acknowledgement arrives on `IWS_STP-001/STP_RECHARGE_ACK_FROM_DEVICE`.
6. Verify no separate `STP_RECHARGE/STP-IWS-001` or `STP_RECHARGE_ACK/STP-IWS-001` subscription is required.

## JSON test

For a normal trip, verify the payload contains `tripDate`, `tripTime`, `timeZoneOffset`, and `processedAt`, but does not contain `tripTimestamp` or `tripTimestampUtc`.

For an exceptional approval, verify `exceptionalApproval` contains `approvalDate`, `approvalTime`, and `timeZoneOffset`, but does not contain `approvedAt` or `approvedAtUtc`.
