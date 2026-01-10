namespace Gearify.SearchService.Infrastructure.Configuration;

public class OpenSearchSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public static class IndexNames
{
    public const string Products = "products";
    public const string Categories = "categories";
    public const string Brands = "brands";

    /// <summary>
    /// Get full index name: {tenantId}-{indexType}
    /// Example: default-tenant-products
    /// </summary>
    public static string GetIndexName(string tenantId, string indexType)
    {
        return $"{tenantId}-{indexType}".ToLowerInvariant();
    }
}
