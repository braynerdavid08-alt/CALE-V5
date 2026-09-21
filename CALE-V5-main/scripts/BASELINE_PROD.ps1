# Baseline EF migrations on production Postgres (Render)
#
# Usage:
#   $env:CALE_DATABASE_URL = "postgresql://USER:PASSWORD@HOST:5432/DBNAME"
#   .\scripts\BASELINE_PROD.ps1
#
# Or pass -DatabaseUrl explicitly (prefer External URL from Render → PostgreSQL → Connect).
# Requires: psql on PATH, OR Docker Desktop.

param(
    [string]$DatabaseUrl = $env:CALE_DATABASE_URL,
    [string]$SqlFile = (Join-Path $PSScriptRoot "baseline-ef-migrations.sql"),
    [switch]$SkipBackupReminder
)

$ErrorActionPreference = "Stop"

if (-not $DatabaseUrl) {
    Write-Host @"
Missing database URL.

1. Render Dashboard → PostgreSQL (linked to MICALE) → Connect
2. Copy External Database URL
3. Run:

   `$env:CALE_DATABASE_URL = 'postgresql://...'
   .\scripts\BASELINE_PROD.ps1

Or paste the SQL from scripts\baseline-ef-migrations.sql into Render → Shell / PSQL.
"@
    exit 1
}

if (-not (Test-Path $SqlFile)) {
    throw "SQL file not found: $SqlFile"
}

if (-not $SkipBackupReminder) {
    Write-Host "Take a Postgres backup in Render before continuing (Dashboard → PostgreSQL → ...)."
    Write-Host "Press Enter to run the baseline SQL, or Ctrl+C to abort."
    [void][System.Console]::ReadLine()
}

$psql = Get-Command psql -ErrorAction SilentlyContinue
if ($psql) {
    Write-Host "Running baseline via local psql..."
    & psql $DatabaseUrl -v ON_ERROR_STOP=1 -f $SqlFile
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit $LASTEXITCODE" }
}
else {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        throw "Neither psql nor docker found. Install one, or run the SQL in Render's PSQL console."
    }
    Write-Host "Running baseline via docker postgres:16 client..."
    $sqlText = Get-Content -Raw -Path $SqlFile
    $sqlText | & docker run --rm -i postgres:16-alpine psql $DatabaseUrl -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "docker psql failed with exit $LASTEXITCODE" }
}

Write-Host @"

Baseline applied.

Next in Render → Web Service MICALE → Environment:
  Database__UseEfMigrations=true
  Database__AllowEnsureCreated=false
  Database__ApplyFeatureSchema=false

Then Manual Deploy → Clear build cache & deploy.
Verify: https://micale.onrender.com/api/health
"@
