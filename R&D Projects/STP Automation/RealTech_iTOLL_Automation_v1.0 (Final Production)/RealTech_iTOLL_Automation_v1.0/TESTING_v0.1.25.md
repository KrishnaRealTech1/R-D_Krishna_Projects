# v0.1.25 vehicle-number RED command and dashboard layout test

## Automatic IN test

1. Start the application with the control COM port connected.
2. Present a registered IN-lane RFID whose vehicle number is `TN 56 H 8658`.
3. Confirm the Status Log contains `Command sent: IN RED TN 56 H 8658`.
4. Confirm the on-screen IN signal changes to red.
5. Confirm the control unit receives the full vehicle number, including spaces.

## Automatic OUT test

1. Present a registered OUT-lane RFID whose vehicle number is `KA 01 AB 1234`.
2. Confirm the Status Log contains `Command sent: OUT RED KA 01 AB 1234`.
3. Confirm the on-screen OUT signal changes to red.
4. Confirm the control unit receives the full vehicle number, including spaces.

## Rejection fallback

1. Present an unregistered RFID.
2. Confirm the lane still receives the plain `IN RED` or `OUT RED` safety command.
3. Confirm the rejection sequence restores the lane to its idle green state.

## Dashboard layout

1. Confirm Capture Storage is a compact card at the bottom-right of the flow area.
2. Confirm its bottom edge aligns with the bottom of OUT FLOW.
3. Confirm Status Log and Server Log extend across the complete window width.
