param(
    [string]$VaultPath = "C:\Program Files\HashiCorp\Vault\vault.exe",
    [string]$VaultAddress = "https://192.168.2.81:8200",
    [string]$VaultCaCert = "C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App\runtime\trust\vault-ca.crt",
    [string]$WindowsRootCert = "C:\BackEndXanhNow\XanhnowCustomer\secrets\postgresql-root-ca.crt"
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

if (-not (Test-Path $VaultPath)) {
    throw "vault.exe not found: $VaultPath"
}

$runtimePassword = Convert-SecureStringToPlainText (Read-Host "Password for PostgreSQL user xanhnow_auth" -AsSecureString)
$migratorPassword = Convert-SecureStringToPlainText (Read-Host "Password for PostgreSQL user xanhnow_auth_migrator" -AsSecureString)

$migrationConnectionString = "Host=192.168.2.80;Port=15432;Database=authtest;Username=xanhnow_auth_migrator;Password=$migratorPassword;SSL Mode=VerifyFull;Root Certificate=$WindowsRootCert"

$env:VAULT_ADDR = $VaultAddress
$env:VAULT_CACERT = $VaultCaCert

& $VaultPath kv put kv/xanhnow/s101/auth-login/postgres/runtime `
    host="192.168.2.80" `
    port="5432" `
    database="authtest" `
    username="xanhnow_auth" `
    password="$runtimePassword"
if ($LASTEXITCODE -ne 0) { throw "runtime postgres secret write failed" }

& $VaultPath kv put kv/xanhnow/s101/auth-login/postgres/migration connection_string="$migrationConnectionString"
if ($LASTEXITCODE -ne 0) { throw "migration postgres secret write failed" }

Write-Host "auth_login_runtime_password_length=$($runtimePassword.Length)"
Write-Host "auth_login_migration_connection_string_length=$($migrationConnectionString.Length)"
Write-Host "Auth Login PostgreSQL secrets written to Vault s101 paths."
