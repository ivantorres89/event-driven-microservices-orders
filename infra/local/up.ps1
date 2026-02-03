param(
  [switch]$Build
)

# Compose file lives at the repository root (two levels up from /infra/local)
$composeFile = Join-Path $PSScriptRoot "..\..\docker-compose.yml"

$cmd = "docker compose -f `"$composeFile`" up -d"
if ($Build) { $cmd += " --build" }

Write-Host "Running: $cmd"
Invoke-Expression $cmd

Write-Host ""
Write-Host "OrderAccept Swagger: http://localhost:8081/swagger"
Write-Host "RabbitMQ UI:        http://localhost:15672 (guest/guest by default)"
Write-Host "Jaeger UI:          http://localhost:16686"
Write-Host "Frontend SPA:       http://localhost:4200"

# Best-effort open browser for SPA
Start-Sleep -Seconds 2
Start-Process "http://localhost:4200"
