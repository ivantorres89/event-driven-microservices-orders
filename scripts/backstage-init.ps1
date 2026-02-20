Param()

$ErrorActionPreference = "Stop"

$appDir = "backstage"
$configDir = "developer-portal"

if (Test-Path $appDir) {
  Write-Host "[backstage-init] '$appDir' already exists. Skipping scaffold."
} else {
  Write-Host "[backstage-init] Creating Backstage app in .\$appDir"
  Write-Host "  If the wizard asks:"
  Write-Host "   - App name: backstage"
  Write-Host "   - Database: sqlite (local dev)"
  npx @backstage/create-app@latest --path ".\$appDir"
}

Write-Host "[backstage-init] Applying Contoso portfolio config"
Copy-Item "$configDir\app-config.contoso.yaml" "$appDir\app-config.yaml" -Force
Copy-Item "$configDir\app-config.production.contoso.yaml" "$appDir\app-config.production.yaml" -Force

Write-Host "`n[backstage-init] Done."
Write-Host "Next:"
Write-Host "  cd $appDir; yarn install; yarn dev"
