@echo off
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0free-ports.ps1"
cd /d "%~dp0..\src\Cale.Api"
dotnet run --urls "http://127.0.0.1:5000;http://[::1]:5000"
