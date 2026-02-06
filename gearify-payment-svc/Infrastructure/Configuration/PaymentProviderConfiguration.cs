namespace Gearify.PaymentService.Infrastructure.Configuration;

public class PaymentProviderConfiguration
{
    /// <summary>
    /// Use mock payment providers instead of real ones.
    /// Set to true for local development and testing.
    /// </summary>
    public bool UseMockProviders { get; set; } = true;

    /// <summary>
    /// Enable detailed logging of payment provider operations.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Testing mode for payment processing.
    /// Values: "Normal" (default), "Slow" (30-second delay for deferred cancellation testing)
    /// </summary>
    public string PaymentTestingMode { get; set; } = "Normal";
}
