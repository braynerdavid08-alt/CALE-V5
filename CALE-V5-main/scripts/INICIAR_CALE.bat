@echo off
setlocal
cd /d "%~dp0.."
echo Liberando puertos 5000, 4200 y Cale.Api.exe...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0free-ports.ps1"
timeout /t 2 /nobreak >nul
set "DOTNET_ROOT=%USERPROFILE%\.dotnet-sdk"
if exist "%DOTNET_ROOT%\dotnet.exe" set "PATH=%DOTNET_ROOT%;%PATH%"
set ASPNETCORE_ENVIRONMENT=Development
start "CALE API" cmd /k "cd /d "%CD%\src\Cale.Api" && set PATH=%PATH% && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --urls http://127.0.0.1:5000;http://[::1]:5000"
start "CALE UI" cmd /k "cd /d "%CD%\frontend" && npx ng serve --port 4200 --host localhost"
echo.
echo CALE v5 iniciado
echo   API  http://localhost:5000
echo   UI   http://localhost:4200
echo   (Development: usuarios demo solo si Seed esta activo en el servidor)
endlocal
