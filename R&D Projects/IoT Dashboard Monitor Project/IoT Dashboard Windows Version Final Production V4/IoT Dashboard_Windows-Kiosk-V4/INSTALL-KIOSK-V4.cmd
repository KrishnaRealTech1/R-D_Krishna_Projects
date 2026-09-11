@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ============================================================
echo WINDOWS DASHBOARD KIOSK - STARTUP FIX V4
echo ============================================================
echo.
echo V4 uses VBScript for the watchdog.
echo There is no generated PowerShell and no PowerShell parser check.
echo.

if not exist "%~dp0Chrome-Kiosk-Watchdog.vbs" (
    echo ERROR: Chrome-Kiosk-Watchdog.vbs is missing.
    echo Extract the complete ZIP before running this file.
    echo.
    pause
    exit /b 2
)

if not exist "C:\Kiosk" mkdir "C:\Kiosk"
if errorlevel 1 (
    echo ERROR: Could not create C:\Kiosk
    echo.
    pause
    exit /b 3
)

if not exist "C:\Kiosk\ChromeProfile" mkdir "C:\Kiosk\ChromeProfile"

copy /Y "%~dp0Chrome-Kiosk-Watchdog.vbs" "C:\Kiosk\Chrome-Kiosk-Watchdog.vbs" >nul
if errorlevel 1 (
    echo ERROR: Could not copy watchdog to C:\Kiosk
    echo.
    pause
    exit /b 4
)

echo Removing previous Chrome kiosk startup methods...
schtasks /Delete /TN "Dashboard Chrome Kiosk" /F >nul 2>&1
del /F /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Dashboard-Kiosk.cmd" >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DashboardKiosk /f >nul 2>&1

echo Registering CURRENT USER startup...
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" ^
 /v DashboardKiosk ^
 /t REG_SZ ^
 /d "wscript.exe \"C:\Kiosk\Chrome-Kiosk-Watchdog.vbs\"" ^
 /f

if errorlevel 1 (
    echo.
    echo ERROR: Could not create the HKCU Run entry.
    echo.
    pause
    exit /b 5
)

echo.
echo Startup registration:
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v DashboardKiosk

echo.
echo Starting kiosk immediately for testing...
del /F /Q "C:\Kiosk\kiosk.log" >nul 2>&1
start "" wscript.exe "C:\Kiosk\Chrome-Kiosk-Watchdog.vbs"

echo.
echo Wait about 15 seconds.
echo Chrome should open automatically.
echo.
echo After Chrome opens, reboot Windows once to verify startup.
echo.
pause
exit /b 0
