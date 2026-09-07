# v0.1.20 Functional Test

## Configuration

Confirm these values in `src/RfidVehicleAccess.App/appsettings.json` or in the
published application's `appsettings.json`:

```json
"Processing": {
  "BarrierAndGreenDelaySeconds": 5
},
"Security": {
  "AdminControlsPassword": "7799"
}
```

Restart the application after manually changing `appsettings.json`.

## IN lane sequence

1. Open **Admin Controls** and enter `7799`.
2. Select **IN Sensor HIGH**.
3. Send a valid IN RFID.
4. Confirm the commands reach ORG, buzzer, and `OPEN IN BB`.
5. Select **IN Sensor LOW**.
6. During the configured delay, confirm ORG remains on and the barrier remains open.
7. At delay expiry, confirm this command order:
   - `CLOSE IN BB`
   - `IN ALL OFF`
   - `IN GRN`
8. Confirm the dashboard shows the IN barrier closed and the IN signal green.

## OUT lane sequence

Repeat the same test with a vehicle that has an active IN trip. Expected completion
commands are:

- `CLOSE OUT BB`
- `OUT ALL OFF`
- `OUT GRN`

## Password checks

- Incorrect password: Admin Controls must remain closed and the password field clears.
- Password `7799`: Admin Controls opens.
- Cancel or Escape: Admin Controls remains closed.

## Hardware sensor-message checks

The parser accepts configured messages such as `IN Not Detected` and also tolerates
common separators and wrappers, for example:

- `IN: Not Detected`
- `STATUS - IN Not Detected`
- `OUT_NOT_DETECTED`
- `OUT Not Detected - INPUT 0`

Check `status.log` for `IN sensor changed to LOW.` or `OUT sensor changed to LOW.`.
If that line is absent, set `Hardware.SensorMessages.InLow` or `OutLow` to the exact
text emitted by the control unit.
