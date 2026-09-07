# v0.1.29 exceptional approval enable/disable test plan

## Admin Controls UI and persistence

1. Start the application and open **Admin Controls -> Open Simulation / Hardware Test Panel...**.
2. Enter the admin password.
3. Confirm the **Exceptional Approval Controls** section contains independent IN and OUT checkboxes.
4. Disable IN, leave OUT enabled, and click **Save Settings**.
5. Confirm the success message and status-log entry show `IN=disabled, OUT=enabled`.
6. Close and reopen Admin Controls and confirm the saved values are restored.
7. Restart the application and confirm the values remain unchanged.
8. Inspect the runtime configuration and confirm it contains:

```json
"ExceptionalApproval": {
  "InEnabled": false,
  "OutEnabled": true
}
```

## IN exceptional approval disabled

1. Create an active IN trip for a registered vehicle without completing its OUT trip.
2. Start another IN sensor/RFID cycle for the same vehicle.
3. Confirm no Exceptional Approval popup opens.
4. Confirm the lane is rejected with `Missing OUT trip - IN exceptional approval is disabled by Admin`.
5. Confirm the barrier remains closed and the lane returns to idle.
6. Confirm no reconciliation OUT and no new current IN record are added.

## IN exceptional approval enabled

1. Enable IN exceptional approval and save.
2. Repeat the missing-OUT scenario.
3. Confirm the Exceptional Approval popup opens and the existing approval/reconciliation workflow still succeeds.

## OUT exceptional approval disabled

1. Disable OUT exceptional approval and save.
2. Use a registered vehicle with no active IN trip and start an OUT sensor/RFID cycle.
3. Confirm no Exceptional Approval popup opens.
4. Confirm the lane is rejected with `Missing IN trip - OUT exceptional approval is disabled by Admin`.
5. Confirm no reconciliation IN and no OUT record are added.

## OUT exceptional approval enabled

1. Enable OUT exceptional approval and save.
2. Repeat the missing-IN scenario.
3. Confirm the Exceptional Approval popup opens and the existing approval/reconciliation workflow still succeeds.

## Independence and normal processing

1. Test all four combinations: both enabled, IN only, OUT only, both disabled.
2. Confirm each lane follows only its own setting.
3. Complete a normal matched IN and OUT while both exceptional approvals are disabled.
4. Confirm normal trips are unaffected.

## Existing v0.1.28 configuration compatibility

1. Start v0.1.29 with an existing runtime `appsettings.json` that has no `ExceptionalApproval` section.
2. Confirm startup succeeds.
3. Open Admin Controls and confirm both options default to enabled.
4. Save the settings and confirm the new section is written to the existing configuration file.
