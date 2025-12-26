namespace Gearify.CatalogService.Infrastructure.Constants;

/// <summary>
/// DynamoDB table name constants for the Catalog Service
/// </summary>
public static class DynamoDbTableNames
{
    /// <summary>
    /// Catalog table storing departments, categories, sections, and subcategories
    /// </summary>
    public const string CATALOG = "gearify-catalog";

    /// <summary>
    /// Products table storing product catalog data
    /// </summary>
    public const string PRODUCTS = "gearify-products";

    /// <summary>
    /// Brands table storing brand information
    /// </summary>
    public const string BRANDS = "gearify-brands";

    /// <summary>
    /// Price ranges table storing price range filter data
    /// </summary>
    public const string PRICE_RANGES = "gearify-price-ranges";
}
