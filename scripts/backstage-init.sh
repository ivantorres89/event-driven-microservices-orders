#!/usr/bin/env bash
set -euo pipefail

APP_DIR="backstage"
CONFIG_DIR="developer-portal"

if [ -d "${APP_DIR}" ]; then
  echo "[backstage-init] '${APP_DIR}' already exists. Skipping scaffold."
else
  echo "[backstage-init] Creating Backstage app in ./${APP_DIR}"
  echo "  If the wizard asks:"
  echo "   - App name: backstage"
  echo "   - Database: sqlite (local dev)"
  echo
  npx @backstage/create-app@latest --path "./${APP_DIR}"
fi

echo "[backstage-init] Applying Contoso portfolio config"
cp "${CONFIG_DIR}/app-config.contoso.yaml" "${APP_DIR}/app-config.yaml"
cp "${CONFIG_DIR}/app-config.production.contoso.yaml" "${APP_DIR}/app-config.production.yaml"

echo
echo "[backstage-init] Done."
echo "Next:"
echo "  cd ${APP_DIR} && yarn install && yarn dev"
