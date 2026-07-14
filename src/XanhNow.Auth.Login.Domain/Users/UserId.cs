namespace XanhNow.Auth.Login.Domain.Users;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("UserId must not be empty.");
        }

        return new UserId(value);
    }

    public override string ToString() => Value.ToString();
}
