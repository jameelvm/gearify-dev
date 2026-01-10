using System.Text.Json.Serialization;

namespace Gearify.SearchService.Domain.Events;

/// <summary>
/// SNS wraps messages in this envelope when sending to SQS
/// We need to unwrap this to get the actual event
/// </summary>
public class SnsMessageWrapper
{
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("MessageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("TopicArn")]
    public string TopicArn { get; set; } = string.Empty;

    [JsonPropertyName("Subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// The actual message content (JSON string of CatalogEvent)
    /// </summary>
    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("SignatureVersion")]
    public string? SignatureVersion { get; set; }

    [JsonPropertyName("Signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("SigningCertURL")]
    public string? SigningCertUrl { get; set; }

    [JsonPropertyName("UnsubscribeURL")]
    public string? UnsubscribeUrl { get; set; }
}
