#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"
ENV_FILE="$SCRIPT_DIR/.env"

ARGS=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE" down)

# Optional: --volumes to delete named volumes
if [[ "${1:-}" == "--volumes" || "${1:-}" == "-v" ]]; then
  ARGS+=(--volumes)
fi

echo "Running: docker compose ${ARGS[*]}"
docker compose "${ARGS[@]}"
