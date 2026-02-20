Param()

$ErrorActionPreference = "Stop"

if (!(Test-Path "backstage")) {
  & .\scripts\backstage-init.ps1
}

Set-Location backstage
yarn install
yarn start
