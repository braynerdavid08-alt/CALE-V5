$ports = 5000, 4200
foreach ($port in $ports) {
    Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue |
        ForEach-Object {
            Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
        }
}

Get-Process Cale.Api -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
