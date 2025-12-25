namespace Gearify.CatalogService.Application.Mappers;

/// <summary>
/// Factory implementation that manages and provides section mappers
/// </summary>
public class SectionMapperFactory : ISectionMapperFactory
{
    private readonly IEnumerable<ISectionMapper> _mappers;

    public SectionMapperFactory(IEnumerable<ISectionMapper> mappers)
    {
        _mappers = mappers;
    }

    public ISectionMapper? GetMapper(string mappingType)
    {
        if (string.IsNullOrEmpty(mappingType))
        {
            return null;
        }

        return _mappers.FirstOrDefault(m =>
            m.MappingType.Equals(mappingType, StringComparison.OrdinalIgnoreCase));
    }
}
