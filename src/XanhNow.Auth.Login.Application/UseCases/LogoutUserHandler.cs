using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class LogoutUserHandler
{
    private readonly ISessionCache sessions;
    private readonly IAuditLogService audit;
    private readonly IUnitOfWork unitOfWork;

    public LogoutUserHandler(ISessionCache sessions, IAuditLogService audit, IUnitOfWork unitOfWork)
    {
        this.sessions = sessions;
        this.audit = audit;
        this.unitOfWork = unitOfWork;
    }

    public async Task<AuthResult<LogoutUserResult>> HandleAsync(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetSessionAsync(command.SessionId, cancellationToken);
        var sessionIdHash = LoginUserHandler.Sha256(command.SessionId);

        await sessions.DeleteSessionAsync(command.SessionId, cancellationToken);

        if (session is not null)
        {
            await sessions.UntrackUserSessionAsync(session.UserId, command.SessionId, cancellationToken);
            await audit.WriteLogoutAsync(session.UserId, sessionIdHash, command.CorrelationId, cancellationToken);
        }
        else
        {
            await audit.WriteLogoutAsync(null, sessionIdHash, command.CorrelationId, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return AuthResult<LogoutUserResult>.Success(new LogoutUserResult("Logged out"));
    }
}
