@echo off
:: Combined build: test gate + portable single-file exe for every architecture.
:: Pass-through args, e.g.: build_all.bat -SkipTests -Rids win-x64
powershell.exe -ExecutionPolicy Bypass -File "%~dp0build_all.ps1" %*
pause
