# v0.1.26 unregistered RFID command test

## IN lane

1. Present an RFID that is not registered while the IN vehicle sensor is active.
2. Confirm the Status Log contains `Command sent: IN RED NOT Registered`.
3. Confirm the control unit starts its IN RED display sequence.
4. Confirm the application still reports the RFID as not registered and returns the lane to its safe idle state.

## OUT lane

1. Present an RFID that is not registered while the OUT vehicle sensor is active.
2. Confirm the Status Log contains `Command sent: OUT RED NOT Registered`.
3. Confirm the control unit starts its OUT RED display sequence.
4. Confirm the application still reports the RFID as not registered and returns the lane to its safe idle state.
