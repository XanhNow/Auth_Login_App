using System.Security.Cryptography;
using System.Text;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class EfAuditLogService : IAuditLogService
{
    private readonly AuthDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public EfAuditLogService(AuthDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public Task WriteRegisterSuccessAsync(User user, string correlationId, CancellationToken cancellationToken)
    {
        Add(user.UserId.Value, "RegisterSuccess", "Info", user.PhoneNumber.Masked, null, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteLoginSuccessAsync(User user, string sessionIdHash, string correlationId, CancellationToken cancellationToken)
    {
        Add(user.UserId.Value, "LoginSuccess", "Info", user.PhoneNumber.Masked, sessionIdHash, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteLoginFailedAsync(User? user, string phoneNumberMasked, string reasonCode, string correlationId, CancellationToken cancellationToken)
    {
        Add(user?.UserId.Value, "LoginFailed", "Warning", phoneNumberMasked, null, correlationId, $$"""{"reasonCode":"{{reasonCode}}"}""");
        return Task.CompletedTask;
    }

    public Task WriteLogoutAsync(UserId? userId, string sessionIdHash, string correlationId, CancellationToken cancellationToken)
    {
        Add(userId?.Value, "Logout", "Info", null, sessionIdHash, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteSessionInvalidAsync(string sessionIdHash, string correlationId, CancellationToken cancellationToken)
    {
        Add(null, "SessionInvalid", "Warning", null, sessionIdHash, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteRateLimitedAsync(string phoneNumberMasked, string correlationId, CancellationToken cancellationToken)
    {
        Add(null, "RateLimited", "Warning", phoneNumberMasked, null, correlationId);
        return Task.CompletedTask;
    }

    private void Add(Guid? userId, string eventType, string severity, string? phoneMasked, string? sessionIdHash, string correlationId, string metadata = "{}")
    {
        dbContext.AuthAuditLogs.Add(new AuthAuditLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            Severity = severity,
            PhoneNumberMasked = phoneMasked,
            SessionIdHash = sessionIdHash,
            MetadataJson = metadata,
            CreatedAt = timeProvider.GetUtcNow(),
            CorrelationId = correlationId
        });
    }

    public static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
