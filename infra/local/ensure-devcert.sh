#!/usr/bin/env bash
set -euo pipefail

CERT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/certs"
PFX_PATH="$CERT_DIR/contoso-devcert.pfx"
PASSWORD="${HTTPS_CERT_PASSWORD:-contoso}"

mkdir -p "$CERT_DIR"

if [[ -f "$PFX_PATH" ]]; then
  echo "HTTPS dev cert already present: $PFX_PATH"
  exit 0
fi

echo "Generating/Exporting HTTPS dev cert to: $PFX_PATH"

# 1) Try to trust dev cert (works on Windows/macOS; may fail on Linux)
if dotnet dev-certs https --trust; then
  true
else
  echo "WARN: dotnet dev-certs https --trust failed (expected on some Linux setups). Continuing..."
fi

# 2) Export cert for Docker/Kestrel
dotnet dev-certs https -ep "$PFX_PATH" -p "$PASSWORD"

echo ""
echo "OK. Exported dev cert for Docker:"
echo "  $PFX_PATH"
echo ""
echo "If your browser still complains about trust, run:"
echo "  dotnet dev-certs https --trust"
