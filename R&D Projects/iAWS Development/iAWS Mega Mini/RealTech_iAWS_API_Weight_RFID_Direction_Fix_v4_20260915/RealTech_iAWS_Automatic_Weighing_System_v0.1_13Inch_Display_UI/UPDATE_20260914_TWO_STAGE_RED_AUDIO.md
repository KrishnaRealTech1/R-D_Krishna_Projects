# iAWS Update - Two-stage RED display + audio interface

- Stage 1: automatic IN/OUT RED command now sends the common `RED <Vehicle Number>` payload in the same locked control write.
- Stage 2: after the live weighbridge value remains within 0.5 kg for 3 continuous seconds, iAWS sends `RED <Vehicle Number> <Weight>`.
- The final transaction weight is the confirmed stable weight.
- Added configurable audio interface in Server Panel > Device & Processing > Audio Interface.
- Separate RFID-detected and Process-completed/Barrier-open audio files can be imported from the PC.
- Imported audio is copied to `Documents\Realtech_iAWS\Audio`.
- Each audio cue has Enable/Disable and configurable repeat count.
- Audio cues are serialized so they do not overlap.
