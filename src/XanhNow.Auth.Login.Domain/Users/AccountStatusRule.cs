namespace XanhNow.Auth.Login.Domain.Users;

public static class AccountStatusRule
{
    public static void EnsureActive(User user, DateTimeOffset now)
    {
        if (user.Status == UserStatus.Active)
        {
            return;
        }

        if (user.Status == UserStatus.Locked)
        {
            throw new AccountLockedException();
        }

        throw new AccountDisabledException();
    }
}
