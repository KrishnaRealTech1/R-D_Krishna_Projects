# v0.1.34 Test Checklist

## Build

1. Run `build-standalone-exe.bat` or `publish-win-x64.ps1`.
2. Confirm restore and publish complete without C# or XAML errors.
3. Confirm the application About menu displays version `0.1.34`.

## Responsive Server Panel

1. Set Windows display resolution to 1024×768 or use display scaling that reduces the working area.
2. Open **Server Panel** with password `rts123!@#`.
3. Confirm the complete window, status area, and bottom buttons remain inside the desktop working area.
4. Confirm the window can be resized smaller and larger.
5. Open Hardware and scroll from Hardware General to Sensor Messages.

## ORANGE and buzzer synchronization

Test IN and OUT separately:

1. Send the ORANGE command from Admin Controls.
2. Confirm the ORANGE lamp and buzzer symbol turn ON together.
3. Wait more than two seconds and confirm the buzzer remains ON.
4. Send GREEN, RED, or ALL OFF and confirm the buzzer turns OFF when ORANGE is cleared.
5. Run a normal approved trip and confirm ORANGE and buzzer remain ON until the release/completion sequence clears ORANGE.

## Frontend indicator switches

1. Open Admin Controls.
2. Disable RED and save.
3. Send an IN RED and OUT RED command.
4. Confirm the control commands are still logged/sent but both RED dashboard lamps remain OFF.
5. Repeat for GREEN, ORANGE, and BUZZER.
6. With ORANGE frontend disabled and BUZZER enabled, send ORANGE and confirm only the buzzer symbol is active.
7. With BUZZER frontend disabled and ORANGE enabled, send ORANGE and confirm only the ORANGE lamp is active.
8. Restart the application and confirm all saved switches persist.

## Server Panel persistence

1. Open Server Panel → Hardware.
2. Change one or more Frontend (UI) Hardware Indicators.
3. Select Save & Apply.
4. Confirm the main dashboard updates immediately.
5. Confirm `appsettings.json` contains `FrontendIndicators` with the saved values.
