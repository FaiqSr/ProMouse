@echo off
cd /d "%~dp0"
"%~dp0bin\Release\net10.0-windows\win-x64\publish\ProMouse.exe"
set "APP_EXIT=%errorlevel%"
echo.
echo ProMouse keluar dengan kode %APP_EXIT%. Salin teks error di atas.
pause
