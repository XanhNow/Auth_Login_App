using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class ValidateSessionHandler
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);

    private readonly ISessionCache sessions;
    private readonly IUserRepository users;
    private readonly IAuditLogService audit;
    private readonly IClock clock;

    public ValidateSessionHandler(ISessionCache sessions, IUserRepository users, IAuditLogService audit, IClock clock)
    {
        this.sessions = sessions;
        this.users = users;
        this.audit = audit;
        this.clock = clock;
    }

    public async Task<AuthResult<ValidateSessionResult>> HandleAsync(ValidateSessionQuery query, CancellationToken cancellationToken)
    {
        var session = await sessions.GetSessionAsync(query.SessionId, cancellationToken);
        var sessionIdHash = LoginUserHandler.Sha256(query.SessionId);
        if (session is null || session.AbsoluteExpiresAt <= clock.UtcNow)
        {
            await sessions.DeleteSessionAsync(query.SessionId, cancellationToken);
            await audit.WriteSessionInvalidAsync(sessionIdHash, query.CorrelationId, cancellationToken);
            return AuthResult<ValidateSessionResult>.Failure(AuthError.SessionInvalid);
        }

        var user = await users.FindByUserIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            await sessions.DeleteSessionAsync(query.SessionId, cancellationToken);
            return AuthResult<ValidateSessionResult>.Failure(AuthError.SessionInvalid);
        }

        try
        {
            user.EnsureCanLogin(clock.UtcNow);
        }
        catch
        {
            await sessions.DeleteSessionAsync(query.SessionId, cancellationToken);
            return AuthResult<ValidateSessionResult>.Failure(AuthError.SessionInvalid);
        }

        var expiresAt = clock.UtcNow.Add(SessionTtl);
        if (expiresAt > session.AbsoluteExpiresAt)
        {
            expiresAt = session.AbsoluteExpiresAt;
        }

        await sessions.RefreshSessionAsync(query.SessionId, expiresAt, expiresAt - clock.UtcNow, cancellationToken);
        return AuthResult<ValidateSessionResult>.Success(new ValidateSessionResult(true, user.UserId.Value, user.PhoneNumber.Masked, expiresAt));
    }
}
