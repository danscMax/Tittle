@echo off
:: Associate .md/.markdown with Tittle for the current user (no admin).
:: Default target is the Debug (dev) build. Pass -Target Portable for dist\Tittle.exe.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0install-fileassoc.ps1" %*
pause
