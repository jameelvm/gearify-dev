using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Gearify.SharedKernel.AI.Resilience;

public class AICircuitBreakerPolicy
{
    private readonly Dictionary<string, ResiliencePipeline> _pipelines = new();
    private readonly ILogger<AICircuitBreakerPolicy> _logger;
    private readonly AIServiceConfiguration _config;
    private readonly object _lock = new();

    public AICircuitBreakerPolicy(
        ILogger<AICircuitBreakerPolicy> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public ResiliencePipeline GetPipeline(string serviceName)
    {
        if (_pipelines.TryGetValue(serviceName, out var existing))
            return existing;

        lock (_lock)
        {
            if (_pipelines.TryGetValue(serviceName, out existing))
                return existing;

            var pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = _config.CircuitBreaker.FailureThreshold,
                    SamplingDuration = _config.CircuitBreaker.SamplingDuration,
                    BreakDuration = _config.CircuitBreaker.BreakDuration,
                    OnOpened = args =>
                    {
                        _logger.LogWarning(
                            "Circuit breaker OPENED for {Service}. Breaking for {Duration}s",
                            serviceName, _config.CircuitBreaker.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        _logger.LogInformation("Circuit breaker CLOSED for {Service}", serviceName);

                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        _logger.LogInformation("Circuit breaker HALF-OPEN for {Service}", serviceName);
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            _pipelines[serviceName] = pipeline;
            return pipeline;
        }
    }

    public bool IsOpen(string serviceName)
    {
        // If no pipeline exists yet, circuit is closed
        return false;
    }
}
