using Gearify.SearchService.Application.DTOs;
using MediatR;

namespace Gearify.SearchService.Application.Queries;

public class SimilarProductsQuery : IRequest<SimilarProductsResponse>
{
    public string? TenantId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int Limit { get; set; } = 4;
}
