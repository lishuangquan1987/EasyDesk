@echo off
REM ============================================================
REM EasyRDP Mirror Driver - sign driver with a test certificate
REM
REM Creates a self-signed test certificate (CurrentUser My store) and
REM signs the driver binaries. Required on 64-bit Win7 where kernel
REM drivers must be signed; with testsigning enabled, a driver signed
REM by a test cert will load.
REM
REM Run as Administrator on the BUILD machine (needs WDK 7.1).
REM The signed binaries in x86\ and x64\ are then copied to the target.
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

echo === Creating self-signed test certificate (CurrentUser My) ===
"%MK%" -r -pe -ss My -n "%SUBJ%" "%CERT%" || goto :fail

echo === Signing x64 driver ===
"%SG%" sign /a /s My /n "EasyRDP Test" "%SRC%x64\mirror_m64.sys" || goto :fail
"%SG%" sign /a /s My /n "EasyRDP Test" "%SRC%x64\mirror64.dll" || goto :fail

echo.
echo === Signing x86 driver ===
"%SG%" sign /a /s My /n "EasyRDP Test" "%SRC%x86\mirror_m.sys" || goto :fail
"%SG%" sign /a /s My /n "EasyRDP Test" "%SRC%x86\mirror.dll" || goto :fail

echo.
echo === Signing complete ===
echo Then on the TARGET machine run trust.bat (install easyrdp-test.cer
echo into the root store) and install.bat, then reboot.
goto :end

:fail
echo.
echo ERROR: signing failed.
exit /b 1

:end
endlocal
