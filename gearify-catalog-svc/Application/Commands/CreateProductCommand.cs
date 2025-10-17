using MediatR;

namespace Gearify.CatalogService.Application.Commands;

public record CreateProductCommand(
    string TenantId,
    string Sku,
    string Name,
    string Description,
    string Category,
    string Brand,
    decimal Price,
    decimal CompareAtPrice,
    List<string> Tags,
    Dictionary<string, string> Attributes
) : IRequest<CreateProductResult>;

public record CreateProductResult(string ProductId, bool Success, string? ErrorMessage = null);
