namespace Gearify.SharedKernel.Multitenancy;

/// <summary>
/// Implementation of ITenantContext that holds the current tenant information.
/// Registered as Scoped - one instance per HTTP request.
/// </summary>
public class TenantContext : ITenantContext
{
    private string _tenantId = string.Empty;
    private bool _isResolved = false;

    /// <inheritdoc />
    public string TenantId => _tenantId;

    /// <inheritdoc />
    public bool IsResolved => _isResolved;

    /// <summary>
    /// Sets the tenant ID for the current request.
    /// This should only be called by the TenantMiddleware.
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    public void SetTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        _tenantId = tenantId;
        _isResolved = true;
    }

    /// <summary>
    /// Clears the tenant context (used for testing)
    /// </summary>
    public void Clear()
    {
        _tenantId = string.Empty;
        _isResolved = false;
    }
}
