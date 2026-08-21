using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;

namespace XanhNow.Auth.Login.Infrastructure.Vault;

public sealed class RuntimeSecretFileProvider : IVaultSecretProvider
{
    private readonly RuntimeSecretFilesOptions options;

    public RuntimeSecretFileProvider(RuntimeSecretFilesOptions options)
    {
        this.options = options;
    }

    public async Task<PostgresSecret> ReadPostgresSecretAsync(CancellationToken cancellationToken)
    {
        return new PostgresSecret(await ReadRequiredFileAsync(
            options.PostgresConnectionStringFile,
            "RuntimeSecrets:PostgresConnectionStringFile",
            cancellationToken));
    }

    public async Task<RedisSecret> ReadRedisSecretAsync(CancellationToken cancellationToken)
    {
        var password = await ReadRequiredFileAsync(
            options.RedisPasswordFile,
            "RuntimeSecrets:RedisPasswordFile",
            cancellationToken);
        var tlsValue = await ReadOptionalFileAsync(options.RedisTlsEnabledFile, cancellationToken);
        var tlsEnabled = string.Equals(tlsValue, "true", StringComparison.OrdinalIgnoreCase);

        return new RedisSecret(password, tlsEnabled);
    }

    public async Task<PasswordSecret> ReadPasswordSecretAsync(string? pepperVersion, CancellationToken cancellationToken)
    {
        var pepper = await ReadRequiredFileAsync(
            options.PasswordPepperFile,
            "RuntimeSecrets:PasswordPepperFile",
            cancellationToken);
        var configuredPepperVersion = await ReadOptionalFileAsync(options.PasswordPepperVersionFile, cancellationToken);
        var algorithm = await ReadOptionalFileAsync(options.PasswordAlgorithmFile, cancellationToken);

        return new PasswordSecret(
            pepper,
            pepperVersion ?? configuredPepperVersion ?? "active",
            algorithm ?? "PBKDF2-SHA256");
    }

    private static async Task<string> ReadRequiredFileAsync(
        string path,
        string configurationKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{configurationKey} is required when runtime secret files are enabled.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Runtime secret file does not exist: {path}");
        }

        var value = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Runtime secret file is empty: {path}");
        }

        return value;
    }

    private static async Task<string?> ReadOptionalFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var value = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
