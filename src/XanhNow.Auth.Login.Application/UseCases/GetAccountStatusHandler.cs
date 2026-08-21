using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class GetAccountStatusHandler
{
    private readonly IUserRepository users;
    private readonly IClock clock;

    public GetAccountStatusHandler(IUserRepository users, IClock clock)
    {
        this.users = users;
        this.clock = clock;
    }

    public async Task<AuthResult<AccountStatusResult>> HandleAsync(GetAccountStatusQuery query, CancellationToken cancellationToken)
    {
        UserId userId;
        try
        {
            userId = UserId.From(query.UserId);
        }
        catch (DomainException)
        {
            return AuthResult<AccountStatusResult>.Failure(AuthError.InvalidRequest);
        }

        var user = await users.FindByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return AuthResult<AccountStatusResult>.Failure(AuthError.InvalidCredentials);
        }

        return AuthResult<AccountStatusResult>.Success(
            new AccountStatusResult(user.UserId.Value, user.PhoneNumber.Masked, user.Status.ToString(), clock.UtcNow));
    }
}

public sealed class GetAccountByPhoneHandler
{
    private readonly IUserRepository users;
    private readonly IClock clock;

    public GetAccountByPhoneHandler(IUserRepository users, IClock clock)
    {
        this.users = users;
        this.clock = clock;
    }

    public async Task<AuthResult<AccountStatusResult>> HandleAsync(GetAccountByPhoneQuery query, CancellationToken cancellationToken)
    {
        PhoneNumber phoneNumber;
        try
        {
            phoneNumber = PhoneNumber.Create(query.PhoneNumber);
        }
        catch (DomainException)
        {
            return AuthResult<AccountStatusResult>.Failure(AuthError.InvalidPhone);
        }

        var user = await users.FindByPhoneNumberAsync(phoneNumber.Normalized, cancellationToken);
        if (user is null)
        {
            return AuthResult<AccountStatusResult>.Failure(AuthError.InvalidCredentials);
        }

        return AuthResult<AccountStatusResult>.Success(
            new AccountStatusResult(user.UserId.Value, user.PhoneNumber.Masked, user.Status.ToString(), clock.UtcNow));
    }
}
