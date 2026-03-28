namespace Gearify.SharedKernel.AI;

public class AIServiceConfiguration
{
    public string Region { get; set; } = "us-east-1";
    public string? LocalStackEndpoint { get; set; }
    public bool UseLocalStack { get; set; }
    public string? PersonalizeCampaignArn { get; set; }
    public string? PersonalizeSimilarItemsCampaignArn { get; set; }
    public string? PersonalizeEventTrackerArn { get; set; }
    public string? BedrockModelId { get; set; } = "anthropic.claude-3-5-sonnet-20240620-v1:0";
    public string? BedrockHaikuModelId { get; set; } = "anthropic.claude-3-haiku-20240307-v1:0";
    public string? FraudDetectorId { get; set; }
    public string? ForecastDatasetGroupArn { get; set; }
    public string? UserEventsQueueUrl { get; set; }
    public string? UserEventsTableName { get; set; } = "gearify-user-events";
    public string? DataExportBucket { get; set; } = "gearify-ml-data";
    public CacheConfiguration Cache { get; set; } = new();
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();
}

public class CacheConfiguration
{
    public TimeSpan UserRecommendations { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan SimilarItems { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan GeneratedDescriptions { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan ReviewSummaries { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan ChatHistory { get; set; } = TimeSpan.FromHours(24);
}

public class CircuitBreakerConfiguration
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
}
