param(
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    [string]$HostName = "192.168.2.80",
    [int]$Port = 15432,
    [string]$Database = "authtest",
    [string]$RootCert = "C:\BackEndXanhNow\XanhnowCustomer\secrets\postgresql-root-ca.crt"
)

$ErrorActionPreference = "Stop"

function Convert-SecureStringToPlainText([securestring]$Value) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Escape-SqlLiteral([string]$Value) {
    $Value.Replace("'", "''")
}

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found: $PsqlPath"
}

Write-Host "This script provisions Auth_Login_App PostgreSQL roles/schema in database $Database."
Write-Host "Roles: s101_xanhnow_auth_login_migrator, s101_xanhnow_auth_login_runtime"

$postgresPassword = Convert-SecureStringToPlainText (Read-Host "Password postgres" -AsSecureString)
$migratorPassword = Convert-SecureStringToPlainText (Read-Host "New password for s101_xanhnow_auth_login_migrator" -AsSecureString)
$runtimePassword = Convert-SecureStringToPlainText (Read-Host "New password for s101_xanhnow_auth_login_runtime" -AsSecureString)

$migratorPasswordSql = Escape-SqlLiteral $migratorPassword
$runtimePasswordSql = Escape-SqlLiteral $runtimePassword

$sqlPath = Join-Path $env:TEMP ("s101-auth-login-provision-" + [Guid]::NewGuid().ToString("N") + ".sql")

@"
DO `$`$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 's101_xanhnow_auth_login_migrator') THEN
    CREATE ROLE s101_xanhnow_auth_login_migrator LOGIN PASSWORD '$migratorPasswordSql' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  ELSE
    ALTER ROLE s101_xanhnow_auth_login_migrator WITH LOGIN PASSWORD '$migratorPasswordSql' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 's101_xanhnow_auth_login_runtime') THEN
    CREATE ROLE s101_xanhnow_auth_login_runtime LOGIN PASSWORD '$runtimePasswordSql' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  ELSE
    ALTER ROLE s101_xanhnow_auth_login_runtime WITH LOGIN PASSWORD '$runtimePasswordSql' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  END IF;
END
`$`$;

GRANT CONNECT ON DATABASE authtest TO s101_xanhnow_auth_login_migrator;
GRANT CONNECT ON DATABASE authtest TO s101_xanhnow_auth_login_runtime;
CREATE SCHEMA IF NOT EXISTS auth AUTHORIZATION s101_xanhnow_auth_login_migrator;
ALTER SCHEMA auth OWNER TO s101_xanhnow_auth_login_migrator;
REVOKE CREATE ON DATABASE authtest FROM s101_xanhnow_auth_login_migrator;
REVOKE CREATE ON DATABASE authtest FROM s101_xanhnow_auth_login_runtime;
REVOKE CREATE ON SCHEMA auth FROM s101_xanhnow_auth_login_runtime;
"@ | Set-Content -Path $sqlPath -Encoding ascii

$env:PGPASSWORD = $postgresPassword
$env:PGSSLMODE = "verify-full"
$env:PGSSLROOTCERT = $RootCert

try {
    & $PsqlPath -h $HostName -p $Port -U postgres -d $Database -f $sqlPath
    if ($LASTEXITCODE -ne 0) {
        throw "psql provision failed with exit code $LASTEXITCODE"
    }

    $env:PGPASSWORD = $migratorPassword
    & $PsqlPath -h $HostName -p $Port -U s101_xanhnow_auth_login_migrator -d $Database -c "select current_user, current_database();"
    if ($LASTEXITCODE -ne 0) {
        throw "migrator login verification failed with exit code $LASTEXITCODE"
    }

    $env:PGPASSWORD = $runtimePassword
    & $PsqlPath -h $HostName -p $Port -U s101_xanhnow_auth_login_runtime -d $Database -c "select current_user, current_database();"
    if ($LASTEXITCODE -ne 0) {
        throw "runtime login verification failed with exit code $LASTEXITCODE"
    }

    Write-Host "Auth Login PostgreSQL roles/schema provisioned and verified."
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGSSLMODE -ErrorAction SilentlyContinue
    Remove-Item Env:\PGSSLROOTCERT -ErrorAction SilentlyContinue
    Remove-Item $sqlPath -Force -ErrorAction SilentlyContinue
}


