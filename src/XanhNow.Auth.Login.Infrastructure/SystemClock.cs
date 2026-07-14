using XanhNow.Auth.Login.Application.Interfaces;

namespace XanhNow.Auth.Login.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
