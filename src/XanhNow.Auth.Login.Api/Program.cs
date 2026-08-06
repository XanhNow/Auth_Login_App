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

var vaultOptions = new VaultOptions();
builder.Configuration.GetSection("Vault").Bind(vaultOptions);
var vaultHttpClient = new HttpClient { BaseAddress = new Uri(vaultOptions.Address) };
var vaultProvider = new VaultSecretProvider(vaultHttpClient, vaultOptions);
var postgresSecret = vaultProvider.ReadPostgresSecretAsync(CancellationToken.None).GetAwaiter().GetResult();

builder.Services.AddSingleton<IVaultSecretProvider>(vaultProvider);
builder.Services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(postgresSecret.ConnectionString));
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAuditLogService, EfAuditLogService>();
builder.Services.AddScoped<IOutboxEventWriter, EfOutboxEventWriter>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

if (string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(infrastructureMode, "RedisVault", StringComparison.OrdinalIgnoreCase))
{
    var redisSecret = vaultProvider.ReadRedisSecretAsync(CancellationToken.None).GetAwaiter().GetResult();

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

if (string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase))
{
    var kafkaSecret = vaultProvider.ReadKafkaSecretAsync(CancellationToken.None).GetAwaiter().GetResult();
    var kafkaOptions = new KafkaOptions();
    builder.Configuration.GetSection("Kafka").Bind(kafkaOptions);
    var outboxOptions = new OutboxOptions();
    builder.Configuration.GetSection("Outbox").Bind(outboxOptions);

    builder.Services.AddSingleton(kafkaOptions);
    builder.Services.AddSingleton(outboxOptions);
    builder.Services.AddSingleton(kafkaSecret);
    builder.Services.AddSingleton<KafkaAuthEventProducer>();
    builder.Services.AddScoped<OutboxDispatcher>();
    builder.Services.AddHostedService<OutboxDispatcherHostedService>();
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
app.MapGet("/health/live", LiveHealth);
app.MapGet("/api/health/live", LiveHealth);
app.MapGet("/health/ready", ReadyHealth);
app.MapGet("/api/health/ready", ReadyHealth);
app.MapGet("/api/edge-probe", (HttpContext context) =>
{
    var release = "unknown";
    var sourceSha = "unknown";
    var metadataPath = Path.Combine(AppContext.BaseDirectory, "release.json");
    if (File.Exists(metadataPath))
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            if (root.TryGetProperty("release_id", out var releaseProperty))
            {
                release = releaseProperty.GetString() ?? release;
            }

            if (root.TryGetProperty("source_sha", out var sourceShaProperty))
            {
                sourceSha = sourceShaProperty.GetString() ?? sourceSha;
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    var node = Environment.MachineName;
    context.Response.Headers["X-XanhNow-Api-Node"] = node;
    context.Response.Headers["X-XanhNow-Release"] = release;

    return Results.Ok(new
    {
        status = "ok",
        service = "xanhnow-auth-login",
        node,
        release,
        source_sha = sourceSha
    });
});

app.Run();

static IResult LiveHealth() => Results.Ok(new { status = "Healthy" });

async Task<IResult> ReadyHealth(IServiceProvider services, CancellationToken cancellationToken)
{
    var checks = new List<DependencyHealthCheck>();

    checks.Add(await CheckPostgreSqlAsync(services, cancellationToken));

    if (string.Equals(infrastructureMode, "RedisVault", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase))
    {
        checks.Add(await CheckRedisAsync(services));
    }

    if (string.Equals(infrastructureMode, "Real", StringComparison.OrdinalIgnoreCase))
    {
        checks.Add(await CheckKafkaAsync(services, cancellationToken));
    }

    var healthy = checks.All(check => check.Healthy);
    var body = new
    {
        status = healthy ? "Healthy" : "Unhealthy",
        mode = infrastructureMode,
        dependencies = checks
    };

    return healthy ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}

static async Task<DependencyHealthCheck> CheckPostgreSqlAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    try
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var connected = await dbContext.Database.CanConnectAsync(cancellationToken);
        return connected
            ? DependencyHealthCheck.Ok("postgres")
            : DependencyHealthCheck.Fail("postgres", "Database connection check returned false.");
    }
    catch (Exception ex)
    {
        return DependencyHealthCheck.Fail("postgres", ex.Message);
    }
}

static async Task<DependencyHealthCheck> CheckRedisAsync(IServiceProvider services)
{
    try
    {
        var redis = services.GetRequiredService<IConnectionMultiplexer>();
        var database = redis.GetDatabase();
        var pong = await database.PingAsync();
        return pong >= TimeSpan.Zero
            ? DependencyHealthCheck.Ok("redis")
            : DependencyHealthCheck.Fail("redis", "Redis ping did not return a valid latency.");
    }
    catch (Exception ex)
    {
        return DependencyHealthCheck.Fail("redis", ex.Message);
    }
}

static Task<DependencyHealthCheck> CheckKafkaAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    try
    {
        var options = services.GetRequiredService<KafkaOptions>();
        using var adminClient = new Confluent.Kafka.AdminClientBuilder(new Confluent.Kafka.AdminClientConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = $"{options.ClientId}-health"
        }).Build();

        var metadata = adminClient.GetMetadata(options.TopicName, TimeSpan.FromSeconds(5));
        var topic = metadata.Topics.SingleOrDefault(item => item.Topic == options.TopicName);
        if (topic is null || topic.Error.IsError)
        {
            return Task.FromResult(DependencyHealthCheck.Fail("kafka", topic?.Error.Reason ?? "Topic metadata was not found."));
        }

        return Task.FromResult(DependencyHealthCheck.Ok("kafka"));
    }
    catch (Exception ex)
    {
        return Task.FromResult(DependencyHealthCheck.Fail("kafka", ex.Message));
    }
}

public sealed record DependencyHealthCheck(string Name, bool Healthy, string Message)
{
    public static DependencyHealthCheck Ok(string name) => new(name, true, "ok");

    public static DependencyHealthCheck Fail(string name, string message) => new(name, false, message);
}
