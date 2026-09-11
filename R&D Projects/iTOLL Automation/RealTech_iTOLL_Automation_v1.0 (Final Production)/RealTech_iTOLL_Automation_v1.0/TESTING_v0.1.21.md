# v0.1.21 Functional Test

## Admin password dialog

1. Open the Admin Controls menu.
2. Confirm the complete Cancel and Unlock buttons are visible.
3. Test at Windows display scale 100%, 125%, and 150%.
4. Confirm password `7799` opens Admin Controls.
5. Confirm an incorrect password keeps the dialog open and shows the validation text.

## Required IN sequence

1. Trigger `IN Sensor HIGH`.
2. Send a valid IN RFID.
3. Wait for processing and local transaction save to complete.
4. Confirm these commands are sent:
   - `IN ALL OFF`
   - `IN ORG`
   - `IN Buzzer`
   - `OPEN IN BB`
5. Confirm the barrier remains open and ORG remains on while the sensor is HIGH.
6. Trigger `IN Sensor LOW`.
7. Confirm the configured timer starts only after LOW.
8. During the timer, confirm ORG remains on and the barrier remains open.
9. At timer expiry, confirm:
   - `CLOSE IN BB`
   - `IN ALL OFF`
   - `IN GRN`

## Required OUT sequence

Repeat the same test using the OUT lane and a vehicle with an active IN trip. Expected
completion commands are:

- `CLOSE OUT BB`
- `OUT ALL OFF`
- `OUT GRN`

## Race-condition test

Trigger Sensor LOW immediately as the OPEN command appears. The LOW must still be
captured. The timer must start after OPEN, and the lane must complete normally.

## Configuration

The delay remains editable in the published application's `appsettings.json`:

```json
"Processing": {
  "BarrierAndGreenDelaySeconds": 5
}
```

Restart the application after manually changing this value.
