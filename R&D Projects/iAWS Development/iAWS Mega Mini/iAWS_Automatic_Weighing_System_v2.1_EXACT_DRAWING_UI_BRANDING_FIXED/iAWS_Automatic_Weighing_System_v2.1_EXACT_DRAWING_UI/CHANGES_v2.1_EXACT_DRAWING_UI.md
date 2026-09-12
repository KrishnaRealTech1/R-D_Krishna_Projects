# iAWS v2.1 - Exact Drawing UI + RealTech Branding

This update keeps the existing iAWS weighing workflow intact and replaces the main dashboard presentation with the structure supplied in the hand-drawn UI reference.

## Main UI
- Four live cameras arranged as Camera 1 / 2 / 3 / 4 in a 2 x 2 monitor block.
- Right-side RealTech iAWS branding block.
- Date / Time block.
- Internet / Server and Hardware / COM connectivity blocks.
- Large Weight Data block with a dedicated weighbridge connectivity indicator.
- IN RFID panel containing signal tower, buzzer and boom-barrier presentation.
- OUT RFID panel containing signal tower, buzzer and boom-barrier presentation.
- Server Log on the bottom-left.
- Status Log on the bottom-right.

## Branding
- Added supplied RealTech iAWS logo artwork.
- Added Windows application icon generated from the supplied iAWS artwork.
- Applied branding to main window, loading window, executable metadata and taskbar/window icon.

## Existing workflow preserved
- Existing weight input and trigger logic is unchanged.
- Existing RFID reader handling is unchanged.
- Existing four-camera RTSP stream/capture logic is unchanged.
- Existing REST API processing is unchanged.
- Existing settings window and configuration file format are unchanged.

The IN/OUT hardware controls are currently the first-pass dashboard presentation requested for the UI. Their final independent hardware command logic can be added in the next requirement updates without changing the established weighing workflow.
