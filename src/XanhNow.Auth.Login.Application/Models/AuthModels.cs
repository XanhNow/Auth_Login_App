using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.Models;

public sealed record RegisterUserCommand(string PhoneNumber, string Password, string CorrelationId);

public sealed record RegisterUserResult(Guid UserId, string PhoneNumberMasked, string Status);

public sealed record LoginUserCommand(
    string PhoneNumber,
    string Password,
    string ClientIp,
    string? UserAgentHash,
    string CorrelationId);

public sealed record LoginUserResult(Guid UserId, string SessionId, DateTimeOffset ExpiresAt);

public sealed record LogoutUserCommand(string SessionId, string CorrelationId);

public sealed record LogoutUserResult(string Message);

public sealed record ValidateSessionQuery(string SessionId, string CorrelationId);

public sealed record ValidateSessionResult(bool Valid, Guid UserId, string PhoneNumberMasked, DateTimeOffset ExpiresAt);

public sealed record GetAccountStatusQuery(Guid UserId);

public sealed record AccountStatusResult(Guid UserId, string MaskedPhoneNumber, string Status, DateTimeOffset UpdatedAtUtc);

public sealed record PasswordSecret(string Pepper, string PepperVersion, string Algorithm);

public sealed record PostgresSecret(string ConnectionString);

public sealed record RedisSecret(string Password, bool TlsEnabled);

public sealed record KafkaSecret(string? Username, string? Password, string? SecurityProtocol, string? SaslMechanism);

public sealed record PasswordHashResult(string Hash, string Algorithm, string PepperVersion);

public sealed record RateLimitDecision(bool Allowed, DateTimeOffset? RetryAfter);

public sealed record SessionRecord(
    string SessionIdHash,
    UserId UserId,
    string PhoneNumberMasked,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    string? ClientInfoHash,
    string CorrelationId);

public sealed record ChangeAccountStateCommand(Guid UserId, string TargetState, string ReasonCode, string? Comment);

public sealed record AccountStateChangeResult(Guid UserId, string Status, DateTimeOffset ChangedAtUtc);
