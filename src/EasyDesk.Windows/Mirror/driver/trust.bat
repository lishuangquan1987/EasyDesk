@echo off
REM ============================================================
REM EasyRDP Mirror Driver - trust the test certificate on target machine
REM
REM The driver binaries in this release folder were ALREADY signed on the
REM build machine (with WDK). To load them on 64-bit Win7, the machine must
REM (a) have test signing enabled AND (b) trust the self-signed test
REM certificate. This script installs easyrdp-test.cer into the trusted
REM root store. It uses only built-in Windows tools (certutil) - no WDK
REM needed.
REM
REM Run as Administrator. Usage:  trust.bat
REM ============================================================
setlocal

set SRC=%~dp0
set CERT=%SRC%easyrdp-test.cer

if not exist "%CERT%" (
  echo ERROR: easyrdp-test.cer not found beside this script.
  exit /b 1
)

echo === Installing test certificate into trusted root store ===
certutil -addstore Root "%CERT%"

echo === Installing test certificate into current-user My store ===
certutil -user -addstore My "%CERT%"

echo.
echo Certificate installed. Now run install.bat and reboot.
echo Verify test signing is on:  bcdedit /enum {current} ^| findstr testsigning
goto :end

:end
endlocal
