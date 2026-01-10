using Gearify.SearchService.Application.DTOs;
using MediatR;

namespace Gearify.SearchService.Application.Queries;

public class AutocompleteQuery : IRequest<AutocompleteResponse>
{
    public string? TenantId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int Limit { get; set; } = 10;
}
