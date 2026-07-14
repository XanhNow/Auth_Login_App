using Microsoft.EntityFrameworkCore;
using XanhNow.Auth.Login.Application.Interfaces;
using XanhNow.Auth.Login.Domain.Users;

namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class EfUserRepository : IUserRepository
{
    private readonly AuthDbContext dbContext;

    public EfUserRepository(AuthDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<User?> FindByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
    {
        var record = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.PhoneNumberNormalized == normalizedPhoneNumber, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<User?> FindByUserIdAsync(UserId userId, CancellationToken cancellationToken)
    {
        var record = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserId == userId.Value, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public Task<bool> PhoneExistsAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
    {
        return dbContext.Users.AnyAsync(user => user.PhoneNumberNormalized == normalizedPhoneNumber, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbContext.Users.AddAsync(ToRecord(user), cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Users.SingleAsync(record => record.UserId == user.UserId.Value, cancellationToken);
        existing.PhoneNumber = user.PhoneNumber.Original;
        existing.PhoneNumberNormalized = user.PhoneNumber.Normalized;
        existing.PhoneNumberMasked = user.PhoneNumber.Masked;
        existing.PasswordHash = user.PasswordHash.Value;
        existing.PasswordAlgorithm = user.PasswordHash.Algorithm;
        existing.PasswordPepperVersion = user.PasswordHash.PepperVersion;
        existing.Status = user.Status.ToString();
        existing.FailedLoginCount = user.FailedLoginCount;
        existing.LockedUntil = user.LockedUntil;
        existing.LastLoginAt = user.LastLoginAt;
        existing.UpdatedAt = user.UpdatedAt;
    }

    private static User ToDomain(UserRecord record)
    {
        return User.Rehydrate(
            UserId.From(record.UserId),
            PhoneNumber.Create(record.PhoneNumberNormalized),
            new PasswordHash(record.PasswordHash, record.PasswordAlgorithm, record.PasswordPepperVersion),
            Enum.Parse<UserStatus>(record.Status, ignoreCase: true),
            record.FailedLoginCount,
            record.LockedUntil,
            record.LastLoginAt,
            record.CreatedAt,
            record.UpdatedAt);
    }

    private static UserRecord ToRecord(User user)
    {
        return new UserRecord
        {
            UserId = user.UserId.Value,
            PhoneNumber = user.PhoneNumber.Original,
            PhoneNumberNormalized = user.PhoneNumber.Normalized,
            PhoneNumberMasked = user.PhoneNumber.Masked,
            PasswordHash = user.PasswordHash.Value,
            PasswordAlgorithm = user.PasswordHash.Algorithm,
            PasswordPepperVersion = user.PasswordHash.PepperVersion,
            Status = user.Status.ToString(),
            FailedLoginCount = user.FailedLoginCount,
            LockedUntil = user.LockedUntil,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
