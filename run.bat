@echo off
:: Quick developer run from source (Debug). Pass a file to open it: run.bat README.md
powershell.exe -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
if errorlevel 1 pause
