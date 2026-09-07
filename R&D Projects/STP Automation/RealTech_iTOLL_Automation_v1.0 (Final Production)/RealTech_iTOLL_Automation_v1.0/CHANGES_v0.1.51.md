# RealTech iTOLL Automation v0.1.51 Changes

## OUT-after-IN RFID handoff fix

Fixed a regression introduced by the v0.1.50 cross-lane same-RFID guard.

### Problem

The v0.1.50 guard kept the winning RFID claim until the entire lane process returned. For an approved trip that includes waiting for the physical `Realeased` command and then the configured post-release barrier delay. During that time the same RFID on the opposite lane was ignored, so a legitimate OUT read immediately after a completed IN passage could be lost.

### New behavior

- Same-RFID IN/OUT processing is still mutually exclusive while the first lane physically owns the vehicle sensor cycle.
- The cross-lane RFID claim is released immediately when that lane receives its physical `Realeased` / `Released` command.
- OUT may therefore process the same RFID immediately after `IN Realeased`, without waiting for the IN `BarrierAndGreenDelaySeconds` completion delay.
- The behavior is symmetric: IN may process the RFID immediately after `OUT Realeased`.
- A release/claim race is handled: if the sensor is already LOW when the claim is acquired, the stale claim is released immediately.
- Final cleanup is owner-aware. An older IN process cannot accidentally remove a newer OUT claim for the same RFID (and vice versa).
- Per-lane `DuplicateReadSeconds` remains unchanged and does not block an OUT read because IN and OUT use different duplicate-read keys.

### Version

Application, assembly and file version updated to `0.1.51`.
