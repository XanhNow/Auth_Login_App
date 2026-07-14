using XanhNow.Auth.Login.Application.Interfaces;

namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AuthDbContext dbContext;

    public EfUnitOfWork(AuthDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
