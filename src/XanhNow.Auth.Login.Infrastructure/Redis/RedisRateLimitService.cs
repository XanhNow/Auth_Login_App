using StackExchange.Redis;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Application.UseCases;

namespace XanhNow.Auth.Login.Infrastructure.Redis;

public sealed class RedisRateLimitService : IRateLimitService
{
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TemporaryLock = TimeSpan.FromMinutes(15);
    private readonly IConnectionMultiplexer redis;
    private readonly RedisOptions options;

    public RedisRateLimitService(IConnectionMultiplexer redis, RedisOptions options)
    {
        this.redis = redis;
        this.options = options;
    }

    public async Task<RateLimitDecision> CheckLoginAllowedAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
    {
        var lockTtl = await redis.GetDatabase().KeyTimeToLiveAsync(LockKey(normalizedPhoneNumber));
        if (lockTtl is { } ttl && ttl > TimeSpan.Zero)
        {
            return new RateLimitDecision(false, DateTimeOffset.UtcNow.Add(ttl));
        }

        return new RateLimitDecision(true, null);
    }

    public async Task RecordFailedAttemptAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = AttemptKey(normalizedPhoneNumber, clientIp);
        var count = await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, AttemptWindow);
        if (count >= 5)
        {
            await db.StringSetAsync(LockKey(normalizedPhoneNumber), "1", TemporaryLock);
        }
    }

    public async Task RecordSuccessAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(AttemptKey(normalizedPhoneNumber, clientIp));
        await db.KeyDeleteAsync(LockKey(normalizedPhoneNumber));
    }

    private string AttemptKey(string phone, string ip)
    {
        return $"{options.KeyPrefix}:login-attempt:{LoginUserHandler.Sha256(phone)}:{LoginUserHandler.Sha256(ip)}";
    }

    private string LockKey(string phone)
    {
        return $"{options.KeyPrefix}:login-lock:{LoginUserHandler.Sha256(phone)}";
    }
}
