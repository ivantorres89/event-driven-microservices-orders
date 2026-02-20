#!/usr/bin/env bash
set -euo pipefail

if [ ! -d "backstage" ]; then
  ./scripts/backstage-init.sh
fi

cd backstage

yarn install
yarn dev
