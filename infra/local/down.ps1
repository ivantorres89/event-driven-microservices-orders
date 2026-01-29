$composeFile = Join-Path $PSScriptRoot "docker-compose.yml"
$cmd = "docker compose -f `"$composeFile`" down -v"
Write-Host "Running: $cmd"
Invoke-Expression $cmd
