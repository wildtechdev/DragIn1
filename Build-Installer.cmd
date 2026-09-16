@echo off
setlocal
cd /d "%~dp0"

set CSC=
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not defined CSC (
  echo Could not find the C# compiler that ships with Windows.
  pause
  exit /b 1
)

echo [1/2] Compiling DragIn1.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:DragIn1.ico /out:DragIn1.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  DragIn1.cs
if errorlevel 1 goto :failed

echo [2/2] Compiling DragIn1-Setup.exe (with DragIn1.exe embedded) ...
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:DragIn1.ico /out:DragIn1-Setup.exe ^
  /resource:DragIn1.exe,DragIn1.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  Setup.cs
if errorlevel 1 goto :failed

echo.
echo Done.
echo   %~dp0DragIn1-Setup.exe
echo.
echo This single file is the whole installer. Send it to anyone.
echo They double-click it, click Install, and they are finished.
echo.
pause
exit /b 0

:failed
echo.
echo BUILD FAILED - copy the errors above and send them back.
pause
exit /b 1
