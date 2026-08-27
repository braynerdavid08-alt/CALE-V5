@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

echo === Mi CALE · publish web (SPA + API) ===

where node >nul 2>&1
if errorlevel 1 (
  echo ERROR: Node.js no esta en PATH.
  exit /b 1
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET SDK no esta en PATH.
  exit /b 1
)

echo [1/3] Build Angular production...
pushd frontend
call npx ng build --configuration=production
if errorlevel 1 (
  popd
  echo ERROR: fallo el build del frontend.
  exit /b 1
)
popd

set "BROWSER=frontend\dist\frontend\browser"
if not exist "%BROWSER%\index.html" (
  echo ERROR: no se encontro %BROWSER%\index.html
  exit /b 1
)

echo [2/3] Copiar SPA a Cale.Api\wwwroot ...
if not exist "src\Cale.Api\wwwroot" mkdir "src\Cale.Api\wwwroot"
robocopy "%BROWSER%" "src\Cale.Api\wwwroot" /E /NFL /NDL /NJH /NJS /nc /ns /np >nul
if errorlevel 8 (
  echo ERROR: robocopy fallo.
  exit /b 1
)
if not exist "src\Cale.Api\wwwroot\uploads" mkdir "src\Cale.Api\wwwroot\uploads"

echo [3/3] Publicar API...
dotnet publish "src\Cale.Api\Cale.Api.csproj" -c Release -o "publish\web"
if errorlevel 1 (
  echo ERROR: fallo dotnet publish.
  exit /b 1
)

echo.
echo Listo. Ejecutar:
echo   set ASPNETCORE_ENVIRONMENT=Production
echo   set ASPNETCORE_URLS=http://0.0.0.0:8080
echo   set ConnectionStrings__Cale=Data Source=publish\web\cale-prod.db
echo   set Jwt__Key=CAMBIA-ESTA-CLAVE-LARGA-32-CHARS-MIN
echo   set Seed__DemoUsers=true
echo   publish\web\Cale.Api.exe
echo.
echo Luego abre http://127.0.0.1:8080  (o la IP de tu PC desde el celular)
echo Ver docs\DEPLOY.md
endlocal
