@echo off
:: Portable single-file build. Double-click to produce dist\Tittle.exe.
:: Pass-through args, e.g.: build.bat -ReadyToRun
powershell.exe -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
pause
