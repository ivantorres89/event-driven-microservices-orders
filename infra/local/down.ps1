param(
  [switch]$Volumes
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$composeFile = Join-Path $repoRoot "docker-compose.yml"

$envFile = Join-Path $PSScriptRoot ".env"
if (!(Test-Path $envFile)) {
  # Fall back to default envs if .env doesn't exist
  $cmd = "docker compose -f `"$composeFile`" down"
} else {
  $cmd = "docker compose --env-file `"$envFile`" -f `"$composeFile`" down"
}

if ($Volumes) { $cmd += " -v" }

Write-Host "Running: $cmd"
Invoke-Expression $cmd
