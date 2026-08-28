@echo off
REM ============================================================
REM EasyRDP Mirror Driver - install script (XP / Win7)
REM
REM Installs the XDDM mirror display driver without relying on the
REM inf right-click path (which Windows may reject for Class=Display).
REM Uses sc create + reg add directly.
REM
REM Run as Administrator. Requires mirror.dll and mirror_m.sys in the
REM same directory as this script.
REM ============================================================
setlocal

set SRC=%~dp0
set SYS=%SystemRoot%\System32\drivers
set DLLDIR=%SystemRoot%\System32

echo Copying driver files...
copy /Y "%SRC%mirror_m.sys" "%SYS%\" || goto :fail
copy /Y "%SRC%mirror.dll"  "%DLLDIR%\" || goto :fail

echo Creating kernel driver service 'mirror'...
sc stop mirror >nul 2>&1
sc delete mirror >nul 2>&1
sc create mirror type= kernel start= system error= ignore binPath= "%SYS%\mirror_m.sys" DisplayName= "EasyRDP Mirror Display Driver" || goto :fail

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
goto :end

:fail
echo.
echo ERROR: installation failed. Run this script from an Administrator
echo command prompt, and ensure mirror.dll / mirror_m.sys are beside it.
exit /b 1

:end
endlocal
