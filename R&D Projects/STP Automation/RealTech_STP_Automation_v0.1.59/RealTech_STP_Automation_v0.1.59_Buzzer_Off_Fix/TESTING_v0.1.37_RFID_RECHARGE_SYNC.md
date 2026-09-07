# Testing v0.1.37 - RFID Recharge Synchronization

1. Configure a Paid RFID with a known balance.
2. Start the application and confirm the server log shows the recharge subscriber connected.
3. Subscribe to the acknowledgement topic in MQTTX.
4. Publish a valid QoS 1 recharge command and verify `Applied` acknowledgement.
5. Scan the RFID and verify the new balance is used.
6. Republish the same recharge ID and verify `Duplicate`; confirm the balance does not change again.
7. Test unregistered, inactive, Free, vehicle-mismatch, wrong-site, wrong-device, zero, and negative amount cases.
8. Verify the `rfid_recharges` row stores the raw JSON, server balances, local balances, status, and timestamps.
9. During the configured IN process delay, publish a recharge. Verify the final balance equals old balance plus recharge minus entry fee.
10. Stop the broker, publish/scan locally, restore it, and verify pending trips retry and recharge subscriber reconnects.
11. Verify MQTTX uses a different client ID from both `ClientId` and `RechargeSubscriberClientId`.
