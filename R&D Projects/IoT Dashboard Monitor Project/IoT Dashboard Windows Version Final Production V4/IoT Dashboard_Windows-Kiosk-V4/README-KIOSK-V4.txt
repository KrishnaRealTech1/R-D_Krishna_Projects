WINDOWS DASHBOARD KIOSK - STARTUP FIX V4
========================================

V4 removes PowerShell from the Chrome watchdog completely.

STARTUP FLOW
------------
Windows boots
  -> Windows auto-login
  -> HKCU Run executes:
       wscript.exe "C:\Kiosk\Chrome-Kiosk-Watchdog.vbs"
  -> watchdog waits 10 seconds
  -> Chrome opens in kiosk mode
  -> https://web.itank.io/login/ opens
  -> C:\Kiosk\ChromeProfile is reused
  -> watchdog relaunches Chrome if it is closed

INSTALL
-------
1. Extract the complete ZIP.
2. Run:
       INSTALL-KIOSK-V4.cmd
3. Wait 10-15 seconds.
4. Chrome should open immediately.
5. Log into the dashboard once if necessary.
6. Reboot Windows.
7. Chrome should open automatically after login.

DIAGNOSTIC
----------
If Chrome does not open, run:
       TEST-KIOSK-V4.cmd

Then send the full screenshot.

PERSISTENT CHROME PROFILE
-------------------------
C:\Kiosk\ChromeProfile

Do not delete this directory if you want the browser cookies/login retained.
