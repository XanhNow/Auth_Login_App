using Microsoft.EntityFrameworkCore;
using XanhNow.Auth.Login.Infrastructure.Kafka;
using XanhNow.Auth.Login.Infrastructure.Persistence;

namespace XanhNow.Auth.Login.Infrastructure.Outbox;

public sealed class OutboxDispatcher
{
    private readonly AuthDbContext dbContext;
    private readonly KafkaAuthEventProducer producer;
    private readonly OutboxOptions options;
    private readonly TimeProvider timeProvider;

    public OutboxDispatcher(AuthDbContext dbContext, KafkaAuthEventProducer producer, OutboxOptions options, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.producer = producer;
        this.options = options;
        this.timeProvider = timeProvider;
    }

    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var events = await dbContext.OutboxEvents
            .Where(item => (item.Status == "Pending" || item.Status == "Failed") && item.AvailableAt <= now)
            .OrderBy(item => item.CreatedAt)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var item in events)
        {
            item.Status = "Publishing";
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var published = 0;
        foreach (var item in events)
        {
            try
            {
                await producer.ProduceAsync(item.AggregateId, item.PayloadJson, cancellationToken);
                item.Status = "Published";
                item.PublishedAt = timeProvider.GetUtcNow();
                published++;
            }
            catch
            {
                item.RetryCount++;
                item.Status = item.RetryCount >= options.MaxRetryCount ? "DeadLetter" : "Failed";
                item.AvailableAt = timeProvider.GetUtcNow().AddSeconds(Math.Min(300, item.RetryCount * 10));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return published;
    }
}
