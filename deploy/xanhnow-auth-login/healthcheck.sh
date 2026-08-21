#!/usr/bin/env bash
set -Eeuo pipefail

base_url="${1:-http://127.0.0.1:8080}"

curl -fsS "$base_url/health/live" >/dev/null
curl -fsS "$base_url/health/ready" >/dev/null
curl -fsS "$base_url/api/edge-probe" >/dev/null

echo "Auth Login healthcheck passed: $base_url"
