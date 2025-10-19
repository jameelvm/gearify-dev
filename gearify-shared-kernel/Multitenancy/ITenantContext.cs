namespace Gearify.SharedKernel.Multitenancy;

/// <summary>
/// Provides access to the current tenant context for the request.
/// This is resolved per HTTP request via middleware.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant ID for this request.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Indicates whether the tenant has been successfully resolved.
    /// </summary>
    bool IsResolved { get; }
}
