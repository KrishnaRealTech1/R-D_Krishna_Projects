# iAWS UI update - 2026-09-14

Changes requested and implemented:

- Branding logo area enlarged; `v0.1` removed from the branding/footer display; `AUTOMATIC WEIGHING SYSTEM` text changed to green.
- Decorative orange startup/theme accents changed to green. Functional orange traffic-signal/buzzer states are intentionally preserved because they represent hardware state.
- Internet, Server and Hardware/COM cards now use the same iTOLL-style live red/green connectivity behavior (server partial connectivity remains amber, as in iTOLL).
- Weight, IN RFID and OUT RFID displays were restyled to match the supplied reference with a vertical connection lamp.
  - Weight lamp: live weighbridge connection.
  - IN RFID lamp: IN RFID COM connection.
  - OUT RFID lamp: OUT RFID COM connection.
- Vehicle Number was added directly below the Weight display and is populated from the active weighing transaction; it clears when the weight returns to the configured reset threshold.

No weighing-cycle, first-RFID-wins, camera, barrier, server sync, or transaction processing logic was changed.
