using System.Text.Json;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Domain.Users;
using XanhNow.Auth.Login.Infrastructure.Persistence;

namespace XanhNow.Auth.Login.Infrastructure.Outbox;

public sealed class EfOutboxEventWriter : IOutboxEventWriter
{
    private readonly AuthDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public EfOutboxEventWriter(AuthDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public Task WriteUserRegisteredAsync(User user, string correlationId, CancellationToken cancellationToken)
    {
        Add("UserRegistered", "User", user.UserId.Value.ToString(), new
        {
            eventId = Guid.NewGuid(),
            eventType = "UserRegistered",
            eventVersion = 1,
            occurredAt = timeProvider.GetUtcNow(),
            producer = "auth-login",
            correlationId,
            userId = user.UserId.Value,
            phoneNumberMasked = user.PhoneNumber.Masked
        }, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteUserLoggedInAsync(User user, string sessionIdHash, string correlationId, CancellationToken cancellationToken)
    {
        Add("UserLoggedIn", "User", user.UserId.Value.ToString(), new
        {
            eventId = Guid.NewGuid(),
            eventType = "UserLoggedIn",
            eventVersion = 1,
            occurredAt = timeProvider.GetUtcNow(),
            producer = "auth-login",
            correlationId,
            userId = user.UserId.Value,
            phoneNumberMasked = user.PhoneNumber.Masked,
            sessionIdHash
        }, correlationId);
        return Task.CompletedTask;
    }

    public Task WriteUserLoggedOutAsync(UserId userId, string sessionIdHash, string correlationId, CancellationToken cancellationToken)
    {
        Add("UserLoggedOut", "Session", sessionIdHash, new
        {
            eventId = Guid.NewGuid(),
            eventType = "UserLoggedOut",
            eventVersion = 1,
            occurredAt = timeProvider.GetUtcNow(),
            producer = "auth-login",
            correlationId,
            userId = userId.Value,
            sessionIdHash
        }, correlationId);
        return Task.CompletedTask;
    }

    private void Add(string eventType, string aggregateType, string aggregateId, object payload, string correlationId)
    {
        var now = timeProvider.GetUtcNow();
        dbContext.OutboxEvents.Add(new OutboxEventRecord
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = "Pending",
            RetryCount = 0,
            AvailableAt = now,
            CreatedAt = now,
            CorrelationId = correlationId
        });
    }
}
