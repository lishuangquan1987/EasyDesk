@echo off
REM ============================================================
REM EasyRDP Mirror Driver - install script (XP / Win7)
REM
REM Installs the XDDM mirror display driver without relying on the
REM inf right-click path (which Windows may reject for Class=Display).
REM Uses sc create + reg add directly.
REM
REM Auto-detects 32/64-bit and picks the matching driver binaries.
REM   x86 -> uses x86\mirror.dll + x86\mirror_m.sys
REM   x64 -> uses x64\mirror64.dll + x64\mirror_m64.sys
REM
REM Run as Administrator. Requires the x86\ and x64\ subfolders beside
REM this script.
REM ============================================================
setlocal

set SRC=%~dp0
set SYS=%SystemRoot%\System32\drivers
set DLLDIR=%SystemRoot%\System32

REM Detect architecture (PROCESSOR_ARCHITECTURE: AMD64 / x86 / IA64)
if /i "%PROCESSOR_ARCHITECTURE%"=="AMD64" goto :is64
if /i "%PROCESSOR_ARCHITECTURE%"=="IA64" goto :is64
goto :is32

:is64
set DLLNAME=mirror64.dll
set SYSNAME=mirror_m64.sys
set SUBDIR=x64
echo Detected 64-bit Windows, installing x64 driver.
goto :install

:is32
set DLLNAME=mirror.dll
set SYSNAME=mirror_m.sys
set SUBDIR=x86
echo Detected 32-bit Windows, installing x86 driver.
goto :install

:install
if not exist "%SRC%%SUBDIR%\%DLLNAME%" goto :fail
if not exist "%SRC%%SUBDIR%\%SYSNAME%" goto :fail

echo Copying driver files...
copy /Y "%SRC%%SUBDIR%\%SYSNAME%" "%SYS%\" || goto :fail
copy /Y "%SRC%%SUBDIR%\%DLLNAME%" "%DLLDIR%\" || goto :fail

echo Creating kernel driver service 'mirror'...
sc stop mirror >nul 2>&1
sc delete mirror >nul 2>&1
sc create mirror type= kernel start= system error= ignore binPath= "%SYS%\%SYSNAME%" DisplayName= "EasyRDP Mirror Display Driver" || goto :fail
REM Critical: the mirror miniport must load in the Video group at boot
REM (like the WDK inf's LoadOrderGroup=Video). Without it the service
REM stays disabled / fails with ERROR 1058.
sc config mirror group= Video || goto :fail

echo Writing display driver registration...
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror" /v MirrorDriver /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror" /v InstalledDisplayDrivers /t REG_MULTI_SZ /d mirror /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror" /v VgaCompatible /t REG_DWORD /d 0 /f >nul

echo Writing device0 attach-to-desktop settings...
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror\device0" /v "Device Description" /t REG_SZ /d "EasyRDP Mirror" /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror\device0" /v "Installed Display Drivers" /t REG_SZ /d mirror /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror\device0" /v MirrorDriver /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\mirror\device0" /v "Attach.ToDesktop" /t REG_DWORD /d 1 /f >nul

echo.
echo Install complete. Reboot required for the mirror driver to attach
echo to the desktop and start capturing dirty rectangles.
echo.
echo After reboot verify with:  sc query mirror
echo.
echo NOTE: 64-bit Win7 requires test signing enabled:
echo   bcdedit /set testsigning on   (then reboot)
goto :end

:fail
echo.
echo ERROR: installation failed. Run this script from an Administrator
echo command prompt, and ensure the x86\ and x64\ subfolders are beside it.
exit /b 1

:end
endlocal
