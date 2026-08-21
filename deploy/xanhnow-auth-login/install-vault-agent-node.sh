#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

install -d -o xanhnow -g xanhnow -m 0750 /etc/xanhnow/s101/auth-login/vault
install -d -o xanhnow -g xanhnow -m 0750 /etc/xanhnow/s101/auth-login/trust
install -d -o xanhnow -g xanhnow -m 0750 /etc/xanhnow/s101/auth-login/templates
install -d -o xanhnow -g xanhnow -m 0700 /srv/xanhnow/s101/secrets/auth-login

install -o root -g root -m 0644 \
  "$repo_root/deploy/xanhnow-auth-login/vault-agent/vault-agent.hcl" \
  /etc/xanhnow/s101/auth-login/vault-agent.hcl

install -o root -g root -m 0644 \
  "$repo_root/deploy/xanhnow-auth-login/systemd/xanhnow-auth-login-vault-agent.service" \
  /etc/systemd/system/xanhnow-auth-login-vault-agent.service

install -o root -g root -m 0644 \
  "$repo_root/deploy/xanhnow-auth-login/systemd/xanhnow-auth-login.service" \
  /etc/systemd/system/xanhnow-auth-login.service

install -o xanhnow -g xanhnow -m 0640 \
  "$repo_root"/deploy/xanhnow-auth-login/vault-agent/templates/*.ctmpl \
  /etc/xanhnow/s101/auth-login/templates/

systemctl daemon-reload
systemctl enable --now xanhnow-auth-login-vault-agent.service
sleep 3
systemctl is-active --quiet xanhnow-auth-login-vault-agent.service

test -s /srv/xanhnow/s101/secrets/auth-login/postgres-connection-string
test -s /srv/xanhnow/s101/secrets/auth-login/redis-password
test -s /srv/xanhnow/s101/secrets/auth-login/password-pepper

echo "Auth Login Vault Agent installed and rendered runtime secrets."
