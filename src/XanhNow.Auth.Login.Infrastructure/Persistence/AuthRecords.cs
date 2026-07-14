namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class UserRecord
{
    public Guid UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string PhoneNumberNormalized { get; set; } = string.Empty;
    public string PhoneNumberMasked { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordAlgorithm { get; set; } = string.Empty;
    public string PasswordPepperVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UserPhoneHistoryRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OldPhoneNumberMasked { get; set; } = string.Empty;
    public string OldPhoneNumberHash { get; set; } = string.Empty;
    public string NewPhoneNumberMasked { get; set; } = string.Empty;
    public string NewPhoneNumberHash { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class LoginAttemptRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string PhoneNumberHash { get; set; } = string.Empty;
    public string PhoneNumberMasked { get; set; } = string.Empty;
    public string IpHash { get; set; } = string.Empty;
    public string? ClientInfoHash { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? FailureReasonCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class AuthAuditLogRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? PhoneNumberMasked { get; set; }
    public string? SessionIdHash { get; set; }
    public string? IpHash { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class OutboxEventRecord
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
