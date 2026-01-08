namespace Gearify.CatalogService.Application.DTOs;

/// <summary>
/// DTO for a single sort option
/// </summary>
public record SortOptionDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string SortType { get; init; } = string.Empty; // "Simple" | "Filter"
    public string? FilterField { get; init; } // Only for SortType="Filter"
    public string SortField { get; init; } = string.Empty;
    public string SortDirection { get; init; } = string.Empty; // "ASC" | "DESC"
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Response containing list of sort options for a tenant
/// </summary>
public record SortOptionsResponse(List<SortOptionDto> SortOptions);
