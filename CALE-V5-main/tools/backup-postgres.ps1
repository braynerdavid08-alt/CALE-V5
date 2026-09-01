# Backup PostgreSQL (Render / local). Requires pg_dump in PATH.
param(
    [string]$DatabaseUrl = $env:DATABASE_URL,
    [string]$OutputDir = ".\backups"
)

if ([string]::IsNullOrWhiteSpace($DatabaseUrl)) {
    Write-Error "Set DATABASE_URL or pass -DatabaseUrl."
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outFile = Join-Path $OutputDir "cale-$stamp.sql"

Write-Host "Writing $outFile"
& pg_dump $DatabaseUrl --no-owner --no-acl -f $outFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "pg_dump failed."
    exit $LASTEXITCODE
}

Write-Host "Done. Presentation media is stored in PostgreSQL (PresentationMediaBlobs)."
