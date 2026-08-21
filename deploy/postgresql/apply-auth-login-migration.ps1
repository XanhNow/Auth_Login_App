param(
    [string]$ProjectRoot = "C:\BackEndXanhNow\XanhnowAuth\Auth_Login_App",
    [string]$HostName = "192.168.2.80",
    [int]$Port = 15432,
    [string]$Database = "authtest",
    [string]$RootCert = "C:\BackEndXanhNow\XanhnowCustomer\secrets\postgresql-root-ca.crt",
    [switch]$PreVerify
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

$migratorPassword = Convert-SecureStringToPlainText (Read-Host "Password xanhnow_auth_migrator" -AsSecureString)
$connectionString = "Host=$HostName;Port=$Port;Database=$Database;Username=xanhnow_auth_migrator;Password=$migratorPassword;SSL Mode=VerifyFull;Root Certificate=$RootCert"

Push-Location $ProjectRoot
try {
    $env:DOTNET_ENVIRONMENT = "Production"
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $env:AUTH_LOGIN_MIGRATION_CONNECTION_STRING = $connectionString

    if ($PreVerify) {
        dotnet run --project src\XanhNow.Auth.Login.Migrator\XanhNow.Auth.Login.Migrator.csproj -- --verify-privileges
        if ($LASTEXITCODE -ne 0) {
            throw "pre-migration privilege verification failed with exit code $LASTEXITCODE"
        }
    }

    dotnet run --project src\XanhNow.Auth.Login.Migrator\XanhNow.Auth.Login.Migrator.csproj
    if ($LASTEXITCODE -ne 0) {
        throw "migration failed with exit code $LASTEXITCODE"
    }

    dotnet run --project src\XanhNow.Auth.Login.Migrator\XanhNow.Auth.Login.Migrator.csproj -- --verify-privileges
    if ($LASTEXITCODE -ne 0) {
        throw "post-migration privilege verification failed with exit code $LASTEXITCODE"
    }

    Write-Host "Auth Login migration applied and runtime privileges verified."
} finally {
    Remove-Item Env:\AUTH_LOGIN_MIGRATION_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Item Env:\DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
    Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    Pop-Location
}
