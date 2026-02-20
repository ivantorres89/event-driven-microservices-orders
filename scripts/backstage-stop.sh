#!/usr/bin/env bash
set -euo pipefail

# Default Backstage ports (frontend 3000, backend 7007)
PORTS=(3000 7007)

echo "Stopping Backstage (ports: ${PORTS[*]})..."

get_pids_by_port() {
  local port="$1"
  local pids=""

  if command -v lsof >/dev/null 2>&1; then
    pids="$(lsof -ti TCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  elif command -v fuser >/dev/null 2>&1; then
    pids="$(fuser -n tcp "$port" 2>/dev/null | tr -d ' ' || true)"
  elif command -v ss >/dev/null 2>&1; then
    pids="$(ss -lptn "sport = :$port" 2>/dev/null | awk -F'pid=' 'NF>1{print $2}' | awk -F',' '{print $1}' | sort -u || true)"
  else
    echo "No lsof/fuser/ss found. Install 'lsof' (recommended) or 'fuser' to stop by port."
    exit 1
  fi

  echo "$pids"
}

kill_pids_gracefully() {
  local pids="$1"
  [[ -z "$pids" ]] && return 0

  echo "Sending SIGTERM to PID(s): $pids"
  kill -TERM $pids 2>/dev/null || true

  for _ in {1..10}; do
    sleep 0.3
    local alive=""
    for pid in $pids; do
      if kill -0 "$pid" 2>/dev/null; then
        alive="$alive $pid"
      fi
    done
    [[ -z "$alive" ]] && return 0
  done

  echo "Still running. Sending SIGKILL to PID(s): $pids"
  kill -KILL $pids 2>/dev/null || true
}

ALL_PIDS=""

for port in "${PORTS[@]}"; do
  PIDS="$(get_pids_by_port "$port")"
  if [[ -n "$PIDS" ]]; then
    echo "Found listener(s) on port $port: $PIDS"
    ALL_PIDS="$ALL_PIDS $PIDS"
  else
    echo "No listener found on port $port."
  fi
done

# De-duplicate PIDs
ALL_PIDS="$(echo "$ALL_PIDS" | tr ' ' '\n' | awk 'NF' | sort -u | tr '\n' ' ')"

if [[ -z "$ALL_PIDS" ]]; then
  echo "Nothing to stop."
  exit 0
fi

kill_pids_gracefully "$ALL_PIDS"

echo "Backstage stopped (if it was running)."

