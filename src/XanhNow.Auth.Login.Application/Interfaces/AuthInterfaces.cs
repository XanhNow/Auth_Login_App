using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken);

    Task<User?> FindByUserIdAsync(UserId userId, CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(string normalizedPhoneNumber, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task UpdateAsync(User user, CancellationToken cancellationToken);
}

public interface ISessionCache
{
    Task CreateSessionAsync(string sessionId, SessionRecord session, TimeSpan ttl, CancellationToken cancellationToken);

    Task<SessionRecord?> GetSessionAsync(string sessionId, CancellationToken cancellationToken);

    Task RefreshSessionAsync(string sessionId, DateTimeOffset expiresAt, TimeSpan ttl, CancellationToken cancellationToken);

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);

    Task UntrackUserSessionAsync(UserId userId, string sessionId, CancellationToken cancellationToken);

    Task DeleteAllSessionsOfUserAsync(UserId userId, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    Task<PasswordHashResult> HashPasswordAsync(string password, PasswordSecret secret, CancellationToken cancellationToken);

    Task<bool> VerifyPasswordAsync(string password, PasswordHash hash, PasswordSecret secret, CancellationToken cancellationToken);

    bool NeedsRehash(PasswordHash hash, PasswordSecret secret);
}

public interface IRateLimitService
{
    Task<RateLimitDecision> CheckLoginAllowedAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken);

    Task RecordFailedAttemptAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken);

    Task RecordSuccessAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken);
}

public interface IAuditLogService
{
    Task WriteRegisterSuccessAsync(User user, string correlationId, CancellationToken cancellationToken);

    Task WriteLoginSuccessAsync(User user, string sessionIdHash, string correlationId, CancellationToken cancellationToken);

    Task WriteLoginFailedAsync(User? user, string phoneNumberMasked, string reasonCode, string correlationId, CancellationToken cancellationToken);

    Task WriteLogoutAsync(UserId? userId, string sessionIdHash, string correlationId, CancellationToken cancellationToken);

    Task WriteSessionInvalidAsync(string sessionIdHash, string correlationId, CancellationToken cancellationToken);

    Task WriteRateLimitedAsync(string phoneNumberMasked, string correlationId, CancellationToken cancellationToken);
}

public interface IVaultSecretProvider
{
    Task<PostgresSecret> ReadPostgresSecretAsync(CancellationToken cancellationToken);

    Task<RedisSecret> ReadRedisSecretAsync(CancellationToken cancellationToken);

    Task<PasswordSecret> ReadPasswordSecretAsync(string? pepperVersion, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}
