param(
    [string]$VaultPath = "C:\Program Files\HashiCorp\Vault\vault.exe",
    [string]$VaultAddress = "https://192.168.2.81:8200",
    [string]$VaultCaCert = "C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App\runtime\trust\vault-ca.crt",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $VaultPath)) {
    throw "vault.exe not found: $VaultPath"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$runtimePolicy = Join-Path $repoRoot "deploy\vault\s101-xanhnow-auth-login-runtime-prod.hcl"
$migratorPolicy = Join-Path $repoRoot "deploy\vault\s101-xanhnow-auth-login-migrator-prod.hcl"

$env:VAULT_ADDR = $VaultAddress
$env:VAULT_CACERT = $VaultCaCert

& $VaultPath policy write s101-xanhnow-auth-login-runtime-prod $runtimePolicy
if ($LASTEXITCODE -ne 0) { throw "runtime policy write failed" }

& $VaultPath policy write s101-xanhnow-auth-login-migrator-prod $migratorPolicy
if ($LASTEXITCODE -ne 0) { throw "migrator policy write failed" }

& $VaultPath write auth/approle/role/s101-xanhnow-auth-login-runtime-prod `
    token_policies="s101-xanhnow-auth-login-runtime-prod" `
    token_ttl="1h" `
    token_max_ttl="24h" `
    secret_id_ttl="720h" `
    secret_id_num_uses="0"
if ($LASTEXITCODE -ne 0) { throw "runtime approle write failed" }

& $VaultPath write auth/approle/role/s101-xanhnow-auth-login-migrator-prod `
    token_policies="s101-xanhnow-auth-login-migrator-prod" `
    token_ttl="15m" `
    token_max_ttl="1h" `
    secret_id_ttl="24h" `
    secret_id_num_uses="1"
if ($LASTEXITCODE -ne 0) { throw "migrator approle write failed" }

$runtimeRoleId = & $VaultPath read -field=role_id auth/approle/role/s101-xanhnow-auth-login-runtime-prod/role-id
$runtimeSecretId = & $VaultPath write -field=secret_id -f auth/approle/role/s101-xanhnow-auth-login-runtime-prod/secret-id
$migratorRoleId = & $VaultPath read -field=role_id auth/approle/role/s101-xanhnow-auth-login-migrator-prod/role-id
$migratorSecretId = & $VaultPath write -field=secret_id -f auth/approle/role/s101-xanhnow-auth-login-migrator-prod/secret-id

if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
    $runtimeRoleId | Out-File -FilePath (Join-Path $OutputDirectory "runtime-role_id") -Encoding ascii -NoNewline
    $runtimeSecretId | Out-File -FilePath (Join-Path $OutputDirectory "runtime-secret_id") -Encoding ascii -NoNewline
    $migratorRoleId | Out-File -FilePath (Join-Path $OutputDirectory "migrator-role_id") -Encoding ascii -NoNewline
    $migratorSecretId | Out-File -FilePath (Join-Path $OutputDirectory "migrator-secret_id") -Encoding ascii -NoNewline
    Write-Host "AppRole material written to $OutputDirectory. Do not commit these files."
}

Write-Host "runtime_role_id_length=$($runtimeRoleId.Length)"
Write-Host "runtime_secret_id_length=$($runtimeSecretId.Length)"
Write-Host "migrator_role_id_length=$($migratorRoleId.Length)"
Write-Host "migrator_secret_id_length=$($migratorSecretId.Length)"
