#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"

ENV_FILE="$SCRIPT_DIR/.env"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"

if [[ ! -f "$ENV_FILE" ]]; then
  cp "$ENV_EXAMPLE" "$ENV_FILE"
  echo "Created $ENV_FILE from .env.example. Edit it if you need different ports/passwords." >&2
fi

# shellcheck disable=SC1090
set -a
source "$ENV_FILE"
set +a

# Export dotnet dev cert to infra/local/certs (used by order-notification container)
"$SCRIPT_DIR/ensure-devcert.sh"

ARGS=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d)
if [[ "${1:-}" == "--build" || "${1:-}" == "-b" ]]; then
  ARGS+=(--build)
fi

echo "Running: docker compose ${ARGS[*]}"
docker compose "${ARGS[@]}"

echo ""
echo "Frontend SPA:              http://localhost:4200"
echo "Order Notification HTTP:   http://localhost:${ORDER_NOTIFICATION_HTTP_PORT:-5006}/healthz"
echo "Order Notification HTTPS:  https://localhost:${ORDER_NOTIFICATION_HTTPS_PORT:-5007}/healthz"
echo "RabbitMQ UI:               http://localhost:15672 (guest/guest by default)"
echo "Jaeger UI:                 http://localhost:16686"
