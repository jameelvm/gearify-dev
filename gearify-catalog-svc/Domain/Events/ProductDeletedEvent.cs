using Gearify.SharedKernel.Events;

namespace Gearify.CatalogService.Domain.Events;

/// <summary>
/// Domain event raised when a product is deleted.
/// </summary>
public record ProductDeletedEvent(
    string ProductId,
    string TenantId,
    DateTime OccurredAt) : IDomainEvent;
