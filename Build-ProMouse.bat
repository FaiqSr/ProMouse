@echo off
setlocal
cd /d "%~dp0"
set "DOTNET=%LOCALAPPDATA%\hermes\cache\scratch\dotnet-sdk\dotnet.exe"

if not exist "%DOTNET%" (
    echo .NET SDK tidak ditemukan: "%DOTNET%"
    pause
    exit /b 1
)

"%DOTNET%" publish ProMouse.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishDir="%~dp0bin\Release\net10.0-windows\win-x64\publish-new\"
if errorlevel 1 (
    echo Build gagal.
    pause
    exit /b 1
)

echo.
echo Build berhasil: bin\Release\net10.0-windows\win-x64\publish-new\ProMouse.exe
pause
