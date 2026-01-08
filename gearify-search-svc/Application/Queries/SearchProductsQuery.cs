using Gearify.SearchService.Application.DTOs;
using MediatR;

namespace Gearify.SearchService.Application.Queries;

public class SearchProductsQuery : IRequest<SearchProductsResponse>
{
    public string? Query { get; set; }
    public string? TenantId { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public List<string>? Tags { get; set; }
    public bool? OnlyDeals { get; set; }
    public bool? OnlyClearance { get; set; }
    public string SortBy { get; set; } = "relevance";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
