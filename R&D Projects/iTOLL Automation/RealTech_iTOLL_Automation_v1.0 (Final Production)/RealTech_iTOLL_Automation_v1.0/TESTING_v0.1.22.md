# v0.1.22 release-command test

## Configuration

Confirm `appsettings.json` contains:

```json
"Processing": {
  "BarrierAndGreenDelaySeconds": 5
},
"Hardware": {
  "SensorMessages": {
    "InHigh": "IN Detected",
    "InReleased": "IN Realeased",
    "OutHigh": "OUT Detected",
    "OutReleased": "OUT Realeased"
  }
}
```

## IN test

1. Send `IN Detected`.
2. Present a valid IN RFID and wait for processing to complete.
3. Confirm `IN ORG`, `IN Buzzer`, and `OPEN IN BB` are sent.
4. Send a raw message such as `IN LOW` or `IN Not Detected`. Confirm the barrier
   remains open and ORG remains on.
5. Send `IN Realeased`. Confirm the configured delay starts.
6. During the delay, confirm ORG remains on and the barrier remains open.
7. After the delay, confirm this exact order:
   - `CLOSE IN BB`
   - `IN ALL OFF`
   - `IN GRN`
8. Send `IN Realeased` again. Confirm the sequence does not run again.

## OUT test

Repeat the same procedure with `OUT Detected`, a valid OUT RFID, and
`OUT Realeased`. The final command order must be:

- `CLOSE OUT BB`
- `OUT ALL OFF`
- `OUT GRN`

## Accepted spelling alias

The app also accepts `IN Released` and `OUT Released` so correcting the UNO
spelling later does not require another application update.
