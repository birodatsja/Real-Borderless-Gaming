@echo off
rem Builds Real Borderless Gaming with the MSBuild that ships with Windows (no Visual Studio needed),
rem then collects everything the app needs into the folder "Real Borderless Gaming" next to this file.
setlocal
rem Files extracted from a downloaded zip carry the "mark of the web", which makes MSBuild reject .resx files.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0.' -Recurse -File | Unblock-File"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\MSBuild.exe" set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%FW%\MSBuild.exe" "%~dp0SRWE\SRWE.csproj" /p:Configuration=Release /p:Platform=AnyCPU /p:FrameworkPathOverride="%FW%" /v:m /nologo
if errorlevel 1 ( echo. & echo BUILD FAILED & pause & exit /b 1 )

set "BIN=%~dp0SRWE\bin\Release"
set "OUT=%~dp0Real Borderless Gaming"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"
copy /y "%BIN%\RealBorderlessGaming.exe" "%OUT%\" >nul
if errorlevel 1 ( echo. & echo Could not find the built RealBorderlessGaming.exe & pause & exit /b 1 )
if exist "%BIN%\RealBorderlessGaming.exe.config" copy /y "%BIN%\RealBorderlessGaming.exe.config" "%OUT%\" >nul
if exist "%~dp0Profiles" xcopy "%~dp0Profiles" "%OUT%\Profiles\" /e /i /y /q >nul
copy /y "%~dp0LICENSE" "%OUT%\LICENSE.txt" >nul

echo.
echo ============================================================
echo  Done. The finished app is in this folder:
echo    %OUT%
echo.
echo  Copy that folder to any Windows PC and run
echo  RealBorderlessGaming.exe. Everything else (source code,
echo  the SRWE folder, build.bat) can be deleted.
echo ============================================================
explorer "%OUT%"
pause
