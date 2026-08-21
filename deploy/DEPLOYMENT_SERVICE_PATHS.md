# Auth Login deployment service/process paths

This is the production deployment contract for the independent Auth Login app.

## Runtime placement

Auth Login is a Security child app. External clients must not call it directly.

```text
Client -> Gateway -> Security -> Auth Login
```

Deploy it on the API pool nodes:

```text
api-1 192.168.2.25
api-2 192.168.2.38
api-3 192.168.2.65
```

The app listens on HTTP only:

```text
ASPNETCORE_URLS=http://0.0.0.0:8080
```

## Production layout

Use the system service layout under `/srv`:

```text
/srv/xanhnow/apps/auth-login/releases/<release-id>
/srv/xanhnow/apps/auth-login/current -> releases/<release-id>
/srv/xanhnow/apps/auth-login/previous -> releases/<previous-release-id>
```

The published API DLL is:

```text
XanhNow.Auth.Login.Api.dll
```

The production systemd unit template is:

```text
deploy/xanhnow-auth-login/systemd/xanhnow-auth-login.service
```

Install it as:

```bash
sudo cp deploy/xanhnow-auth-login/systemd/xanhnow-auth-login.service /etc/systemd/system/xanhnow-auth-login.service
sudo systemctl daemon-reload
sudo systemctl enable --now xanhnow-auth-login
```

## Runtime environment

Runtime must use the s101 Vault AppRole:

```text
Vault__BasePath=xanhnow/s101/auth-login
Vault__RoleIdFile=/etc/xanhnow/s101/auth-login/vault/role_id
Vault__SecretIdFile=/etc/xanhnow/s101/auth-login/vault/secret_id
Vault__CaCertFile=/etc/xanhnow/s101/auth-login/trust/vault-ca.crt
Infrastructure__Mode=RedisVault
```

Required local files:

```text
/etc/xanhnow/s101/auth-login/vault/role_id
/etc/xanhnow/s101/auth-login/vault/secret_id
/etc/xanhnow/s101/auth-login/trust/vault-ca.crt
```

## Vault paths

Runtime AppRole:

```text
s101-xanhnow-auth-login-runtime-prod
```

Runtime policy:

```text
deploy/vault/s101-xanhnow-auth-login-runtime-prod.hcl
```

Required runtime secrets:

```text
kv/xanhnow/s101/auth-login/postgres/runtime
kv/xanhnow/s101/auth-login/redis
kv/xanhnow/s101/auth-login/password-hashing
```

Migration AppRole:

```text
s101-xanhnow-auth-login-migrator-prod
```

Migration policy:

```text
deploy/vault/s101-xanhnow-auth-login-migrator-prod.hcl
```

Required migration secret:

```text
kv/xanhnow/s101/auth-login/postgres/migration
```

## PostgreSQL roles

Auth Login owns its own PostgreSQL identities even when sharing database `authtest`.

```text
xanhnow_auth_migrator
xanhnow_auth
```

Use:

```powershell
powershell -ExecutionPolicy Bypass -File C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App\deploy\postgresql\provision-auth-login-postgres.ps1
```

The API runtime must not run migrations. Run migrations separately with the migrator identity:

```powershell
powershell -ExecutionPolicy Bypass -File C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App\deploy\postgresql\apply-auth-login-migration.ps1
```

## Deploy

Build and deploy on an API node:

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
sudo bash deploy/xanhnow-auth-login/deploy-api-node.sh "$release_dir"
bash deploy/xanhnow-auth-login/healthcheck.sh
```

Rollback:

```bash
sudo bash deploy/xanhnow-auth-login/rollback-api-node.sh
```

## Endpoints

Health:

```text
GET /health/live
GET /health/ready
GET /api/health/live
GET /api/health/ready
GET /api/edge-probe
```

Security-internal account lookup:

```text
GET /internal/v1/accounts/{userId}/status
GET /internal/v1/accounts/by-phone/status?phoneNumber=...
POST /internal/v1/accounts/{userId}/state
```

Auth API endpoints remain app-local and are intended to be called through Security/Gateway:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/session
```

## Deploy-ready gate

Login is production-ready when all are true:

```text
dotnet build -c Release: pass
dotnet test -c Release: pass
Vault runtime AppRole can read s101 runtime secrets
Vault migrator AppRole can read s101 migration secret
PostgreSQL runtime/migrator roles verified
migrator privilege verification: pass
/health/ready on every API node: HTTP 200
/api/edge-probe on every API node: HTTP 200
Security -> Auth Login internal lookup: HTTP 200 for an existing user
Admin -> Security -> Auth Login recovery lookup: HTTP 200 for an existing user
```
