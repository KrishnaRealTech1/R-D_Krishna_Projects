@echo off
setlocal EnableExtensions

echo ============================================================
echo WINDOWS DASHBOARD KIOSK - V4 DIAGNOSTIC
echo ============================================================
echo.

echo CURRENT USER:
whoami
echo.

echo HKCU RUN ENTRY:
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DashboardKiosk
echo.

echo WATCHDOG FILE:
dir "C:\Kiosk\Chrome-Kiosk-Watchdog.vbs"
echo.

echo CHROME LOCATIONS:
if exist "%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe" echo FOUND: %LOCALAPPDATA%\Google\Chrome\Application\chrome.exe
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" echo FOUND: %ProgramFiles%\Google\Chrome\Application\chrome.exe
if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" echo FOUND: %ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe
echo.

echo KIOSK LOG:
type "C:\Kiosk\kiosk.log" 2>nul
echo.

echo RUNNING CHROME:
wmic process where "name='chrome.exe'" get ProcessId,CommandLine 2>nul
echo.

echo Starting the V4 watchdog now...
start "" wscript.exe "C:\Kiosk\Chrome-Kiosk-Watchdog.vbs"

echo.
echo Wait 15 seconds. If Chrome does not open, press a key and
echo send a screenshot of this window plus C:\Kiosk\kiosk.log.
echo.
timeout /t 15 /nobreak >nul

echo.
echo UPDATED KIOSK LOG:
type "C:\Kiosk\kiosk.log" 2>nul
echo.
pause
