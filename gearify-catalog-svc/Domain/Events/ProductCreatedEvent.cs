namespace Gearify.CatalogService.Domain.Events;

public record ProductCreatedEvent(
    string ProductId,
    string TenantId,
    string Name,
    decimal Price,
    string Category,
    DateTime OccurredAt
);

public record ProductUpdatedEvent(
    string ProductId,
    string TenantId,
    Dictionary<string, object> Changes,
    DateTime OccurredAt
);

public record ProductDeletedEvent(
    string ProductId,
    string TenantId,
    DateTime OccurredAt
);
