# iAWS v0.1 practical test plan

1. Configure the real weight COM port and set Target Weight / Reset Weight.
2. Confirm live data `wn009011 kg` displays as `9011 kg`.
3. Keep weight below target and scan IN/OUT RFID: no transaction must start.
4. Raise weight to target and scan IN first: IN must lock and OUT must be ignored.
5. Complete the cycle and confirm lock remains until weight falls to reset.
6. Repeat with OUT first: OUT must lock and IN must be ignored.
7. Trigger IN and OUT as close together as possible: only one direction must win.
8. Configure four camera RTSP URLs and verify all four live panels.
9. Confirm a transaction captures Camera 1-4 and local image paths.
10. Disconnect network, process a cycle, reconnect network and confirm pending transaction/images synchronize.
11. Confirm no payment screen, amount deduction or balance authorization is required for a weighing cycle.
12. Confirm app title/logo/version show RealTech iAWS v0.1.
