namespace XanhNow.Auth.Login.Infrastructure.Redis;

public sealed class RedisOptions
{
    public string Mode { get; set; } = "Cluster";
    public string BootstrapEndpoints { get; set; } = "192.168.2.16:6379,192.168.2.33:6379,192.168.2.53:6379";
    public string KeyPrefix { get; set; } = "auth-login";
    public int DatabaseIndex { get; set; }
    public int DefaultSessionTtlMinutes { get; set; } = 30;
    public bool SlidingExpirationEnabled { get; set; } = true;
    public int AbsoluteLifetimeHours { get; set; } = 24;
    public int MaxSessionsPerUser { get; set; } = 5;
    public int ConnectTimeoutMs { get; set; } = 5000;
    public int OperationTimeoutMs { get; set; } = 3000;
    public bool AbortOnConnectFail { get; set; }

    public void Validate()
    {
        if (!string.Equals(Mode, "Cluster", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Redis mode must be Cluster for App Login.");
        }

        if (DatabaseIndex != 0)
        {
            throw new InvalidOperationException("Redis Cluster contract requires database index 0 and no SELECT usage.");
        }

        if (string.IsNullOrWhiteSpace(BootstrapEndpoints))
        {
            throw new InvalidOperationException("Redis bootstrap endpoints are required.");
        }

        if (string.IsNullOrWhiteSpace(KeyPrefix) || !string.Equals(KeyPrefix, "auth-login", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Redis key prefix must be auth-login.");
        }

        if (DefaultSessionTtlMinutes <= 0 || AbsoluteLifetimeHours <= 0)
        {
            throw new InvalidOperationException("Redis session TTL and absolute lifetime must be positive.");
        }
    }
}
