using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class ChangeAccountStateHandler
{
    private readonly IUserRepository users;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    public ChangeAccountStateHandler(IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        this.users = users;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    public async Task<AuthResult<AccountStateChangeResult>> HandleAsync(ChangeAccountStateCommand command, CancellationToken cancellationToken)
    {
        UserId userId;
        try
        {
            userId = UserId.From(command.UserId);
        }
        catch (DomainException)
        {
            return AuthResult<AccountStateChangeResult>.Failure(AuthError.InvalidRequest);
        }

        var user = await users.FindByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return AuthResult<AccountStateChangeResult>.Failure(AuthError.InvalidCredentials);
        }

        var now = clock.UtcNow;
        if (string.Equals(command.TargetState, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            user.Disable(now);
        }
        else if (string.Equals(command.TargetState, "Locked", StringComparison.OrdinalIgnoreCase))
        {
            user.LockUntil(now.AddMinutes(30), now);
        }
        else
        {
            return AuthResult<AccountStateChangeResult>.Failure(AuthError.InvalidRequest);
        }

        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return AuthResult<AccountStateChangeResult>.Success(new AccountStateChangeResult(user.UserId.Value, user.Status.ToString(), now));
    }
}
