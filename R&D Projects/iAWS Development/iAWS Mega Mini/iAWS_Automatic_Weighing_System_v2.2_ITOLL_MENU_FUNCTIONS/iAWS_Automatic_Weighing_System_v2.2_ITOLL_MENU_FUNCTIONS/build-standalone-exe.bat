@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-win-x64.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo Build failed. Review the error shown above.
) else (
    echo Build completed. The EXE is in Publish\StandaloneWinX64.
)

echo.
pause
exit /b %EXIT_CODE%
