using MediatR;

namespace Gearify.CatalogService.Application.Commands;

public record DeleteProductCommand(string ProductId) : IRequest<DeleteProductResult>;

public record DeleteProductResult(bool Success, string? ErrorMessage = null);
