using Gearify.SearchService.Application.DTOs;
using MediatR;

namespace Gearify.SearchService.Application.Queries;

public class SearchProductsQuery : IRequest<SearchProductsResponse>
{
    public string? TenantId { get; set; }
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Department { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public List<string>? Tags { get; set; }
    public bool? DealsOnly { get; set; }
    public bool? ClearanceOnly { get; set; }
    public bool? NewArrivalsOnly { get; set; }
    public bool? BestSellersOnly { get; set; }
    public string? SortBy { get; set; } = "relevance";
    public string? SortDirection { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
