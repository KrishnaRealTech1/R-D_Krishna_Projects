# iAWS v0.1 implementation summary

## Implemented

- Rebranded the legacy application as RealTech iAWS.
- Applied the supplied iAWS logo and application icon assets.
- Set application version to v0.1.
- Removed payment configuration and payment/authorization/recharge processing from the active application flow.
- Added one shared weighbridge serial service and parser for data such as `wn009011 kg`.
- Added configurable target and reset weights.
- Added atomic first-RFID-wins direction locking for IN vs OUT.
- Opposite RFID is ignored until the shared weighbridge resets.
- Replaced sensor-triggered transaction start with weight-target arming.
- Added four editable camera streams and four-image transaction capture.
- Updated server synchronization payload to include weight, target and Camera 1-4 data.
- Reworked dashboard to match the supplied hand-drawn iAWS layout.
- Added weight and camera configuration to control/server panels.

## Practical cycle example

Target = 5000 kg, Reset = 100 kg.

```text
wn000000 kg -> idle
wn004999 kg -> idle
wn005000 kg -> armed
OUT RFID arrives first -> OUT lock
IN RFID arrives -> ignored
Camera 1-4 captured -> record saved -> server sync queued
OUT boom/signal/buzzer sequence runs
weight falls <= 100 kg -> cycle reset
next IN/OUT RFID race is allowed only after the next target crossing
```
