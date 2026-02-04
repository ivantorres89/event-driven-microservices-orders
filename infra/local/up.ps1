param(
  [switch]$Build
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$composeFile = Join-Path $repoRoot "docker-compose.yml"

# Compose env file (source of truth for local ports/certs)
$envFile = Join-Path $PSScriptRoot ".env"
$envExample = Join-Path $PSScriptRoot ".env.example"

if (!(Test-Path $envFile)) {
  Copy-Item $envExample $envFile
  Write-Warning "Created $envFile from .env.example. Edit it if you need different ports."
}

# Load .env into current process (so ensure-devcert.ps1 can read HTTPS_CERT_PASSWORD)
Get-Content $envFile | ForEach-Object {
  $line = $_.Trim()
  if (!$line -or $line.StartsWith("#")) { return }
  $parts = $line.Split("=", 2)
  if ($parts.Length -ne 2) { return }
  [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim(), "Process")
}

# Ensure we have an HTTPS dev cert exported for Docker/Kestrel
& (Join-Path $PSScriptRoot "ensure-devcert.ps1")

$cmd = "docker compose --env-file `"$envFile`" -f `"$composeFile`" up -d"
if ($Build) { $cmd += " --build" }

Write-Host "Running: $cmd"
Invoke-Expression $cmd

Write-Host ""
Write-Host "Frontend SPA:            http://localhost:4200"
Write-Host "Order Notification HTTP: http://localhost:$($env:ORDER_NOTIFICATION_HTTP_PORT)/healthz"
Write-Host "Order Notification HTTPS:https://localhost:$($env:ORDER_NOTIFICATION_HTTPS_PORT)/healthz"
Write-Host "RabbitMQ UI:             http://localhost:15672 (guest/guest by default)"
Write-Host "Jaeger UI:               http://localhost:16686"

# Best-effort open browser for SPA
Start-Sleep -Seconds 2
Start-Process "http://localhost:4200"
