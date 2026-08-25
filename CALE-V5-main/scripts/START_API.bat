@echo off
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0free-ports.ps1"
cd /d "%~dp0..\src\Cale.Api"
set "DOTNET_ROOT=%USERPROFILE%\.dotnet-sdk"
if exist "%DOTNET_ROOT%\dotnet.exe" set "PATH=%DOTNET_ROOT%;%PATH%"
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --urls "http://127.0.0.1:5000;http://[::1]:5000"
