namespace Gearify.SharedKernel.Outbox;

public class OutboxPublisherOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 100;
    public int MaxRetries { get; set; } = 10;
    public double BackoffBase { get; set; } = 2.0;
}
