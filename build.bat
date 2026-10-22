@echo off
rem Builds dist\PrayerTray.exe using the C# compiler that ships with Windows
rem (.NET Framework 4.x). No Visual Studio or SDK needed.
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe from .NET Framework 4.
  exit /b 1
)

if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /out:dist\PrayerTray.exe ^
  /win32icon:assets\app.ico /win32manifest:src\app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll ^
  src\*.cs
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Built dist\PrayerTray.exe
