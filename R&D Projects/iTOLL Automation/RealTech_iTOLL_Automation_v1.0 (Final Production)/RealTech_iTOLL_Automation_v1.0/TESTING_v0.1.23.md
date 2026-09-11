# v0.1.23 matching and dated-log test

## Preparation

Use simulation mode or connected hardware. Import a valid RFID and keep:

```json
"Processing": {
  "SensorValiditySeconds": 30,
  "BarrierAndGreenDelaySeconds": 5
}
```

## IN: sensor first

1. Send `IN Detected`.
2. Confirm the lane displays that it is waiting to match an RFID.
3. Send a valid IN RFID.
4. Confirm processing starts exactly once.
5. After approval, confirm ORG and buzzer turn on and the IN barrier opens.
6. Send `IN Realeased`.
7. Confirm the configured completion delay runs, then commands are sent in this
   order: `CLOSE IN BB`, `IN ALL OFF`, `IN GRN`.

## IN: RFID first

1. With the IN sensor LOW, send a valid IN RFID.
2. Confirm no database process, snapshot, or barrier command starts yet.
3. Confirm the lane displays `RFID detected - waiting for vehicle sensor`.
4. Within `SensorValiditySeconds`, send `IN Detected`.
5. Confirm processing starts exactly once using the buffered RFID.
6. Complete the cycle with `IN Realeased`.

## Expired RFID-first buffer

1. Send an RFID while the lane sensor is LOW.
2. Wait longer than `SensorValiditySeconds`.
3. Send the matching lane `Detected` command.
4. Confirm the old RFID does not start processing.
5. Send the RFID again and confirm processing starts.

## Repeated frame protection

1. Send `IN Detected` and repeatedly inject the same RFID.
2. Confirm only one process starts for that HIGH cycle.
3. Repeat for OUT.

## OUT tests

Repeat the sensor-first, RFID-first, expiry, and repeated-frame tests using
`OUT Detected`, OUT RFID, and `OUT Realeased`.

## Date-wise logs

Generate at least one status event and one server-sync event. Confirm these files
are created for the current local date:

```text
Documents\Realtech_systems\Logs\Status\YYYY\MM\DD\status.log
Documents\Realtech_systems\Logs\Server\YYYY\MM\DD\server.log
```

Confirm status messages are not written into the Server folder and server-sync
messages are not written into the Status folder.
