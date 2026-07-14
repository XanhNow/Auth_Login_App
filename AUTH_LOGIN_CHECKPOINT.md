# Auth Login Checkpoint

Date: 2026-07-10 23:11:32 +07:00

## Current State

- Independent root: `C:\XanhnowAuth\Auth_Login_App`
- App Login solution: `XanhNow.Auth.Login.slnx`
- API project: `src\XanhNow.Auth.Login.Api`
- Migrator project: `src\XanhNow.Auth.Login.Migrator`
- Current verified API URL: `http://localhost:5097`
- App Login is an independent service. Treat its database boundary as independent even if the lab currently shares physical PostgreSQL database `authtest`.
- PostgreSQL, Redis, Kafka, and password hashing secrets are managed through Vault for runtime.
- API runtime must not run database migration.
- Migration must run through migrator/admin identity only.

## Completed Architecture

- Clean Architecture projects:
  - `XanhNow.Auth.Login.Domain`
  - `XanhNow.Auth.Login.Application`
  - `XanhNow.Auth.Login.Infrastructure`
  - `XanhNow.Auth.Login.Api`
  - `XanhNow.Auth.Login.Migrator`
  - `XanhNow.Auth.Login.Application.Tests`
- Production source no longer uses in-memory infrastructure adapters.
- Infrastructure modes support real dependencies, including Vault-backed runtime configuration.
- API endpoints implemented:
  - `POST /api/auth/register`
  - `POST /api/auth/login`
  - `POST /api/auth/logout`
  - `GET /api/auth/session`
  - `GET /health/live`
  - `GET /health/ready`
- Clean JSON exception handler implemented, no stack trace leakage.
- Real readiness health check implemented for PostgreSQL, Redis, and Kafka based on infrastructure mode.
- Swagger/OpenAPI implemented in Development:
  - `/swagger`
  - `/swagger/v1/swagger.json`

## PostgreSQL

- Database: `authtest`
- Schema: `auth`
- Runtime user: `xanhnow_auth`
- Migration user: `xanhnow_auth_migrator`
- Admin endpoint for migration: `192.168.2.80:15432`
- Runtime endpoint comes from Vault runtime secret.
- Migration project refuses to run unless connection string targets:
  - Username: `xanhnow_auth_migrator`
  - Host: `192.168.2.80`
  - Port: `15432`
  - Database: `authtest`
- Initial migration does not create user or schema because both already exist.
- SQL Server-style `row_version` compatibility workaround was removed.
- `RemoveUserRowVersion` migration applied; PostgreSQL schema no longer depends on SQL Server rowversion behavior.
- Migrator applies runtime grants after migration.

## PostgreSQL Privilege Verification

Status: PASS on 2026-07-10.

Command used successfully:

```powershell
cd C:\XanhnowAuth\Auth_Login_App

$secure = Read-Host "Paste password xanhnow_auth_migrator" -AsSecureString
$password = [System.Net.NetworkCredential]::new("", $secure).Password

$env:AUTH_LOGIN_MIGRATION_CONNECTION_STRING="Host=192.168.2.80;Port=15432;Database=authtest;Username=xanhnow_auth_migrator;Password=$password;Pooling=true;Timeout=15;Command Timeout=60"

dotnet run --no-build --project src\XanhNow.Auth.Login.Migrator\XanhNow.Auth.Login.Migrator.csproj -- --verify-privileges

Remove-Variable secure -ErrorAction SilentlyContinue
Remove-Variable password -ErrorAction SilentlyContinue
Remove-Item Env:\AUTH_LOGIN_MIGRATION_CONNECTION_STRING -ErrorAction SilentlyContinue
```

Verified output:

```text
[PASS] runtime role exists and is not elevated
[PASS] runtime user has USAGE but not CREATE on schema auth
[PASS] runtime user is not member of migrator role
[PASS] runtime user has DML and no TRUNCATE/TRIGGER on auth tables
[PASS] runtime user is not owner of auth tables
[PASS] no auth sequences found
PostgreSQL runtime privilege verification passed.
```

Conclusion:

- `xanhnow_auth` has only runtime DML permissions needed by Login App.
- `xanhnow_auth` does not have schema CREATE.
- `xanhnow_auth` is not a member of `xanhnow_auth_migrator`.
- `xanhnow_auth` does not own auth tables, so owner-level DDL is not available.
- `xanhnow_auth` does not have TRUNCATE/TRIGGER.

## Vault

Runtime AppRole:

- AppRole: `auth-login`
- Runtime policy: `auth-login-read`
- Runtime secret paths include PostgreSQL, Redis, Kafka, and password hashing secrets.

Migration AppRole:

- AppRole: `auth-login-migrator`
- Policy: `auth-login-migration-read`
- Secret path: `kv/xanhnow/auth-login/postgres/migration`
- Migration AppRole was verified earlier to read migration secret and be denied runtime secret.

Security status:

- Previously exposed Vault root token was revoked/deleted by the user.
- Do not rely on old root token again.
- If Vault AppRole RoleID/SecretID generation is needed later, use a fresh admin token or a dedicated admin workflow.
- The 5 unseal keys were not exposed in this chat based on current record; no rekey required unless they were exposed elsewhere.

## Redis

- Redis is real, not in-memory.
- Runtime Redis connection is read from Vault.
- Login creates real Redis session.
- Logout invalidates real Redis session.

## Kafka

- Kafka topic: `xanhnow.auth.login.events.v1`
- Kafka tool on Windows: `C:\Tools\kafkactl\kafkactl.exe`
- kafkactl config path used: `%APPDATA%\kafkactl\config.yml`
- Brokers configured:
  - `192.168.2.14:9092`
  - `192.168.2.31:9092`
  - `192.168.2.51:9092`
- Kafka Vault secret path: `kv/xanhnow/auth-login/kafka`
- Kafka secret currently represents no-SASL Kafka; `n/a` fields are treated as empty by producer.
- Outbox dispatcher: `OutboxDispatcherHostedService`
- Kafka E2E verified by consumer with events:
  - `UserRegistered`
  - `UserLoggedIn`
  - `UserLoggedOut`

## Verification Commands Passed

```powershell
dotnet build C:\XanhnowAuth\Auth_Login_App\XanhNow.Auth.Login.slnx --no-restore
dotnet test C:\XanhnowAuth\Auth_Login_App\XanhNow.Auth.Login.slnx --no-restore
```

Latest known result:

- Build: passed, 0 warnings, 0 errors.
- Tests: passed, 3/3.

## How To Run API With Runtime Vault AppRole

Use a fresh runtime SecretID each time if current one has expired:

```powershell
cd C:\XanhnowAuth\Auth_Login_App
$env:VAULT_ADDR="https://192.168.2.81:8200"

$runtimeRoleId = vault read -field=role_id auth/approle/role/auth-login/role-id
$runtimeSecretId = vault write -field=secret_id -f auth/approle/role/auth-login/secret-id

$env:VAULT_ROLE_ID = $runtimeRoleId
$env:VAULT_SECRET_ID = $runtimeSecretId
$env:Infrastructure__Mode = "Real"

dotnet run --no-build --project src\XanhNow.Auth.Login.Api\XanhNow.Auth.Login.Api.csproj --urls http://localhost:5097
```

If only PostgreSQL + Redis testing is needed, use:

```powershell
$env:Infrastructure__Mode = "RedisVault"
```

## Next Suggested Work

1. Add structured logging/correlation polish if needed.
2. Add API contract examples to Swagger.
3. Add more application tests around invalid login, logout, session lookup, and Kafka outbox retry behavior.
4. Decide whether to create a dedicated non-root Vault admin workflow for generating AppRole SecretIDs.
5. Rotate any remaining temporary test AppRole SecretIDs after active testing.

## Deployment Readiness Update - 2026-07-14

Status: READY FOR CONTROLLED DEPLOYMENT SMOKE, not yet fully accepted in production until API pool deployment and Edge smoke pass.

Completed for Edge/internal deployment:

- API now supports Edge-facing health routes:
  - `GET /api/health/live`
  - `GET /api/health/ready`
  - Existing `/health/live` and `/health/ready` remain available.
- API now exposes Edge probe route:
  - `GET /api/edge-probe`
  - Response includes service name, node name, release, and source SHA when `release.json` is present.
  - Response headers include `X-XanhNow-Api-Node` and `X-XanhNow-Release`.
- Forwarded headers are configured for the current Edge gateways only:
  - `192.168.2.24`
  - `192.168.2.64`
- HTTPS redirection is disabled by default for production internal Edge traffic. Enable only by explicitly setting:
  - `Http__EnableHttpsRedirection=true`
- Deployment documentation added:
  - `deploy\DEPLOYMENT_SERVICE_PATHS.md`
  - `deploy\systemd\xanhnow-auth-login.service`
  - `deploy\vault\secret-field-contract.md`

Target production flow:

```text
Client/Postman -> Edge VIP 192.168.2.82 -> Nginx /api/ -> API pool 192.168.2.25/.38/.65:8080 -> Auth Login API
```

Runtime deploy contract:

- Deploy to API nodes: `api-1`, `api-2`, `api-3`.
- Listen internally on HTTP only: `http://0.0.0.0:8080`.
- Use runtime AppRole only:
  - `VAULT_ROLE_ID=<auth-login role id>`
  - `VAULT_SECRET_ID=<fresh auth-login secret id>`
  - `Infrastructure__Mode=Real`
  - `Vault__Address=https://192.168.2.81:8200`
  - `Http__EnableHttpsRedirection=false`
- Keep migration separate from API runtime.

Verification on 2026-07-14:

```powershell
dotnet build XanhNow.Auth.Login.slnx -c Release --no-restore
dotnet test XanhNow.Auth.Login.slnx -c Release --no-build --no-restore
```

Result:

- Build: PASS, 0 warnings, 0 errors.
- Tests: PASS, 3/3.

Remaining before declaring production accepted:

1. Publish release artifact and copy it to API pool nodes.
2. Create fresh `auth-login` AppRole SecretID for runtime.
3. Install/enable user systemd service on each API node.
4. Smoke each API node directly on `:8080`.
5. Smoke through Edge VIP `192.168.2.82` for register/login/logout/session and `/api/edge-probe`.
6. Confirm PostgreSQL, Redis session, and Kafka outbox events are all real and healthy after deployment.
