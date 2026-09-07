# Testing v0.1.40 - Device_Response and Exceptional Approval

1. Open Server Panel and verify Base Topic and Publish Topic are `IWS_STP-001/Device_Response`.
2. Present an RFID to the IN reader and verify the published JSON contains `direction: "IN"`.
3. Present an RFID to the OUT reader and verify the published JSON contains `direction: "OUT"`.
4. Complete a normal IN and OUT trip and verify camera capture, SFTP upload and `imageRemotePath`.
5. Trigger a Manual exceptional approval and verify the current lane image is captured/uploaded and JSON contains `approvalMode: "Manual"`, approver name, role and 10-digit mobile.
6. Enable Auto approval, trigger an exception and verify JSON contains `approvalMode: "Auto"`, `approverName: "SYSTEM"`, lane auto-approval role and `approverMobile: "N/A"`.
7. Start with an existing v0.1.39 database and verify the application adds the `exceptional_approval_mode` column without data loss.
8. Start with a legacy `STP_COM` topic and verify startup normalizes it to `<deviceId>/Device_Response`.
