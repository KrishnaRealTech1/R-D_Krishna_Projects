# v0.1.24 release-event completion test

## OUT approved-cycle test

1. Send `OUT Detected` and provide a valid OUT RFID.
2. Confirm processing completes and these commands are sent:
   `OUT ALL OFF`, `OUT ORG`, `OUT Buzzer`, `OPEN OUT BB`.
3. Confirm the status log says it is waiting for `OUT Realeased`.
4. Send `OUT Realeased` after the UNO standing-vehicle rule is satisfied.
5. Confirm the log contains both:
   - `Control-unit message received: OUT Realeased (Out, Realeased).`
   - `OUT Realeased command received and registered for the active lane cycle.`
6. Confirm the configured completion timer starts immediately.
7. After the timer, confirm command order:
   `CLOSE OUT BB`, `OUT ALL OFF`, `OUT GRN`.
8. Confirm ORG is OFF, GRN is ON, and the OUT barrier is closed.

## IN approved-cycle test

Repeat the same test with `IN Detected`, a valid IN RFID, and `IN Realeased`.
The final command order must be:
`CLOSE IN BB`, `IN ALL OFF`, `IN GRN`.

## Early release test

1. Start a valid lane process.
2. Send the release command while validation, process delay, snapshot capture,
   or database save is still running.
3. Confirm the event is remembered.
4. Once the barrier-open command is issued, confirm the completion timer starts
   and the normal close/ALL OFF/GRN sequence completes.

## Repeated release test

1. Open an approved lane barrier.
2. Send the lane release command two or more times.
3. Confirm the first valid event completes the waiting cycle.
4. Confirm only one barrier-close sequence is sent.

## Diagnostics

When a physical sensor goes LOW but the barrier remains open, verify that the
status log contains a recognized `Control-unit message received` entry. If it
is absent, confirm the UNO serial output, COM port selection, baud rate, and the
exact transmitted text.
