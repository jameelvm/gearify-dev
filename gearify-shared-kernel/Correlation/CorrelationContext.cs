namespace Gearify.SharedKernel.Correlation;

/// <summary>
/// Ambient storage for the current CorrelationId.
/// Uses AsyncLocal so the value flows across async calls within the same logical operation,
/// whether initiated by HTTP middleware or an event queue processor.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the current CorrelationId, or null if not set.
    /// </summary>
    public static string? Get() => _correlationId.Value;

    /// <summary>
    /// Sets the current CorrelationId.
    /// </summary>
    public static void Set(string correlationId) => _correlationId.Value = correlationId;

    /// <summary>
    /// Gets the current CorrelationId, or creates and sets a new one if not present.
    /// </summary>
    public static string GetOrCreate()
    {
        var existing = _correlationId.Value;
        if (!string.IsNullOrEmpty(existing))
            return existing;

        var newId = Guid.NewGuid().ToString();
        _correlationId.Value = newId;
        return newId;
    }
}
