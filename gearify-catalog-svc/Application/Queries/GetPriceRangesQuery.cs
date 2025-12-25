using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get all price ranges for filtering products
/// </summary>
public class GetPriceRangesQuery : IRequest<List<PriceRangeDto>>
{
    /// <summary>
    /// Optional category to filter price ranges
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// If true, only return category-specific ranges (exclude global ranges)
    /// If false (default), return global ranges + category-specific ranges
    /// </summary>
    public bool OnlyCategorySpecific { get; set; } = false;
}
