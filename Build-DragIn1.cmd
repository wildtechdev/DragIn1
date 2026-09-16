@echo off
setlocal
cd /d "%~dp0"

set CSC=
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not defined CSC (
  echo Could not find the C# compiler that ships with Windows.
  echo Looked in %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\
  pause
  exit /b 1
)

echo Compiling DragIn1.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:DragIn1.ico /out:DragIn1.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  DragIn1.cs

if errorlevel 1 (
  echo.
  echo BUILD FAILED - copy the errors above and send them back.
  pause
  exit /b 1
)

echo.
echo Built: %~dp0DragIn1.exe
echo Starting it now.
start "" "%~dp0DragIn1.exe"
