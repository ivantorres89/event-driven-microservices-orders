param(
  [switch]$Volumes
)

# Compose file lives at the repository root (two levels up from /infra/local)
$composeFile = Join-Path $PSScriptRoot "..\..\docker-compose.yml"

# Default: docker compose down (keeps named volumes).
# Use -Volumes to also remove named volumes (RabbitMQ/Redis, etc.)
$cmd = "docker compose -f `"$composeFile`" down"
if ($Volumes) { $cmd += " -v" }

Write-Host "Running: $cmd"
Invoke-Expression $cmd
