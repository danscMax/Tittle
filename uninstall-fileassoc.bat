@echo off
:: Remove the .md/.markdown -> SeriousView association for the current user.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0uninstall-fileassoc.ps1" %*
pause
