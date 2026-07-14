using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository users;
    private readonly IPasswordHasher passwordHasher;
    private readonly IVaultSecretProvider vault;
    private readonly IAuditLogService audit;
    private readonly IOutboxEventWriter outbox;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;
    private readonly PasswordPolicy passwordPolicy = new();

    public RegisterUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IVaultSecretProvider vault,
        IAuditLogService audit,
        IOutboxEventWriter outbox,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.users = users;
        this.passwordHasher = passwordHasher;
        this.vault = vault;
        this.audit = audit;
        this.outbox = outbox;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    public async Task<AuthResult<RegisterUserResult>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var phoneNumber = PhoneNumber.Create(command.PhoneNumber);
            passwordPolicy.EnsureValid(command.Password, phoneNumber);

            if (await users.PhoneExistsAsync(phoneNumber.Normalized, cancellationToken))
            {
                return AuthResult<RegisterUserResult>.Failure(AuthError.PhoneExists);
            }

            var secret = await vault.ReadPasswordSecretAsync(null, cancellationToken);
            var hash = await passwordHasher.HashPasswordAsync(command.Password, secret, cancellationToken);
            var user = User.Register(phoneNumber, new PasswordHash(hash.Hash, hash.Algorithm, hash.PepperVersion), clock.UtcNow);

            await users.AddAsync(user, cancellationToken);
            await audit.WriteRegisterSuccessAsync(user, command.CorrelationId, cancellationToken);
            await outbox.WriteUserRegisteredAsync(user, command.CorrelationId, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return AuthResult<RegisterUserResult>.Success(new RegisterUserResult(user.UserId.Value, user.PhoneNumber.Masked, user.Status.ToString()));
        }
        catch (InvalidPhoneException)
        {
            return AuthResult<RegisterUserResult>.Failure(AuthError.InvalidPhone);
        }
        catch (WeakPasswordException)
        {
            return AuthResult<RegisterUserResult>.Failure(AuthError.WeakPassword);
        }
    }
}
