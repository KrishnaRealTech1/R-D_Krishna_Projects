# Changes in v0.1.26

- Unregistered RFID scans now send a RED command containing a display value instead of the plain RED command:
  - `IN RED NOT Registered`
  - `OUT RED NOT Registered`
- This allows the control unit to use the same two-stage RED display sequence used for registered vehicle numbers.
- The existing rejection status remains `NOT REGISTERED` in the application UI and logs.
