namespace XanhNow.Auth.Login.Infrastructure.Vault;

public sealed class RuntimeSecretFilesOptions
{
    public string PostgresConnectionStringFile { get; set; } = string.Empty;

    public string RedisPasswordFile { get; set; } = string.Empty;

    public string RedisTlsEnabledFile { get; set; } = string.Empty;

    public string PasswordPepperFile { get; set; } = string.Empty;

    public string PasswordPepperVersionFile { get; set; } = string.Empty;

    public string PasswordAlgorithmFile { get; set; } = string.Empty;

    public bool HasPostgresFile => !string.IsNullOrWhiteSpace(PostgresConnectionStringFile);

    public bool HasRedisFile => !string.IsNullOrWhiteSpace(RedisPasswordFile);

    public bool HasPasswordSecretFile => !string.IsNullOrWhiteSpace(PasswordPepperFile);
}
