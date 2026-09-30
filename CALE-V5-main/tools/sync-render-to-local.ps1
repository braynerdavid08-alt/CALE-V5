# Copy the production (Render) PostgreSQL database into the local PostgreSQL server.
# 1. pg_dump Render -> backups\micale-<stamp>.dump (kept on disk as a file backup)
# 2. (Re)create the local database and pg_restore the dump into it.
# Secrets are read from env vars or prompted; nothing is written to disk except the dump.
# -DumpFile restores an existing dump without contacting Render.
# -UpdateAppSettings points src\Cale.Api\appsettings.Development.local.json at the local database
#   (the previous file is kept as appsettings.Development.local.json.bak).
param(
    [string]$DumpFile,
    [switch]$UpdateAppSettings,
    [string]$RenderUrl = $env:RENDER_DATABASE_URL,
    [string]$LocalHost = "localhost",
    [int]$LocalPort = 5432,
    [string]$LocalUser = "postgres",
    [string]$LocalDatabase = "micale_local",
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin",
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\backups")
)

$ErrorActionPreference = "Stop"
$pgDump = Join-Path $PgBin "pg_dump.exe"
$pgRestore = Join-Path $PgBin "pg_restore.exe"
$psql = Join-Path $PgBin "psql.exe"

foreach ($exe in $pgDump, $pgRestore, $psql) {
    if (-not (Test-Path $exe)) { throw "No se encontró $exe. Ajusta -PgBin." }
}

function Read-Secret([string]$prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

if ($DumpFile) {
    if (-not (Test-Path $DumpFile)) { throw "No existe el archivo $DumpFile." }
    $dumpFile = (Resolve-Path $DumpFile).Path
}
else {
    if ([string]::IsNullOrWhiteSpace($RenderUrl)) {
        $RenderUrl = Read-Secret "Pega la External Database URL de Render (postgresql://...)"
    }
    if ($RenderUrl -notmatch '^postgres(ql)?://') { throw "La URL de Render debe empezar por postgresql://" }
    if ($RenderUrl -notmatch 'sslmode=') {
        $RenderUrl += $(if ($RenderUrl.Contains('?')) { '&' } else { '?' }) + 'sslmode=require'
    }
}

$localPassword = $env:LOCAL_PGPASSWORD
if ([string]::IsNullOrWhiteSpace($localPassword)) {
    $localPassword = Read-Secret "Contraseña del usuario '$LocalUser' de tu PostgreSQL local"
}

if ($DumpFile) {
    Write-Host "1/3 Usando la copia existente $dumpFile"
}
else {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $dumpFile = Join-Path (Resolve-Path $OutputDir) "micale-$stamp.dump"

    Write-Host "1/3 Descargando la base de Render a $dumpFile ..."
    & $pgDump --format=custom --no-owner --no-acl --file=$dumpFile $RenderUrl
    if ($LASTEXITCODE -ne 0) { throw "pg_dump falló (código $LASTEXITCODE)." }
    $sizeMb = [Math]::Round((Get-Item $dumpFile).Length / 1MB, 2)
    Write-Host "    Copia guardada ($sizeMb MB)."
}

$env:PGPASSWORD = $localPassword
try {
    Write-Host "2/3 Preparando la base local '$LocalDatabase' ..."
    $exists = & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$LocalDatabase'"
    if ($LASTEXITCODE -ne 0) { throw "No se pudo entrar al PostgreSQL local. Revisa la contraseña." }
    if ($exists -eq "1") {
        & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$LocalDatabase' AND pid <> pg_backend_pid();" | Out-Null
        & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE `"$LocalDatabase`";" | Out-Null
    }
    & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE `"$LocalDatabase`" ENCODING 'UTF8' TEMPLATE template0;" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear la base local." }

    Write-Host "3/3 Restaurando la copia en '$LocalDatabase' ..."
    & $pgRestore -h $LocalHost -p $LocalPort -U $LocalUser -d $LocalDatabase --no-owner --no-acl $dumpFile
    if ($LASTEXITCODE -ne 0) { Write-Warning "pg_restore terminó con advertencias (código $LASTEXITCODE). Revisa el conteo abajo." }

    & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d $LocalDatabase -c "ANALYZE;" | Out-Null
    $counts = & $psql -h $LocalHost -p $LocalPort -U $LocalUser -d $LocalDatabase -tA -F " | " -c "SELECT relname, n_live_tup FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 15;"
    Write-Host ""
    Write-Host "Listo. Tablas con más filas en '$LocalDatabase':"
    $counts | ForEach-Object { Write-Host "  $_" }

    if ($UpdateAppSettings) {
        $settingsPath = Join-Path $PSScriptRoot "..\src\Cale.Api\appsettings.Development.local.json"
        if (Test-Path $settingsPath) {
            Copy-Item $settingsPath "$settingsPath.bak" -Force
            $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        }
        else {
            $settings = [pscustomobject]@{}
        }
        if (-not $settings.PSObject.Properties["ConnectionStrings"]) {
            $settings | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
        }
        $local = "Host=$LocalHost;Port=$LocalPort;Database=$LocalDatabase;Username=$LocalUser;Password=$localPassword"
        $settings.ConnectionStrings | Add-Member -NotePropertyName Cale -NotePropertyValue $local -Force
        $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
        Write-Host ""
        Write-Host "Configuración local actualizada: la API en Development ahora usa '$LocalDatabase'."
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
