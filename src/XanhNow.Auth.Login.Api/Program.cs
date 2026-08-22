using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "XanhNow Auth Login API",
        Version = "v1",
        Description = "Independent REST Login Provider for XanhNow applications."
    });
});
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddScoped<LogoutUserHandler>();
builder.Services.AddScoped<ValidateSessionHandler>();
builder.Services.AddScoped<GetAccountStatusHandler>();
builder.Services.AddScoped<GetAccountByPhoneHandler>();
builder.Services.AddScoped<ChangeAccountStateHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

var infrastructureMode = builder.Configuration["Infrastructure:Mode"] ?? "PostgreSqlVault";
if (!string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(infrastructureMode, "RedisVault", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(infrastructureMode, "PostgreSqlVault", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Infrastructure mode must be PostgreSqlVault, RedisVault or Real. In-memory infrastructure is not supported.");
}

var runtimeSecretFiles = new RuntimeSecretFilesOptions();
builder.Configuration.GetSection("RuntimeSecrets").Bind(runtimeSecretFiles);

var vaultOptions = new VaultOptions();
builder.Configuration.GetSection("Vault").Bind(vaultOptions);

IVaultSecretProvider secretProvider = runtimeSecretFiles.HasPostgresFile ||
                                      runtimeSecretFiles.HasRedisFile ||
                                      runtimeSecretFiles.HasPasswordSecretFile
    ? new RuntimeSecretFileProvider(runtimeSecretFiles)
    : new VaultSecretProvider(new HttpClient { BaseAddress = new Uri(vaultOptions.Address) }, vaultOptions);
var postgresSecret = secretProvider.ReadPostgresSecretAsync(CancellationToken.None).GetAwaiter().GetResult();

builder.Services.AddSingleton(secretProvider);
builder.Services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(postgresSecret.ConnectionString));
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAuditLogService, EfAuditLogService>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

if (string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(infrastructureMode, "RedisVault", StringComparison.OrdinalIgnoreCase))
{
    var redisSecret = secretProvider.ReadRedisSecretAsync(CancellationToken.None).GetAwaiter().GetResult();

    var redisOptions = new RedisOptions();
    builder.Configuration.GetSection("Redis").Bind(redisOptions);
    redisOptions.Validate();

    var redisConfiguration = ConfigurationOptions.Parse(redisOptions.BootstrapEndpoints);
    redisConfiguration.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
    redisConfiguration.ConnectTimeout = redisOptions.ConnectTimeoutMs;
    redisConfiguration.SyncTimeout = redisOptions.OperationTimeoutMs;
    redisConfiguration.AsyncTimeout = redisOptions.OperationTimeoutMs;
    redisConfiguration.Password = redisSecret.Password;
    redisConfiguration.Ssl = redisSecret.TlsEnabled;

    builder.Services.AddSingleton(redisOptions);
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
    builder.Services.AddSingleton<ISessionCache, RedisSessionCache>();
    builder.Services.AddSingleton<IRateLimitService, RedisRateLimitService>();
}

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownProxies.Add(IPAddress.Parse("192.168.2.24"));
forwardedHeaders.KnownProxies.Add(IPAddress.Parse("192.168.2.64"));
app.UseForwardedHeaders(forwardedHeaders);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "XanhNow Auth Login API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    var enableHttpsRedirection = app.Configuration.GetValue("Http:EnableHttpsRedirection", false);
    if (enableHttpsRedirection)
    {
        app.UseHttpsRedirection();
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();

app.Run();
