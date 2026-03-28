using Microsoft.Extensions.Logging;

namespace Gearify.SharedKernel.AI.Monitoring;

public class BedrockCostTracker
{
    private readonly ILogger<BedrockCostTracker> _logger;
    private long _totalInputTokens;
    private long _totalOutputTokens;

    // Pricing per 1K tokens (USD)
    private static readonly Dictionary<string, (decimal Input, decimal Output)> ModelPricing = new()
    {
        ["anthropic.claude-3-5-sonnet-20240620-v1:0"] = (0.003m, 0.015m),
        ["anthropic.claude-3-haiku-20240307-v1:0"] = (0.00025m, 0.00125m),
    };

    public BedrockCostTracker(ILogger<BedrockCostTracker> logger)
    {
        _logger = logger;
    }

    public void RecordUsage(string modelId, string feature, int inputTokens, int outputTokens)
    {
        Interlocked.Add(ref _totalInputTokens, inputTokens);
        Interlocked.Add(ref _totalOutputTokens, outputTokens);

        var cost = CalculateCost(modelId, inputTokens, outputTokens);

        _logger.LogInformation(
            "Bedrock usage - Model: {Model}, Feature: {Feature}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, EstimatedCost: ${Cost:F6}",
            modelId, feature, inputTokens, outputTokens, cost);
    }

    public decimal CalculateCost(string modelId, int inputTokens, int outputTokens)
    {
        if (!ModelPricing.TryGetValue(modelId, out var pricing))
            return 0;

        return (inputTokens / 1000m * pricing.Input) + (outputTokens / 1000m * pricing.Output);
    }

    public (long InputTokens, long OutputTokens) GetTotalUsage()
    {
        return (Interlocked.Read(ref _totalInputTokens), Interlocked.Read(ref _totalOutputTokens));
    }
}
