using StackExchange.Redis;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Application.UseCases;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Infrastructure.Redis;

public sealed class RedisSessionCache : ISessionCache
{
    private static readonly TimeSpan UserSessionsTtl = TimeSpan.FromHours(24);
    private readonly IConnectionMultiplexer redis;
    private readonly RedisOptions options;

    public RedisSessionCache(IConnectionMultiplexer redis, RedisOptions options)
    {
        this.redis = redis;
        this.options = options;
    }

    public async Task CreateSessionAsync(string sessionId, SessionRecord session, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var sessionKey = SessionKey(sessionId);
        var userSessionsKey = UserSessionsKey(session.UserId);
        var sessionHash = LoginUserHandler.Sha256(sessionId);

        await db.StringSetAsync(sessionKey, RedisSessionSerializer.Serialize(session), ttl);
        await db.SetAddAsync(userSessionsKey, sessionHash);
        await db.KeyExpireAsync(userSessionsKey, UserSessionsTtl);
    }

    public async Task<SessionRecord?> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().StringGetAsync(SessionKey(sessionId));
        return value.HasValue ? RedisSessionSerializer.Deserialize(value!) : null;
    }

    public async Task RefreshSessionAsync(string sessionId, DateTimeOffset expiresAt, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = SessionKey(sessionId);
        var value = await db.StringGetAsync(key);
        if (!value.HasValue)
        {
            return;
        }

        var session = RedisSessionSerializer.Deserialize(value!);
        await db.StringSetAsync(key, RedisSessionSerializer.Serialize(session with { ExpiresAt = expiresAt }), ttl);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        return redis.GetDatabase().KeyDeleteAsync(SessionKey(sessionId));
    }

    public Task UntrackUserSessionAsync(UserId userId, string sessionId, CancellationToken cancellationToken)
    {
        return redis.GetDatabase().SetRemoveAsync(UserSessionsKey(userId), LoginUserHandler.Sha256(sessionId));
    }

    public async Task DeleteAllSessionsOfUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var userSessionsKey = UserSessionsKey(userId);
        var hashes = await db.SetMembersAsync(userSessionsKey);
        foreach (var hash in hashes)
        {
            await db.KeyDeleteAsync($"{options.KeyPrefix}:session:{hash}");
        }

        await db.KeyDeleteAsync(userSessionsKey);
    }

    private string SessionKey(string sessionId) => $"{options.KeyPrefix}:session:{LoginUserHandler.Sha256(sessionId)}";

    private string UserSessionsKey(UserId userId) => $"{options.KeyPrefix}:user-sessions:{userId.Value}";
}
