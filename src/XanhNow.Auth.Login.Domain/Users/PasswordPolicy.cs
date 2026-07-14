namespace XanhNow.Auth.Login.Domain.Users;

public sealed class PasswordPolicy
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456",
        "password",
        "qwerty",
        "111111"
    };

    public void EnsureValid(string password, PhoneNumber phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            throw new WeakPasswordException("Password must contain at least 10 characters.");
        }

        if (password.Contains(phoneNumber.Normalized.TrimStart('+'), StringComparison.OrdinalIgnoreCase))
        {
            throw new WeakPasswordException("Password must not contain the phone number.");
        }

        if (CommonPasswords.Contains(password))
        {
            throw new WeakPasswordException("Password is too common.");
        }

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

        if (!hasLower || !hasUpper || (!hasDigit && !hasSymbol))
        {
            throw new WeakPasswordException("Password must include lower case, upper case, and a digit or symbol.");
        }
    }
}
