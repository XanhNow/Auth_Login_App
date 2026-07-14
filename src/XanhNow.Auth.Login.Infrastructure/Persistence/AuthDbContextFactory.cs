using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=192.168.2.80;Port=15432;Database=authtest;Username=xanhnow_auth_migrator")
            .Options;

        return new AuthDbContext(options);
    }
}
