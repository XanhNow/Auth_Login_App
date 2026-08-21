using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using XanhNow.Auth.Login.Infrastructure.Persistence;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var connectionString = configuration.GetConnectionString("AuthMigrationDb")
    ?? configuration["AUTH_LOGIN_MIGRATION_CONNECTION_STRING"]
    ?? await ReadMigrationConnectionStringFromVaultAsync(configuration, CancellationToken.None);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Migration connection string is required. Set ConnectionStrings__AuthMigrationDb, AUTH_LOGIN_MIGRATION_CONNECTION_STRING, or MIGRATION_VAULT_ROLE_ID/MIGRATION_VAULT_SECRET_ID.");
}

var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
if (!string.Equals(connectionStringBuilder.Username, "xanhnow_auth_migrator", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Migration must run as xanhnow_auth_migrator. Runtime user xanhnow_auth is not allowed to run DDL.");
}

if (!string.Equals(connectionStringBuilder.Host, "192.168.2.80", StringComparison.Ordinal) ||
    connectionStringBuilder.Port != 15432 ||
    !string.Equals(connectionStringBuilder.Database, "authtest", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Migration must target admin endpoint 192.168.2.80:15432 and database authtest.");
}

if (args.Any(arg => string.Equals(arg, "--verify-privileges", StringComparison.OrdinalIgnoreCase)))
{
    await VerifyRuntimePrivilegesAsync(connectionString, CancellationToken.None);
    return;
}

var options = new DbContextOptionsBuilder<AuthDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth"))
    .Options;

await using var dbContext = new AuthDbContext(options);
await dbContext.Database.MigrateAsync();
await dbContext.Database.ExecuteSqlRawAsync("""
    GRANT USAGE ON SCHEMA auth TO xanhnow_auth;
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
        auth.users,
        auth.user_phone_histories,
        auth.login_attempts,
        auth.auth_audit_logs,
        auth.outbox_events
    TO xanhnow_auth;
    """);

Console.WriteLine("Auth Login database migration completed and runtime grants applied.");

static async Task VerifyRuntimePrivilegesAsync(string connectionString, CancellationToken cancellationToken)
{
    string[] expectedTables =
    [
        "auth_audit_logs",
        "login_attempts",
        "outbox_events",
        "user_phone_histories",
        "users"
    ];

    var failures = new List<string>();
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await VerifyRuntimeRoleAsync(connection, failures, cancellationToken);
    await VerifySchemaPrivilegesAsync(connection, failures, cancellationToken);
    await VerifyRoleMembershipAsync(connection, failures, cancellationToken);
    await VerifyTablePrivilegesAsync(connection, expectedTables, failures, cancellationToken);
    await VerifySequencePrivilegesAsync(connection, failures, cancellationToken);

    if (failures.Count > 0)
    {
        Console.WriteLine("PostgreSQL runtime privilege verification failed:");
        foreach (var failure in failures)
        {
            Console.WriteLine($"[FAIL] {failure}");
        }

        throw new InvalidOperationException("Runtime PostgreSQL privileges are not locked down correctly.");
    }

    Console.WriteLine("PostgreSQL runtime privilege verification passed.");
}

static async Task VerifyRuntimeRoleAsync(NpgsqlConnection connection, List<string> failures, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        SELECT rolsuper, rolcreatedb, rolcreaterole, rolreplication
        FROM pg_roles
        WHERE rolname = 'xanhnow_auth';
        """, connection);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        failures.Add("runtime role xanhnow_auth does not exist");
        return;
    }

    var isElevated = reader.GetBoolean(0) || reader.GetBoolean(1) || reader.GetBoolean(2) || reader.GetBoolean(3);
    if (isElevated)
    {
        failures.Add("runtime role xanhnow_auth has superuser/createdb/createrole/replication privilege");
        return;
    }

    Console.WriteLine("[PASS] runtime role exists and is not elevated");
}

static async Task VerifySchemaPrivilegesAsync(NpgsqlConnection connection, List<string> failures, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        SELECT
            has_schema_privilege('xanhnow_auth', 'auth', 'USAGE'),
            has_schema_privilege('xanhnow_auth', 'auth', 'CREATE');
        """, connection);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    await reader.ReadAsync(cancellationToken);

    var hasUsage = reader.GetBoolean(0);
    var hasCreate = reader.GetBoolean(1);
    if (!hasUsage || hasCreate)
    {
        failures.Add("runtime user must have USAGE but not CREATE on schema auth");
        return;
    }

    Console.WriteLine("[PASS] runtime user has USAGE but not CREATE on schema auth");
}

static async Task VerifyRoleMembershipAsync(NpgsqlConnection connection, List<string> failures, CancellationToken cancellationToken)
{
    await using var command = new NpgsqlCommand("""
        SELECT pg_has_role('xanhnow_auth', 'xanhnow_auth_migrator', 'member');
        """, connection);

    var isMemberOfMigrator = (bool)(await command.ExecuteScalarAsync(cancellationToken)
        ?? throw new InvalidOperationException("Role membership query did not return a value."));

    if (isMemberOfMigrator)
    {
        failures.Add("runtime user must not be a member of xanhnow_auth_migrator");
        return;
    }

    Console.WriteLine("[PASS] runtime user is not member of migrator role");
}

static async Task VerifyTablePrivilegesAsync(NpgsqlConnection connection, string[] expectedTables, List<string> failures, CancellationToken cancellationToken)
{
    var seenTables = new HashSet<string>(StringComparer.Ordinal);
    var runtimeOwnedTables = new List<string>();
    var badDmlTables = new List<string>();
    var ddlLikeTables = new List<string>();

    await using var command = new NpgsqlCommand("""
        SELECT
            c.relname,
            pg_get_userbyid(c.relowner) AS owner,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'SELECT') AS can_select,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'INSERT') AS can_insert,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'UPDATE') AS can_update,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'DELETE') AS can_delete,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'TRUNCATE') AS can_truncate,
            has_table_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'TRIGGER') AS can_trigger
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'auth'
          AND c.relkind = 'r'
          AND c.relname = ANY(@tables)
        ORDER BY c.relname;
        """, connection);
    command.Parameters.AddWithValue("tables", expectedTables);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        var tableName = reader.GetString(0);
        var owner = reader.GetString(1);
        var hasDml = reader.GetBoolean(2) && reader.GetBoolean(3) && reader.GetBoolean(4) && reader.GetBoolean(5);
        var hasDdlLike = reader.GetBoolean(6) || reader.GetBoolean(7);

        seenTables.Add(tableName);

        if (string.Equals(owner, "xanhnow_auth", StringComparison.Ordinal))
        {
            runtimeOwnedTables.Add(tableName);
        }

        if (!hasDml)
        {
            badDmlTables.Add(tableName);
        }

        if (hasDdlLike)
        {
            ddlLikeTables.Add(tableName);
        }
    }

    var missingTables = expectedTables.Except(seenTables, StringComparer.Ordinal).ToArray();
    if (missingTables.Length > 0)
    {
        failures.Add($"expected auth tables are missing: {string.Join(", ", missingTables)}");
    }

    if (badDmlTables.Count > 0)
    {
        failures.Add($"runtime user is missing SELECT/INSERT/UPDATE/DELETE on: {string.Join(", ", badDmlTables)}");
    }

    if (ddlLikeTables.Count > 0)
    {
        failures.Add($"runtime user has TRUNCATE or TRIGGER on: {string.Join(", ", ddlLikeTables)}");
    }

    if (runtimeOwnedTables.Count > 0)
    {
        failures.Add($"runtime user owns auth tables and can perform owner-level DDL: {string.Join(", ", runtimeOwnedTables)}");
    }

    if (missingTables.Length == 0 && badDmlTables.Count == 0 && ddlLikeTables.Count == 0)
    {
        Console.WriteLine("[PASS] runtime user has DML and no TRUNCATE/TRIGGER on auth tables");
    }

    if (runtimeOwnedTables.Count == 0)
    {
        Console.WriteLine("[PASS] runtime user is not owner of auth tables");
    }
}

static async Task VerifySequencePrivilegesAsync(NpgsqlConnection connection, List<string> failures, CancellationToken cancellationToken)
{
    var sequenceCount = 0;
    var badSequences = new List<string>();

    await using var command = new NpgsqlCommand("""
        SELECT
            c.relname,
            has_sequence_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'USAGE') AS can_usage,
            has_sequence_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'SELECT') AS can_select,
            has_sequence_privilege('xanhnow_auth', format('%I.%I', n.nspname, c.relname), 'UPDATE') AS can_update
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'auth'
          AND c.relkind = 'S'
        ORDER BY c.relname;
        """, connection);

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        sequenceCount++;
        var sequenceName = reader.GetString(0);
        var hasRequired = reader.GetBoolean(1) && reader.GetBoolean(2);
        var hasUpdate = reader.GetBoolean(3);

        if (!hasRequired || hasUpdate)
        {
            badSequences.Add(sequenceName);
        }
    }

    if (badSequences.Count > 0)
    {
        failures.Add($"runtime sequence privileges must be USAGE/SELECT only on: {string.Join(", ", badSequences)}");
        return;
    }

    Console.WriteLine(sequenceCount == 0
        ? "[PASS] no auth sequences found"
        : "[PASS] runtime sequence privileges are USAGE/SELECT only");
}

static async Task<string?> ReadMigrationConnectionStringFromVaultAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var roleId = await ReadConfiguredSecretAsync(
        Environment.GetEnvironmentVariable("MIGRATION_VAULT_ROLE_ID"),
        configuration["MigrationVault:RoleIdFile"],
        cancellationToken);
    var secretId = await ReadConfiguredSecretAsync(
        Environment.GetEnvironmentVariable("MIGRATION_VAULT_SECRET_ID"),
        configuration["MigrationVault:SecretIdFile"],
        cancellationToken);
    if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId))
    {
        return null;
    }

    var vaultAddress = configuration["MigrationVault:Address"] ?? "https://192.168.2.81:8200";
    var mountPath = configuration["MigrationVault:MountPath"] ?? "kv";
    var secretPath = configuration["MigrationVault:SecretPath"] ?? "xanhnow/s101/auth-login/postgres/migration";

    using var httpClient = CreateVaultHttpClient(vaultAddress, configuration["MigrationVault:CaCertFile"] ?? configuration["MigrationVault:CaCertificatePath"]);
    using var loginResponse = await httpClient.PostAsJsonAsync("/v1/auth/approle/login", new
    {
        role_id = roleId,
        secret_id = secretId
    }, cancellationToken);
    await EnsureVaultSuccessAsync(loginResponse, "AppRole login", cancellationToken);

    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
    var token = loginPayload?["auth"]?["client_token"]?.GetValue<string>()
        ?? throw new InvalidOperationException("Vault AppRole login did not return a client token.");

    using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/{mountPath}/data/{secretPath}");
    request.Headers.Add("X-Vault-Token", token);
    using var secretResponse = await httpClient.SendAsync(request, cancellationToken);
    await EnsureVaultSuccessAsync(secretResponse, "read migration PostgreSQL secret", cancellationToken);

    var secretPayload = await secretResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
    var data = secretPayload?["data"]?["data"]?.AsObject()
        ?? throw new InvalidOperationException("Vault KV v2 response did not contain a data object.");

    return data.TryGetPropertyValue("connection_string", out var node) && node is not null
        ? node.GetValue<string>()
        : throw new InvalidOperationException("Vault migration secret is missing connection_string.");
}

static async Task<string?> ReadConfiguredSecretAsync(string? value, string? file, CancellationToken cancellationToken)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value.Trim();
    }

    return string.IsNullOrWhiteSpace(file) || !File.Exists(file)
        ? null
        : (await File.ReadAllTextAsync(file, cancellationToken)).Trim();
}

static HttpClient CreateVaultHttpClient(string vaultAddress, string? caPath)
{
    if (string.IsNullOrWhiteSpace(caPath))
    {
        return new HttpClient { BaseAddress = new Uri(vaultAddress) };
    }

    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
    {
        if (certificate is null)
        {
            return false;
        }

        using var ca = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(caPath);
        using var chain = new System.Security.Cryptography.X509Certificates.X509Chain();
        chain.ChainPolicy.TrustMode = System.Security.Cryptography.X509Certificates.X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    };

    return new HttpClient(handler) { BaseAddress = new Uri(vaultAddress) };
}

static async Task EnsureVaultSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
    throw new HttpRequestException($"Vault {operation} failed with {status}. Response: {body}", null, response.StatusCode);
}
