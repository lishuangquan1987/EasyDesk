@echo off
REM ============================================================
REM EasyRDP Mirror Driver - sign driver with a test certificate
REM
REM Creates a self-signed test certificate and signs the driver
REM binaries. Required on 64-bit Win7 (or any 64-bit Windows) where
REM kernel drivers must be signed; with testsigning enabled, a driver
REM signed by a test cert will load.
REM
REM Run as Administrator on the target machine.
REM Usage:  sign.bat   (signs the binaries in x86\ and x64\)
REM ============================================================
setlocal

set WDK=C:\WinDDK\7600.16385.1
set SRC=%~dp0
set CERT=%SRC%easyrdp-test.cer
set SUBJ=CN=EasyRDP Test

set MK=%WDK%\bin\x86\MakeCert.exe
set SG=%WDK%\bin\x86\SignTool.exe

if not exist "%MK%" (
  echo ERROR: MakeCert.exe not found. WDK7.1 must be installed.
  exit /b 1
)

echo === Creating self-signed test certificate ===
"%MK%" -r -pe -ss My -sr LocalMachine -n "%SUBJ%" "%CERT%" || goto :fail

echo === Trusting the test certificate (install to Root store) ===
certutil -addstore Root "%CERT%" || goto :fail

echo.
echo === Signing x64 driver ===
"%SG%" sign /a /s My /sr LocalMachine /n "EasyRDP Test" "%SRC%x64\mirror_m64.sys" || goto :fail
"%SG%" sign /a /s My /sr LocalMachine /n "EasyRDP Test" "%SRC%x64\mirror64.dll" || goto :fail

echo.
echo === Signing x86 driver ===
"%SG%" sign /a /s My /sr LocalMachine /n "EasyRDP Test" "%SRC%x86\mirror_m.sys" || goto :fail
"%SG%" sign /a /s My /sr LocalMachine /n "EasyRDP Test" "%SRC%x86\mirror.dll" || goto :fail

echo.
echo === Signing complete ===
echo Verify with:  signtool verify /pa x64\mirror_m64.sys
echo Then run install.bat and reboot.
goto :end

:fail
echo.
echo ERROR: signing failed.
exit /b 1

:end
endlocal
