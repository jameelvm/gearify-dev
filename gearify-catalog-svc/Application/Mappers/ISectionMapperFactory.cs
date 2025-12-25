namespace Gearify.CatalogService.Application.Mappers;

/// <summary>
/// Factory for creating section mappers based on mapping type
/// </summary>
public interface ISectionMapperFactory
{
    /// <summary>
    /// Gets the appropriate mapper for the given mapping type
    /// </summary>
    /// <param name="mappingType">The mapping type (e.g., "BRAND", "CATEGORY")</param>
    /// <returns>The mapper instance, or null if no mapper is registered for the type</returns>
    ISectionMapper? GetMapper(string mappingType);
}
