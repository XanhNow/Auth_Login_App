namespace XanhNow.Auth.Login.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "192.168.2.14:9092,192.168.2.31:9092,192.168.2.51:9092";
    public string TopicName { get; set; } = "auth-events";
    public string ClientId { get; set; } = "auth-login-producer";
    public string Acks { get; set; } = "all";
    public bool EnableIdempotence { get; set; } = true;
    public string MessageKey { get; set; } = "user_id";
}

public sealed class OutboxOptions
{
    public string DispatcherId { get; set; } = "auth-login-outbox-dispatcher";
    public int BatchSize { get; set; } = 50;
    public int PollIntervalSeconds { get; set; } = 5;
    public int MaxRetryCount { get; set; } = 20;
}
