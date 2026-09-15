# iAWS Update - Audio Announcement

- Added an Audio Interface section under Server Panel > Device & Processing.
- RFID Detected Audio:
  - Enable/Disable.
  - Import an audio file from the PC.
  - Configure play count (for example, 3 = play three times).
  - Plays once a valid RFID wins the active weighbridge cycle.
- Process Completed / Barrier Open Audio:
  - Enable/Disable.
  - Import a separate audio file from the PC.
  - Configure play count (for example, 3 = play three times).
  - Starts after processing completes and the OPEN boom-barrier command is sent.
- Imported audio files are copied into `Documents\Realtech_iAWS\Audio`.
- Supported import selections: WAV, MP3, WMA, M4A and AAC (actual playback codec availability depends on Windows Media Foundation on the target PC).
- Announcement requests are serialized so the two announcement clips do not play over each other.
- Existing iAWS UI, weighing, RFID, camera, server, barrier and connectivity behavior is preserved.
