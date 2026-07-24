@echo off
chcp 65001 >nul
if "%~2"=="" (
  echo 用法：生成完整安装包.cmd "Unity Build目录" "新输出目录"
  echo 示例：生成完整安装包.cmd "D:\Build\OurDoor" "D:\Release\OurDoor-LAN"
  pause
  exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Package-OurDoor.ps1" -UnityBuildPath "%~1" -OutputPath "%~2"
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" pause
exit /b %EXIT_CODE%
