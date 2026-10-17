@echo off
setlocal
if not exist "%~dp0Chay-ung-dung.ps1" (
    echo Missing Chay-ung-dung.ps1. Extract the FULL source ZIP before running.
    pause
    exit /b 2
)
if /i "%~1"=="--check-project" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Chay-ung-dung.ps1" -CheckOnly
    exit /b
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Chay-ung-dung.ps1" -InitOnly
set "PAWN_EXIT=%ERRORLEVEL%"
if not "%PAWN_EXIT%"=="0" pause
exit /b %PAWN_EXIT%
