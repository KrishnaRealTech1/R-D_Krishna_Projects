@echo off
setlocal EnableExtensions

echo Removing CURRENT USER kiosk startup...

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DashboardKiosk /f >nul 2>&1
schtasks /Delete /TN "Dashboard Chrome Kiosk" /F >nul 2>&1
del /F /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Dashboard-Kiosk.cmd" >nul 2>&1

echo.
echo Automatic Chrome startup removed.
echo C:\Kiosk\ChromeProfile was NOT deleted.
echo.
pause
