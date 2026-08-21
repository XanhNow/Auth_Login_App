#!/usr/bin/env bash
set -Eeuo pipefail

release_dir="${1:?release directory is required}"
app_root="${2:-/srv/xanhnow/apps/auth-login}"
service_name="${3:-xanhnow-auth-login}"

if [ ! -f "$release_dir/release.json" ]; then
  echo "FAIL: release.json not found in $release_dir" >&2
  exit 1
fi

if [ ! -d "$release_dir/publish/api" ]; then
  echo "FAIL: publish/api not found in $release_dir" >&2
  exit 1
fi

install -d -o xanhnow -g xanhnow -m 0755 "$app_root/releases"
release_name="$(basename "$release_dir")"
target="$app_root/releases/$release_name"
previous=""

if [ -L "$app_root/current" ]; then
  previous="$(readlink -f "$app_root/current" || true)"
fi

rm -rf "$target"
cp -a "$release_dir/publish/api" "$target"
cp "$release_dir/release.json" "$target/release.json"
chown -R xanhnow:xanhnow "$target"
chmod -R u=rwX,g=rX,o= "$target"

if [ -n "$previous" ] && [ -d "$previous" ]; then
  ln -sfn "$previous" "$app_root/previous"
fi

ln -sfn "$target" "$app_root/current"
systemctl daemon-reload
systemctl restart "$service_name"
systemctl is-active --quiet "$service_name"
