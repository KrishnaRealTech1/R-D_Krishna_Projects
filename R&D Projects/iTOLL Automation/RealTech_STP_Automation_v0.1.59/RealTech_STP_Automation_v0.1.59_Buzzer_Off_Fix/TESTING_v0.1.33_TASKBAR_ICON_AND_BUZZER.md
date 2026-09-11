# v0.1.33 Taskbar Icon and Buzzer Test

1. Run `build-standalone-exe.bat` and confirm the publish completes without errors.
2. Start `Publish\StandaloneWinX64\RealTechSTPAutomation.exe` directly.
3. Confirm the RealTech RTS icon is visible on the Windows taskbar instead of the generic WPF window icon.
4. Confirm the RealTech icon is also displayed in the application title bar and Alt+Tab view.
5. Open `Admin Controls > Open Simulation / Hardware Test Panel...` and enter the configured password.
6. Select the IN Buzzer command and confirm the graphical IN buzzer below the IN signal illuminates for approximately 1.5 seconds.
7. Select the IN Buzzer command repeatedly and confirm the illuminated duration restarts without flicker or an early reset.
8. Select the OUT Buzzer command and confirm only the OUT buzzer indicator illuminates.
9. Select `IN ALL OFF` or `OUT ALL OFF` and confirm the matching buzzer indicator clears immediately.
10. Process an automatic IN and OUT lane sequence that sends a buzzer command and confirm the same dashboard indicator responds.
11. Minimize and restore the application and confirm the taskbar icon remains the RealTech icon.
12. If an old pinned shortcut still shows a cached icon, unpin the old shortcut and pin the newly published executable.
