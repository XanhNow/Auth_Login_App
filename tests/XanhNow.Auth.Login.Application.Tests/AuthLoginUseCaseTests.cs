using System.Reflection;
using XanhNow.Auth.Login.Api.Contracts;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Application.UseCases;
using XanhNow.Auth.Login.Domain;
using XanhNow.Auth.Login.Domain.Users;
using XanhNow.Auth.Login.Infrastructure;
using XanhNow.Auth.Login.Infrastructure.Security;

namespace XanhNow.Auth.Login.Application.Tests;

public sealed class AuthLoginUseCaseTests
{
    [Fact]
    public void PasswordPolicy_BlocksWeakPassword()
    {
        var policy = new PasswordPolicy();
        var phone = PhoneNumber.Create("+84988888888");

        Assert.Throws<WeakPasswordException>(() => policy.EnsureValid("123456", phone));
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnsSessionOnly()
    {
        var deps = CreateDependencies();
        var register = new RegisterUserHandler(deps.Users, deps.Hasher, deps.Vault, deps.Audit, deps.UnitOfWork, deps.Clock);
        var login = new LoginUserHandler(deps.Users, deps.Sessions, deps.Hasher, deps.Vault, deps.RateLimit, deps.Audit, deps.UnitOfWork, deps.Clock);

        var registerResult = await register.HandleAsync(
            new RegisterUserCommand("+84988888888", "StrongPass2026!", "req-test-1"),
            CancellationToken.None);
        var loginResult = await login.HandleAsync(
            new LoginUserCommand("+84988888888", "StrongPass2026!", "127.0.0.1", "ua-hash", "req-test-2"),
            CancellationToken.None);

        Assert.True(registerResult.Succeeded);
        Assert.True(loginResult.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Value!.SessionId));
        Assert.True(loginResult.Value.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void LoginResponseContract_HasNoTokenFields()
    {
        var names = typeof(LoginResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains("UserId", names);
        Assert.Contains("SessionId", names);
        Assert.Contains("ExpiresAt", names);
        Assert.DoesNotContain("AccessToken", names);
        Assert.DoesNotContain("RefreshToken", names);
        Assert.DoesNotContain("Jwt", names);
        Assert.DoesNotContain("IdToken", names);
        Assert.DoesNotContain("BearerToken", names);
    }

    private static TestDependencies CreateDependencies()
    {
        return new TestDependencies(
            new FakeUserRepository(),
            new FakeSessionCache(),
            new Pbkdf2PasswordHasher(),
            new FakeVaultSecretProvider(),
            new FakeRateLimitService(),
            new FakeAuditLogService(),
            new FakeUnitOfWork(),
            new SystemClock());
    }

    private sealed record TestDependencies(
        IUserRepository Users,
        ISessionCache Sessions,
        IPasswordHasher Hasher,
        IVaultSecretProvider Vault,
        IRateLimitService RateLimit,
        IAuditLogService Audit,
        IUnitOfWork UnitOfWork,
        IClock Clock);

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> byId = [];
        private readonly Dictionary<string, Guid> byPhone = [];

        public Task<User?> FindByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(byPhone.TryGetValue(normalizedPhoneNumber, out var userId) ? byId[userId] : null);
        }

        public Task<User?> FindByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(byId.GetValueOrDefault(userId.Value));
        }

        public Task<bool> PhoneExistsAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(byPhone.ContainsKey(normalizedPhoneNumber));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            byId[user.UserId.Value] = user;
            byPhone[user.PhoneNumber.Normalized] = user.UserId.Value;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken)
        {
            byId[user.UserId.Value] = user;
            byPhone[user.PhoneNumber.Normalized] = user.UserId.Value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionCache : ISessionCache
    {
        private readonly Dictionary<string, SessionRecord> sessions = [];

        public Task CreateSessionAsync(string sessionId, SessionRecord session, TimeSpan ttl, CancellationToken cancellationToken)
        {
            sessions[sessionId] = session;
            return Task.CompletedTask;
        }

        public Task<SessionRecord?> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(sessions.GetValueOrDefault(sessionId));
        }

        public Task RefreshSessionAsync(string sessionId, DateTimeOffset expiresAt, TimeSpan ttl, CancellationToken cancellationToken)
        {
            if (sessions.TryGetValue(sessionId, out var session))
            {
                sessions[sessionId] = session with { ExpiresAt = expiresAt };
            }

            return Task.CompletedTask;
        }

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task UntrackUserSessionAsync(UserId userId, string sessionId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAllSessionsOfUserAsync(UserId userId, CancellationToken cancellationToken)
        {
            foreach (var key in sessions.Where(item => item.Value.UserId == userId).Select(item => item.Key).ToArray())
            {
                sessions.Remove(key);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeVaultSecretProvider : IVaultSecretProvider
    {
        public Task<PostgresSecret> ReadPostgresSecretAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PostgresSecret("Host=localhost;Database=test"));
        }

        public Task<RedisSecret> ReadRedisSecretAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new RedisSecret("test", false));
        }

        public Task<PasswordSecret> ReadPasswordSecretAsync(string? pepperVersion, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PasswordSecret("test-pepper", pepperVersion ?? "test-v1", "PBKDF2-SHA256"));
        }
    }

    private sealed class FakeRateLimitService : IRateLimitService
    {
        public Task<RateLimitDecision> CheckLoginAllowedAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RateLimitDecision(true, null));
        }

        public Task RecordFailedAttemptAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RecordSuccessAsync(string normalizedPhoneNumber, string clientIp, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditLogService : IAuditLogService
    {
        public Task WriteRegisterSuccessAsync(User user, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteLoginSuccessAsync(User user, string sessionIdHash, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteLoginFailedAsync(User? user, string phoneNumberMasked, string reasonCode, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteLogoutAsync(UserId? userId, string sessionIdHash, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteSessionInvalidAsync(string sessionIdHash, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteRateLimitedAsync(string phoneNumberMasked, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
