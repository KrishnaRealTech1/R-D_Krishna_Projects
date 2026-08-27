# v0.1.32 Server Panel Test

1. Build or publish the application using `build-standalone-exe.bat`.
2. Start the application.
3. Confirm the old `Open Configuration File` command is no longer shown under File.
4. Open `Server Panel > Open Server Configuration Panel...`.
5. Verify an incorrect password is rejected.
6. Verify `rts123!@#` opens the panel on an existing configuration without `ServerPanelPassword`.
7. Review every tab and confirm current `appsettings.json` values are loaded.
8. Change a noncritical value such as `DeviceName`, select `Save & Apply`, close and reopen the panel, and confirm it persisted.
9. Change the Server Panel password, save, close the panel, and verify the new password is required.
10. Change approval options and confirm the Admin Controls panel reflects the changes.
11. In simulation mode, change COM settings and save. Confirm no physical port is opened.
12. With hardware connected, change a COM setting and confirm the status reports the reconnect result.
13. Change an RTSP URL or camera enabled flag and confirm playback reloads.
14. Enter invalid values such as duplicate COM ports, an invalid RTSP URL, or an out-of-range network port and confirm saving is blocked with a clear message.
15. Change the database path and confirm the panel reports that an application restart is required.
