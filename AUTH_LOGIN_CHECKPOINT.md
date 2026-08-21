# Auth Login Checkpoint

Date: 2026-08-21

## Current State

- Independent app root: `C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App`
- Solution: `XanhNow.Auth.Login.slnx`
- API project: `src\XanhNow.Auth.Login.Api`
- Migrator project: `src\XanhNow.Auth.Login.Migrator`
- Production service name: `xanhnow-auth-login`
- Production API node layout: `/srv/xanhnow/apps/auth-login`
- Runtime port: `8080`
- Auth Login is a Security child app. External clients must go through Gateway and Security.

## Architecture Boundary

Production request flow:

```text
Client -> Gateway -> Security -> Auth Login
```

No app outside Security should call Auth Login directly.

## PostgreSQL

- Database: `authtest`
- Schema: `auth`
- Runtime user: `xanhnow_auth`
- Migration user: `xanhnow_auth_migrator`
- Admin/migration endpoint: `192.168.2.80:15432`
- Runtime endpoint comes from Vault runtime secret.
- API runtime must not run migrations.
- Migration must run through migrator identity only.

Deploy scripts:

```text
deploy/postgresql/provision-auth-login-postgres.ps1
deploy/postgresql/apply-auth-login-migration.ps1
```

## Vault

Runtime AppRole:

```text
s101-xanhnow-auth-login-runtime-prod
```

Runtime policy:

```text
deploy/vault/s101-xanhnow-auth-login-runtime-prod.hcl
```

Runtime secret paths:

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

Migration secret path:

```text
kv/xanhnow/s101/auth-login/postgres/migration
```

Vault provisioning scripts:

```text
deploy/vault/provision-auth-login-vault.ps1
deploy/vault/write-auth-login-postgres-secrets.ps1
```

## Runtime Files On API Nodes

```text
/etc/xanhnow/s101/auth-login/vault/role_id
/etc/xanhnow/s101/auth-login/vault/secret_id
/etc/xanhnow/s101/auth-login/trust/vault-ca.crt
```

Migration AppRole files, if running migrator on Linux:

```text
/etc/xanhnow/s101/auth-login/migrator/role_id
/etc/xanhnow/s101/auth-login/migrator/secret_id
```

## Redis

- Redis is real, not in-memory.
- Runtime Redis secret is read from Vault at `kv/xanhnow/s101/auth-login/redis`.
- Login creates real Redis session.
- Logout invalidates real Redis session.

## Kafka

- Kafka/outbox publishing is not part of Auth Login runtime.
- Auth Login no longer requires a Kafka Vault secret.
- Official cross-app events must be published by XanhNow.Security, not by this child app.
- Existing historical `auth.outbox_events` schema is not dropped in this step because dropping tables is a separate DB migration decision.

## Deploy Assets

```text
deploy/xanhnow-auth-login/systemd/xanhnow-auth-login.service
deploy/xanhnow-auth-login/deploy-api-node.sh
deploy/xanhnow-auth-login/rollback-api-node.sh
deploy/xanhnow-auth-login/healthcheck.sh
deploy/xanhnow-auth-login/README.md
```

## Verified On 2026-08-21

Local verification:

```text
dotnet build XanhNow.Auth.Login.slnx -c Release: PASS
dotnet test XanhNow.Auth.Login.slnx -c Release --no-build: PASS, 3/3
```

api-1 verification before adding final deploy assets:

```text
xanhnow-auth-login active
GET 127.0.0.1:8080/internal/v1/accounts/by-phone/status?phoneNumber=%2B84979382975 -> 200
Security -> Auth Login recovery lookup -> 200
Admin -> Security -> Auth Login recovery lookup -> 200
```

## Production Acceptance Gate

Login is production-ready when all are true:

```text
Vault runtime AppRole can read s101 runtime secrets
Vault migrator AppRole can read s101 migration secret
PostgreSQL runtime/migrator roles verified
migrator privilege verification: pass
/health/ready on every API node: HTTP 200
/api/edge-probe on every API node: HTTP 200
Security -> Auth Login internal lookup: HTTP 200 for an existing user
Admin -> Security -> Auth Login recovery lookup: HTTP 200 for an existing user
```
