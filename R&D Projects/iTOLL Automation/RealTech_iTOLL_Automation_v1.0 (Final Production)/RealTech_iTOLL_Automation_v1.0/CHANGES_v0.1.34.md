# Changes in v0.1.34

## Responsive Server Panel

- The Server Panel now calculates its initial width and height from the Windows working area.
- The panel no longer opens larger than the available display area on 1024×768 and similar screens.
- Reduced the minimum window size and made configuration label/editor columns scale with the available width.
- Retained vertical scrolling inside every configuration tab and enabled resize grips.

## ORANGE and buzzer dashboard behavior

- The graphical buzzer now turns ON together with the matching IN or OUT ORANGE state.
- The buzzer remains ON for the complete ORANGE period instead of switching off after a 1.5-second visual pulse.
- Clearing ORANGE by RED, GREEN, or ALL OFF also clears the matching dashboard buzzer.
- A manual IN/OUT BUZZER command still turns the matching buzzer indicator ON.
- Hardware command strings and the physical lane sequence remain unchanged.

## Frontend indicator controls

- Added separate frontend enable/disable switches for RED, GREEN, ORANGE, and BUZZER.
- Added the switches to Admin Controls under **Frontend (UI) Hardware Control**.
- Added the same settings to the Server Panel Hardware tab.
- Disabling an indicator only keeps that indicator OFF in the main dashboard. Hardware commands continue to be sent normally.
- Settings are persisted under `FrontendIndicators` in `appsettings.json`.
- Existing configurations default all four frontend indicators to enabled.

## Admin Controls layout

- Added scrolling so the new controls remain accessible on smaller displays.
- Moved the common Save Settings button to the bottom of the panel.
- Saving now persists approval, auto-approval, and frontend indicator settings together.

## Version

- Updated application version to `0.1.34`.
