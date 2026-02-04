param(
  [string]$Password
)

$ErrorActionPreference = "Stop"

$certDir = Join-Path $PSScriptRoot "certs"
if (!(Test-Path $certDir)) {
  New-Item -ItemType Directory -Path $certDir | Out-Null
}

$pfxPath = Join-Path $certDir "contoso-devcert.pfx"
if (Test-Path $pfxPath) {
  Write-Host "HTTPS dev cert already present: $pfxPath"
  exit 0
}

if (-not $Password) {
  $Password = $env:HTTPS_CERT_PASSWORD
}
if (-not $Password) {
  $Password = "contoso"
}

Write-Host "Generating/Exporting HTTPS dev cert to: $pfxPath"

# 1) Ensure a developer certificate exists and is trusted (Windows/macOS)
try {
  dotnet dev-certs https --trust | Out-Host
} catch {
  Write-Warning "dotnet dev-certs https --trust failed (this is expected on some Linux setups). Continuing..."
}

# 2) Export the dev cert to PFX (used by Docker containers)
dotnet dev-certs https -ep "$pfxPath" -p "$Password" | Out-Host

Write-Host ""
Write-Host "OK. Exported dev cert for Docker:"
Write-Host "  $pfxPath"
Write-Host ""
Write-Host "If your browser still complains about trust:"
Write-Host "  dotnet dev-certs https --trust"
