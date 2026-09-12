# RealTech iAWS v0.1 - Status / Control UI Fix

This update is based on the previous UI Polish Fix project and keeps the iAWS single-weighbridge processing logic unchanged.

## Changes in this update

1. Added **PROCESSED** and **PENDING** counters to the right-side dashboard below Date/Time, using the same dashboard count source already used by the original iTOLL project.
2. Changed the top menu item from the version label to **About**. Clicking About now shows the RealTech iAWS product name and v0.1 information.
3. Manual IN/OUT Signal, Buzzer and Boom Barrier operations from the Control Panel now update the main dashboard immediately. The existing hardware command is still sent normally.
4. Existing automatic hardware command feedback continues to update the same dashboard indicators.

## Preserved

- One shared weighbridge
- First IN/OUT RFID wins
- Opposite RFID ignored during the active cycle
- Four editable cameras
- Weight target / reset logic
- RFID, COM, MQTT, server sync and image upload logic
- Existing signal, buzzer and barrier command strings
