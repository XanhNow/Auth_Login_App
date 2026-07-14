using System.Security.Cryptography;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public Task<PasswordHashResult> HashPasswordAsync(string password, PasswordSecret secret, CancellationToken cancellationToken)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, secret.Pepper, salt);
        var encoded = $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        return Task.FromResult(new PasswordHashResult(encoded, "PBKDF2-SHA256", secret.PepperVersion));
    }

    public Task<bool> VerifyPasswordAsync(string password, PasswordHash hash, PasswordSecret secret, CancellationToken cancellationToken)
    {
        var parts = hash.Value.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations))
        {
            return Task.FromResult(false);
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            $"{password}:{secret.Pepper}",
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(actual, expected));
    }

    public bool NeedsRehash(PasswordHash hash, PasswordSecret secret) =>
        hash.Algorithm != "PBKDF2-SHA256" || hash.PepperVersion != secret.PepperVersion;

    private static byte[] Derive(string password, string pepper, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2($"{password}:{pepper}", salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
