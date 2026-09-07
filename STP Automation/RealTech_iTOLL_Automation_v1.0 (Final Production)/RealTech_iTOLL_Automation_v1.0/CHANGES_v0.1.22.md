# Changes in v0.1.22

- Approved IN and OUT lane completion now waits specifically for the UNO commands
  `IN Realeased` and `OUT Realeased`.
- Raw sensor LOW, CLEAR, OFF, NOT DETECTED, and NO VEHICLE text is no longer
  accepted as an approved-lane completion trigger.
- The normal spellings `IN Released` and `OUT Released` are also accepted as safe
  aliases, while the packaged configuration uses the UNO spelling `Realeased`.
- After the release command is received, the existing configurable
  `Processing.BarrierAndGreenDelaySeconds` timer runs. ORG remains ON and the
  barrier remains OPEN during the delay.
- When the delay expires, the app sends `CLOSE <lane> BB`, `<lane> ALL OFF`, and
  `<lane> GRN` in that order.
- Duplicate release messages cannot repeat the completion sequence for the same
  lane cycle.
- Admin simulation buttons now say `Send Realeased` to test the real completion
  trigger.
