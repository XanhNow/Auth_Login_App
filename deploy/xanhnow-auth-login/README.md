# XanhNow Auth Login API deploy

Production service name:

```text
xanhnow-auth-login
```

Production layout:

```text
/srv/xanhnow/apps/auth-login/releases/<release>
/srv/xanhnow/apps/auth-login/current
/srv/xanhnow/apps/auth-login/previous
```

Required runtime Vault Agent AppRole files:

```text
/etc/xanhnow/s101/auth-login/vault/role_id
/etc/xanhnow/s101/auth-login/vault/secret_id
/etc/xanhnow/s101/auth-login/trust/vault-ca.crt
```

Vault Agent renders runtime secrets here:

```text
/srv/xanhnow/s101/secrets/auth-login/postgres-connection-string
/srv/xanhnow/s101/secrets/auth-login/redis-password
/srv/xanhnow/s101/secrets/auth-login/redis-tls-enabled
/srv/xanhnow/s101/secrets/auth-login/password-pepper
/srv/xanhnow/s101/secrets/auth-login/password-pepper-version
/srv/xanhnow/s101/secrets/auth-login/password-algorithm
```

Build on an API node:

```bash
cd /srv/xanhnow/src/Auth_Login_App
git fetch origin
git reset --hard origin/main
release_dir=/tmp/auth-login-$(git rev-parse --short HEAD)-$(date -u +%Y%m%d%H%M%S)
rm -rf "$release_dir"
mkdir -p "$release_dir/publish/api"
/home/xanhnow/.dotnet-10.0.400/dotnet publish src/XanhNow.Auth.Login.Api/XanhNow.Auth.Login.Api.csproj -c Release -o "$release_dir/publish/api"
printf '{"app":"Auth_Login_App","commit":"%s"}\n' "$(git rev-parse HEAD)" > "$release_dir/release.json"
sudo cp deploy/xanhnow-auth-login/systemd/xanhnow-auth-login.service /etc/systemd/system/xanhnow-auth-login.service
sudo bash deploy/xanhnow-auth-login/install-vault-agent-node.sh
sudo bash deploy/xanhnow-auth-login/deploy-api-node.sh "$release_dir"
bash deploy/xanhnow-auth-login/healthcheck.sh
```
