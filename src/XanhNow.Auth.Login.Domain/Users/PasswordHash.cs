namespace XanhNow.Auth.Login.Domain.Users;

public sealed record PasswordHash
{
    public PasswordHash(string value, string algorithm, string pepperVersion)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Password hash must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(algorithm))
        {
            throw new DomainException("Password algorithm must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(pepperVersion))
        {
            throw new DomainException("Password pepper version must not be empty.");
        }

        Value = value;
        Algorithm = algorithm;
        PepperVersion = pepperVersion;
    }

    public string Value { get; }

    public string Algorithm { get; }

    public string PepperVersion { get; }
}
