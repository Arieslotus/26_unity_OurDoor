@echo off
chcp 65001 >nul
set "PACKAGE_ROOT=%~dp0."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0HostTools\Install-OurDoorHost.ps1" -PackageRoot "%PACKAGE_ROOT%"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" echo Host installation failed. Exit code: %EXIT_CODE%
pause
exit /b %EXIT_CODE%
