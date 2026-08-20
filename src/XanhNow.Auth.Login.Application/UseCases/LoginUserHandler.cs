using System.Security.Cryptography;
using System.Text;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Domain;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Application.UseCases;

public sealed class LoginUserHandler
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(24);

    private readonly IUserRepository users;
    private readonly ISessionCache sessions;
    private readonly IPasswordHasher passwordHasher;
    private readonly IVaultSecretProvider vault;
    private readonly IRateLimitService rateLimit;
    private readonly IAuditLogService audit;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    public LoginUserHandler(
        IUserRepository users,
        ISessionCache sessions,
        IPasswordHasher passwordHasher,
        IVaultSecretProvider vault,
        IRateLimitService rateLimit,
        IAuditLogService audit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.users = users;
        this.sessions = sessions;
        this.passwordHasher = passwordHasher;
        this.vault = vault;
        this.rateLimit = rateLimit;
        this.audit = audit;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    public async Task<AuthResult<LoginUserResult>> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken)
    {
        PhoneNumber phoneNumber;
        try
        {
            phoneNumber = PhoneNumber.Create(command.PhoneNumber);
        }
        catch (InvalidPhoneException)
        {
            return AuthResult<LoginUserResult>.Failure(AuthError.InvalidPhone);
        }

        var decision = await rateLimit.CheckLoginAllowedAsync(phoneNumber.Normalized, command.ClientIp, cancellationToken);
        if (!decision.Allowed)
        {
            await audit.WriteRateLimitedAsync(phoneNumber.Masked, command.CorrelationId, cancellationToken);
            return AuthResult<LoginUserResult>.Failure(AuthError.TooManyAttempts);
        }

        var user = await users.FindByPhoneNumberAsync(phoneNumber.Normalized, cancellationToken);
        if (user is null)
        {
            await rateLimit.RecordFailedAttemptAsync(phoneNumber.Normalized, command.ClientIp, cancellationToken);
            await audit.WriteLoginFailedAsync(null, phoneNumber.Masked, "InvalidCredentials", command.CorrelationId, cancellationToken);
            return AuthResult<LoginUserResult>.Failure(AuthError.InvalidCredentials);
        }

        try
        {
            user.EnsureCanLogin(clock.UtcNow);
        }
        catch (AccountLockedException)
        {
            await audit.WriteLoginFailedAsync(user, phoneNumber.Masked, "Locked", command.CorrelationId, cancellationToken);
            return AuthResult<LoginUserResult>.Failure(AuthError.AccountLocked);
        }
        catch (AccountDisabledException)
        {
            await audit.WriteLoginFailedAsync(user, phoneNumber.Masked, "Disabled", command.CorrelationId, cancellationToken);
            return AuthResult<LoginUserResult>.Failure(AuthError.InvalidCredentials);
        }

        var secret = await vault.ReadPasswordSecretAsync(user.PasswordHash.PepperVersion, cancellationToken);
        var verified = await passwordHasher.VerifyPasswordAsync(command.Password, user.PasswordHash, secret, cancellationToken);
        if (!verified)
        {
            user.MarkLoginFailed(clock.UtcNow);
            await users.UpdateAsync(user, cancellationToken);
            await rateLimit.RecordFailedAttemptAsync(phoneNumber.Normalized, command.ClientIp, cancellationToken);
            await audit.WriteLoginFailedAsync(user, phoneNumber.Masked, "InvalidCredentials", command.CorrelationId, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return AuthResult<LoginUserResult>.Failure(AuthError.InvalidCredentials);
        }

        var sessionId = CreateOpaqueSessionId();
        var now = clock.UtcNow;
        var expiresAt = now.Add(SessionTtl);
        var absoluteExpiresAt = now.Add(AbsoluteLifetime);
        var sessionIdHash = Sha256(sessionId);
        var session = new SessionRecord(
            sessionIdHash,
            user.UserId,
            user.PhoneNumber.Masked,
            now,
            expiresAt,
            absoluteExpiresAt,
            command.UserAgentHash,
            command.CorrelationId);

        user.MarkLoginSuccess(now);
        await users.UpdateAsync(user, cancellationToken);
        await sessions.CreateSessionAsync(sessionId, session, SessionTtl, cancellationToken);
        await rateLimit.RecordSuccessAsync(phoneNumber.Normalized, command.ClientIp, cancellationToken);
        await audit.WriteLoginSuccessAsync(user, sessionIdHash, command.CorrelationId, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return AuthResult<LoginUserResult>.Success(new LoginUserResult(user.UserId.Value, sessionId, expiresAt, "password"));
    }

    public static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string CreateOpaqueSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');
    }
}
