using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XanhNow.Auth.Login.Infrastructure.Kafka;

namespace XanhNow.Auth.Login.Infrastructure.Outbox;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly OutboxOptions options;
    private readonly ILogger<OutboxDispatcherHostedService> logger;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        OutboxOptions options,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchOnceAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            var published = await dispatcher.DispatchOnceAsync(stoppingToken);
            if (published > 0)
            {
                logger.LogInformation("Published {PublishedCount} auth outbox event(s) to Kafka.", published);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth outbox dispatch failed.");
        }
    }
}