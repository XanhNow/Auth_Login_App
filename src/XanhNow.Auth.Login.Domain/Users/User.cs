namespace XanhNow.Auth.Login.Domain.Users;

public sealed class User
{
    private User()
    {
        PhoneNumber = null!;
        PasswordHash = null!;
    }

    private User(UserId userId, PhoneNumber phoneNumber, PasswordHash passwordHash, DateTimeOffset createdAt)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public UserId UserId { get; private set; }

    public PhoneNumber PhoneNumber { get; private set; }

    public PasswordHash PasswordHash { get; private set; }

    public UserStatus Status { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(PhoneNumber phoneNumber, PasswordHash passwordHash, DateTimeOffset now) =>
        new(UserId.New(), phoneNumber, passwordHash, now);

    public static User Rehydrate(
        UserId userId,
        PhoneNumber phoneNumber,
        PasswordHash passwordHash,
        UserStatus status,
        int failedLoginCount,
        DateTimeOffset? lockedUntil,
        DateTimeOffset? lastLoginAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new User
        {
            UserId = userId,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHash,
            Status = status,
            FailedLoginCount = failedLoginCount,
            LockedUntil = lockedUntil,
            LastLoginAt = lastLoginAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void EnsureCanLogin(DateTimeOffset now)
    {
        AccountStatusRule.EnsureActive(this, now);
    }

    public void MarkLoginSuccess(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LastLoginAt = now;
        UpdatedAt = now;
    }

    public void MarkLoginFailed(DateTimeOffset now)
    {
        FailedLoginCount++;
        UpdatedAt = now;
    }

    public void LockUntil(DateTimeOffset lockedUntil, DateTimeOffset now)
    {
        Status = UserStatus.Locked;
        LockedUntil = lockedUntil;
        UpdatedAt = now;
    }

    public void ChangePhone(PhoneNumber newPhoneNumber, DateTimeOffset now)
    {
        PhoneNumber = newPhoneNumber;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Status = UserStatus.Disabled;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now)
    {
        Status = UserStatus.Deleted;
        UpdatedAt = now;
    }
}
