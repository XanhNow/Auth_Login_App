# Auth Login deployment service/process paths

This is the deployment contract for the Edge-facing Login API.

## Runtime placement

Auth Login is an HTTP API behind the Edge layer:

```text
Client/Postman -> Edge VIP 192.168.2.82 -> Nginx /api/ -> API pool 192.168.2.25/.38/.65:8080
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

Do not enable HTTPS redirection for this internal Edge integration stage. Public TLS/domain/VPS ingress belongs to a separate runbook.

## Release layout

Use the Edge API release layout under the `xanhnow` Linux user:

```text
/home/xanhnow/xanhnow-auth-login/
  config/runtime.env
  incoming/
  releases/
  current -> releases/<release-id>
  previous -> releases/<previous-release-id>
```

The published API DLL is:

```text
XanhNow.Auth.Login.Api.dll
```

The systemd user unit template is:

```text
deploy/systemd/xanhnow-auth-login.service
```

Install it as:

```bash
install -d -o xanhnow -g xanhnow -m 0750 /home/xanhnow/.config/systemd/user
install -o xanhnow -g xanhnow -m 0640 deploy/systemd/xanhnow-auth-login.service /home/xanhnow/.config/systemd/user/xanhnow-auth-login.service
loginctl enable-linger xanhnow
sudo -iu xanhnow systemctl --user daemon-reload
sudo -iu xanhnow systemctl --user enable --now xanhnow-auth-login.service
```

## Runtime environment

`/home/xanhnow/xanhnow-auth-login/config/runtime.env` must be root/xanhnow protected and must not be committed.

Required runtime variables:

```text
VAULT_ROLE_ID=<auth-login role id>
VAULT_SECRET_ID=<fresh auth-login secret id>
Infrastructure__Mode=Real
Vault__Address=https://192.168.2.81:8200
Http__EnableHttpsRedirection=false
```

Optional if the appsettings defaults remain valid:

```text
Vault__MountPath=kv
Vault__BasePath=xanhnow/s101/auth-login
```

## Edge-facing endpoints

The Login app supports both direct local health paths and Edge `/api/` paths:

```text
GET /health/live
GET /health/ready
GET /api/health/live
GET /api/health/ready
GET /api/edge-probe
```

Auth endpoints remain:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/session
```

The app trusts forwarded headers only from:

```text
edge-gw-1 192.168.2.24
edge-gw-2 192.168.2.64
```

## Migration

The API runtime must not run migrations.

Run the migrator separately with the migrator identity. The migrator requires `xanhnow_auth_migrator`, host `192.168.2.80`, port `15432`, database `authtest`.

Accepted ways to provide the migration connection string:

```text
ConnectionStrings__AuthMigrationDb
AUTH_LOGIN_MIGRATION_CONNECTION_STRING
MIGRATION_VAULT_ROLE_ID + MIGRATION_VAULT_SECRET_ID
```

Recommended verification before/after migration:

```bash
dotnet XanhNow.Auth.Login.Migrator.dll --verify-privileges
dotnet XanhNow.Auth.Login.Migrator.dll
dotnet XanhNow.Auth.Login.Migrator.dll --verify-privileges
```

## Vault paths

Runtime AppRole: `auth-login`

Expected KV root:

```text
kv/xanhnow/s101/auth-login
```

Required runtime secrets:

```text
kv/xanhnow/s101/auth-login/postgres/runtime
kv/xanhnow/s101/auth-login/redis
kv/xanhnow/s101/auth-login/password-hashing
```

Migration AppRole: `auth-login-migrator`

```text
kv/xanhnow/s101/auth-login/postgres/migration
```

## Deploy-ready gate

Login is deploy-ready when all are true:

```text
dotnet build -c Release: pass
dotnet test -c Release: pass
migrator privilege verification: pass
/api/health/ready through Edge VIP: HTTP 200
/api/edge-probe through Edge VIP: HTTP 200
register/login/logout/session smoke through Edge VIP: pass
Kafka outbox emits UserRegistered/UserLoggedIn/UserLoggedOut in Real mode
```
