# Changes in v0.1.24

- Fixed approved OUT and IN lanes remaining with the boom barrier open and ORG
  active after the UNO sends the lane `Realeased` command.
- `Realeased`/`Released` is now handled as an explicit lane-completion event
  instead of depending only on a local HIGH-to-LOW state transition.
- A repeated release line can safely unblock the currently active approved lane
  cycle when the local sensor state is already LOW.
- Release commands received during RFID validation, process delay, snapshot
  capture, database storage, or the barrier-open transition are retained and
  applied to that same lane cycle.
- Added defensive recovery for an active processing cycle whose local sensor
  state was already LOW when the release command was received.
- Replaced blocking control-port `ReadLine()` handling with a thread-safe
  non-blocking receive buffer, preventing a one-time release line from being
  lost when serial data arrives in partial CR/LF chunks.
- Added status-log entries for every recognized control-unit sensor/release line
  to make field diagnosis easier.
- Added accepted release aliases for `Realeased`, `Realesed`, `Released`, and
  `Release`, while keeping the configured UNO message as the primary value.
- IN and OUT continue to operate independently, and duplicate release lines
  cannot run the close/GRN sequence more than once.
