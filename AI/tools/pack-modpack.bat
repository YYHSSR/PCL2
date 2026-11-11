@echo off
rem Double-click to pack the modpack. Extra arguments are passed through, e.g. pack-modpack.bat -IncludeSaves
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack-modpack.ps1" %*
echo.
pause
