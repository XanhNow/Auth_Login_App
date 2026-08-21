#!/usr/bin/env bash
set -Eeuo pipefail

app_root="${1:-/srv/xanhnow/apps/auth-login}"
service_name="${2:-xanhnow-auth-login}"

if [ ! -L "$app_root/previous" ]; then
  echo "FAIL: previous release symlink not found: $app_root/previous" >&2
  exit 1
fi

previous="$(readlink -f "$app_root/previous")"
current=""
if [ -L "$app_root/current" ]; then
  current="$(readlink -f "$app_root/current" || true)"
fi

if [ ! -d "$previous" ]; then
  echo "FAIL: previous release target does not exist: $previous" >&2
  exit 1
fi

ln -sfn "$previous" "$app_root/current"
if [ -n "$current" ] && [ -d "$current" ]; then
  ln -sfn "$current" "$app_root/previous"
fi

systemctl daemon-reload
systemctl restart "$service_name"
systemctl is-active --quiet "$service_name"
