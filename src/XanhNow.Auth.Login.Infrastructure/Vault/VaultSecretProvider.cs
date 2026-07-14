using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;

namespace XanhNow.Auth.Login.Infrastructure.Vault;

public sealed class VaultSecretProvider : IVaultSecretProvider
{
    private readonly HttpClient httpClient;
    private readonly VaultOptions options;
    private string? clientToken;

    public VaultSecretProvider(HttpClient httpClient, VaultOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<PostgresSecret> ReadPostgresSecretAsync(CancellationToken cancellationToken)
    {
        var data = await ReadSecretDataAsync("postgres", cancellationToken);
        if (TryGet(data, "connection_string", out var connectionString))
        {
            return new PostgresSecret(connectionString);
        }

        var host = Required(data, "host");
        var port = Optional(data, "port") ?? "5432";
        var database = Required(data, "database");
        var username = Required(data, "username");
        var password = Required(data, "password");
        var sslMode = Optional(data, "ssl_mode");
        var result = $"Host={host};Port={port};Database={database};Username={username};Password={password};Pooling=true;No Reset On Close=true;Timeout=15;Command Timeout=30";
        if (!string.IsNullOrWhiteSpace(sslMode))
        {
            result += $";SSL Mode={sslMode}";
        }

        return new PostgresSecret(result);
    }

    public async Task<RedisSecret> ReadRedisSecretAsync(CancellationToken cancellationToken)
    {
        var data = await ReadSecretDataAsync("redis", cancellationToken);
        var tls = string.Equals(Optional(data, "tls_enabled") ?? Optional(data, "tls"), "true", StringComparison.OrdinalIgnoreCase);
        return new RedisSecret(Required(data, "password"), tls);
    }

    public async Task<KafkaSecret> ReadKafkaSecretAsync(CancellationToken cancellationToken)
    {
        var data = await ReadSecretDataAsync("kafka", cancellationToken);
        return new KafkaSecret(
            Optional(data, "username"),
            Optional(data, "password"),
            Optional(data, "security_protocol"),
            Optional(data, "sasl_mechanism"));
    }

    public async Task<PasswordSecret> ReadPasswordSecretAsync(string? pepperVersion, CancellationToken cancellationToken)
    {
        var data = await ReadSecretDataAsync("password", cancellationToken);
        return new PasswordSecret(
            Required(data, "pepper"),
            pepperVersion ?? Optional(data, "pepper_version") ?? "active",
            Optional(data, "algorithm") ?? "PBKDF2-SHA256");
    }

    private async Task<JsonObject> ReadSecretDataAsync(string name, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/{options.MountPath}/data/{options.BasePath}/{name}");
        request.Headers.Add("X-Vault-Token", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureVaultSuccessAsync(response, $"read secret '{name}'", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return payload?["data"]?["data"]?.AsObject()
            ?? throw new InvalidOperationException("Vault KV v2 response did not contain a data object.");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(clientToken))
        {
            return clientToken;
        }

        var roleId = Environment.GetEnvironmentVariable(options.RoleIdEnvironmentVariable);
        var secretId = Environment.GetEnvironmentVariable(options.SecretIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId))
        {
            throw new InvalidOperationException("Vault AppRole material is not available in environment variables.");
        }

        using var response = await httpClient.PostAsJsonAsync("/v1/auth/approle/login", new
        {
            role_id = roleId,
            secret_id = secretId
        }, cancellationToken);
        await EnsureVaultSuccessAsync(response, "AppRole login", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        clientToken = payload?["auth"]?["client_token"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Vault AppRole login did not return a client token.");
        return clientToken;
    }

    private static async Task EnsureVaultSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        throw new HttpRequestException($"Vault {operation} failed with {status}. Response: {body}", null, response.StatusCode);
    }
    private static string Required(JsonObject data, string key)
    {
        if (!TryGet(data, key, out var value))
        {
            throw new InvalidOperationException($"Vault secret is missing required field '{key}'.");
        }

        return value;
    }

    private static string? Optional(JsonObject data, string key)
    {
        return TryGet(data, key, out var value) ? value : null;
    }

    private static bool TryGet(JsonObject data, string key, out string value)
    {
        value = string.Empty;
        if (!data.TryGetPropertyValue(key, out var node) || node is null)
        {
            return false;
        }

        value = node.GetValue<string>();
        return !string.IsNullOrWhiteSpace(value);
    }
}
