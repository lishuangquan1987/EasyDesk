@echo off
REM ============================================================
REM EasyRDP Mirror Driver - build + package release
REM
REM Rebuilds the XDDM mirror driver with WDK 7.1 and packages the
REM install files (dll, sys, inf, install.bat) into the release\ folder.
REM
REM Requirements:
REM   - WDK 7.1 installed at C:\WinDDK\7600.16385.1
REM   - Run from an ordinary (non-admin) command prompt is fine.
REM
REM Usage:  build-release.bat
REM ============================================================
setlocal
set WDK=C:\WinDDK\7600.16385.1
set ROOT=%~dp0
set DRV=%ROOT%driver
set OUT=%ROOT%release

if not exist "%WDK%\bin\setenv.bat" (
  echo ERROR: WDK 7.1 not found at %WDK%
  exit /b 1
)

echo === Building MirrorDisp (display driver) ===
call "%WDK%\bin\setenv.bat" %WDK% chk WXP
cd /d "%DRV%\MirrorDisp"
build -n
if errorlevel 1 goto :fail

echo === Building MirrorMini (miniport) ===
cd /d "%DRV%\MirrorMini"
build -n
if errorlevel 1 goto :fail

echo === Packaging release ===
if not exist "%OUT%" mkdir "%OUT%"
copy /Y "%DRV%\MirrorDisp\objchk_wxp_x86\i386\mirror.dll"     "%OUT%\" >nul
copy /Y "%DRV%\MirrorMini\objchk_wxp_x86\i386\mirror_m.sys"   "%OUT%\" >nul
copy /Y "%DRV%\inf\MirrorDriver.inf"                          "%OUT%\" >nul
copy /Y "%DRV%\install.bat"                                   "%OUT%\" >nul

echo.
echo === Release packaged to: %OUT% ===
dir "%OUT%"
goto :end

:fail
echo.
echo ERROR: build failed. See messages above.
exit /b 1

:end
endlocal
