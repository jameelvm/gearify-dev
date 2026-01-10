namespace Gearify.CatalogService.Infrastructure.Configuration;

/// <summary>
/// Configuration for event publishing to SNS
/// </summary>
public class EventPublisherSettings
{
    /// <summary>
    /// ARN of the SNS topic for catalog events
    /// </summary>
    public string CatalogEventsTopicArn { get; set; } = string.Empty;

    /// <summary>
    /// Whether event publishing is enabled (can be disabled for testing)
    /// </summary>
    public bool Enabled { get; set; } = true;
}
