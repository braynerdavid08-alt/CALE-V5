# Backup SQLite de desarrollo / piloto local (CALE v5)
# Uso:
#   powershell -File tools/backup-sqlite.ps1
#   powershell -File tools/backup-sqlite.ps1 -DbPath ".\cale-dev.db" -OutDir ".\backups"
#
# Restaurar:
#   1. Detener la API
#   2. Copiar el archivo .db (y -shm/-wal si existen) sobre cale-dev.db
#   3. Reiniciar la API
#
# RPO sugerido piloto local: 24h (backup diario) o antes de demos.
# RTO sugerido: < 15 min (copia de archivo + reinicio).

param(
    [string]$DbPath = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $DbPath) {
    $candidates = @(
        (Join-Path $root "cale-dev.db"),
        (Join-Path $root "src\Cale.Api\cale-dev.db"),
        (Join-Path (Get-Location) "cale-dev.db")
    )
    $DbPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $DbPath -or -not (Test-Path $DbPath)) {
    Write-Error "No se encontró SQLite. Pasa -DbPath con la ruta a cale-dev.db"
}

if (-not $OutDir) {
    $OutDir = Join-Path $root "backups"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$base = [IO.Path]::GetFileNameWithoutExtension($DbPath)
$dest = Join-Path $OutDir "$base-$stamp.db"
Copy-Item -LiteralPath $DbPath -Destination $dest -Force

foreach ($suffix in @("-wal", "-shm")) {
    $side = "$DbPath$suffix"
    if (Test-Path $side) {
        Copy-Item -LiteralPath $side -Destination "$dest$suffix" -Force
    }
}

Write-Host "Backup OK: $dest"
Get-Item $dest | Format-List FullName, Length, LastWriteTime
