using Confluent.Kafka;
using XanhNow.Auth.Login.Application.Models;

namespace XanhNow.Auth.Login.Infrastructure.Kafka;

public sealed class KafkaAuthEventProducer : IDisposable
{
    private readonly IProducer<string, string> producer;
    private readonly KafkaOptions options;

    public KafkaAuthEventProducer(KafkaOptions options, KafkaSecret secret)
    {
        this.options = options;
        var config = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = options.ClientId,
            Acks = Acks.All,
            EnableIdempotence = options.EnableIdempotence
        };

        var securityProtocol = NormalizeOptional(secret.SecurityProtocol);
        var saslMechanism = NormalizeOptional(secret.SaslMechanism);
        var username = NormalizeOptional(secret.Username);
        var password = NormalizeOptional(secret.Password);

        if (!string.IsNullOrWhiteSpace(securityProtocol))
        {
            config.SecurityProtocol = Enum.Parse<SecurityProtocol>(securityProtocol, ignoreCase: true);
        }

        if (!string.IsNullOrWhiteSpace(saslMechanism))
        {
            config.SaslMechanism = Enum.Parse<SaslMechanism>(saslMechanism, ignoreCase: true);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            config.SaslUsername = username;
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            config.SaslPassword = password;
        }

        producer = new ProducerBuilder<string, string>(config).Build();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "n/a", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    public Task ProduceAsync(string key, string payload, CancellationToken cancellationToken)
    {
        return producer.ProduceAsync(options.TopicName, new Message<string, string>
        {
            Key = key,
            Value = payload
        }, cancellationToken);
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
    }
}
