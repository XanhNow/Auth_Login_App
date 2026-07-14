namespace XanhNow.Auth.Login.Domain;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

public sealed class InvalidPhoneException : DomainException
{
    public InvalidPhoneException() : base("Phone number is invalid.")
    {
    }
}

public sealed class WeakPasswordException : DomainException
{
    public WeakPasswordException(string message) : base(message)
    {
    }
}

public sealed class AccountLockedException : DomainException
{
    public AccountLockedException() : base("Account is locked.")
    {
    }
}

public sealed class AccountDisabledException : DomainException
{
    public AccountDisabledException() : base("Account is disabled.")
    {
    }
}
