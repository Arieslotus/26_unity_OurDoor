@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0HostTools\Invoke-OurDoorHost.ps1" -Action Start -LaunchGame -PackageRoot "%~dp0."
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo Server startup failed. The game was not launched. Exit code: %EXIT_CODE%
  pause
)
exit /b %EXIT_CODE%
