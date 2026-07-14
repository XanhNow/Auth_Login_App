using System.Text.Json;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Infrastructure.Redis;

public static class RedisSessionSerializer
{
    public static string Serialize(SessionRecord session)
    {
        return JsonSerializer.Serialize(new RedisSessionDto(
            session.SessionIdHash,
            session.UserId.Value,
            session.PhoneNumberMasked,
            session.CreatedAt,
            session.ExpiresAt,
            session.AbsoluteExpiresAt,
            session.ClientInfoHash,
            session.CorrelationId));
    }

    public static SessionRecord Deserialize(string value)
    {
        var dto = JsonSerializer.Deserialize<RedisSessionDto>(value)
            ?? throw new InvalidOperationException("Redis session payload is invalid.");
        return new SessionRecord(
            dto.SessionIdHash,
            UserId.From(dto.UserId),
            dto.PhoneNumberMasked,
            dto.CreatedAt,
            dto.ExpiresAt,
            dto.AbsoluteExpiresAt,
            dto.ClientInfoHash,
            dto.CorrelationId);
    }

    private sealed record RedisSessionDto(
        string SessionIdHash,
        Guid UserId,
        string PhoneNumberMasked,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset AbsoluteExpiresAt,
        string? ClientInfoHash,
        string CorrelationId);
}
