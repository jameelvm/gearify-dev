namespace Gearify.SharedKernel.Outbox;

/// <summary>
/// Resolves the SNS topic ARN for a given event type.
/// Implemented by each service's SnsEventPublisher via SnsEventPublisherBase.
/// </summary>
public interface ITopicArnResolver
{
    string? GetTopicArn(string eventType);
}
