using MediatR;

namespace Gearify.CatalogService.Application.Commands;

public record UpdateProductCommand(
    string ProductId,
    string TenantId,
    string? Name,
    string? Description,
    decimal? Price,
    decimal? CompareAtPrice,
    bool? IsActive
) : IRequest<UpdateProductResult>;

public record UpdateProductResult(bool Success, string? ErrorMessage = null);
