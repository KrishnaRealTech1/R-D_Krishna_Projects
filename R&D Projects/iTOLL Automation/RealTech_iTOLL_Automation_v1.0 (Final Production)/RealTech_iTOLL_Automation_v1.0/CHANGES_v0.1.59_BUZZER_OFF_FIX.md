# v0.1.59 - Buzzer OFF Reliability Fix

## Fixed
- Automatic approved-trip completion now sends the dedicated `IN Buzzer OFF` / `OUT Buzzer OFF` command before `ALL OFF`.
- Lane idle/reset now also sends the dedicated buzzer OFF command.
- Approved barrier sequence has an uncancelled best-effort buzzer OFF cleanup in `finally`, so cancellation or an exception after buzzer activation cannot leave the buzzer latched ON.
- Completion logging now reports BUZZER OFF explicitly.

## Why
The hardware exposes a separate `Buzzer OFF` command. The previous automatic flow relied on `ALL OFF`, while the manual UI already used the dedicated buzzer command. On controllers where `ALL OFF` does not clear the buzzer relay, the buzzer remained ON after trip completion.
