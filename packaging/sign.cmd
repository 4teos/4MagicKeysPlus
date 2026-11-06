@echo off
if "%1"=="elevated" goto run
fltmc >nul 2>&1 && goto run
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList elevated -Verb RunAs"
exit /b
:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign.ps1"
pause
