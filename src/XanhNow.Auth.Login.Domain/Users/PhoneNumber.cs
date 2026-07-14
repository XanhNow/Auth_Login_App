using System.Text.RegularExpressions;

namespace XanhNow.Auth.Login.Domain.Users;

public sealed partial record PhoneNumber
{
    private PhoneNumber(string original, string normalized, string masked)
    {
        Original = original;
        Normalized = normalized;
        Masked = masked;
    }

    public string Original { get; }

    public string Normalized { get; }

    public string Masked { get; }

    public static PhoneNumber Create(string value)
    {
        var cleaned = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!PhoneRegex().IsMatch(cleaned))
        {
            throw new InvalidPhoneException();
        }

        var normalized = cleaned.StartsWith("+", StringComparison.Ordinal) ? cleaned : $"+{cleaned}";
        return new PhoneNumber(value, normalized, Mask(normalized));
    }

    public static string Mask(string normalized)
    {
        if (normalized.Length <= 7)
        {
            return "***";
        }

        return $"{normalized[..4]}****{normalized[^3..]}";
    }

    [GeneratedRegex(@"^\+?[1-9]\d{7,14}$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();
}
