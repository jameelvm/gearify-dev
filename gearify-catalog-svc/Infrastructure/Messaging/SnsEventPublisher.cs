using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.CatalogService.Application.Events;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// Publishes catalog events to SNS for consumption by other services (e.g., Search Service)
/// </summary>
public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly EventPublisherSettings _settings;
    private readonly ILogger<SnsEventPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<EventPublisherSettings> settings,
        ILogger<SnsEventPublisher> logger)
    {
        _snsClient = snsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishProductCreatedAsync(Product product, CancellationToken cancellationToken = default)
    {
        var catalogEvent = CreateCatalogEvent("ProductCreated", product.TenantId, product);
        await PublishEventAsync(catalogEvent, cancellationToken);

        _logger.LogInformation(
            "Published ProductCreated event: ProductId={ProductId}, TenantId={TenantId}",
            product.Id,
            product.TenantId);
    }

    public async Task PublishProductUpdatedAsync(Product product, CancellationToken cancellationToken = default)
    {
        var catalogEvent = CreateCatalogEvent("ProductUpdated", product.TenantId, product);
        await PublishEventAsync(catalogEvent, cancellationToken);

        _logger.LogInformation(
            "Published ProductUpdated event: ProductId={ProductId}, TenantId={TenantId}",
            product.Id,
            product.TenantId);
    }

    public async Task PublishProductDeletedAsync(string productId, string tenantId, CancellationToken cancellationToken = default)
    {
        // For delete, we only need the ID and tenant
        var payload = new DeletedProductPayload
        {
            Id = productId,
            TenantId = tenantId
        };

        var catalogEvent = new CatalogEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "ProductDeleted",
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };

        await PublishEventAsync(catalogEvent, cancellationToken);

        _logger.LogInformation(
            "Published ProductDeleted event: ProductId={ProductId}, TenantId={TenantId}",
            productId,
            tenantId);
    }

    private CatalogEvent CreateCatalogEvent(string eventType, string tenantId, Product product)
    {
        return new CatalogEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow,
            Payload = MapProductToPayload(product)
        };
    }

    private ProductPayload MapProductToPayload(Product product)
    {
        return new ProductPayload
        {
            Id = product.Id,
            TenantId = product.TenantId,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Brand = product.Brand,
            BrandSlug = product.BrandSlug,
            Department = product.Department,
            DepartmentSlug = product.DepartmentSlug,
            Category = product.Category,
            CategorySlug = product.CategorySlug,
            Subcategory = product.Subcategory,
            SubcategorySlug = product.SubcategorySlug,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            DiscountPercentage = product.DiscountPercentage,
            Currency = product.Currency,
            RatingAverage = product.RatingAverage,
            RatingCount = product.RatingCount,
            ThumbnailUrl = product.ThumbnailUrl,
            ImageUrls = product.ImageUrls ?? new List<string>(),
            Tags = product.Tags ?? new List<string>(),
            IsActive = product.IsActive,
            IsDeal = product.IsDeal,
            IsClearance = product.IsClearance,
            IsNewArrival = product.IsNewArrival,
            IsBestSeller = product.IsBestSeller,
            IsFeatured = product.IsFeatured,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private async Task PublishEventAsync(object catalogEvent, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Event publishing is disabled, skipping");
            return;
        }

        if (string.IsNullOrEmpty(_settings.CatalogEventsTopicArn))
        {
            _logger.LogWarning("CatalogEventsTopicArn is not configured, skipping event publish");
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(catalogEvent, JsonOptions);

            var request = new PublishRequest
            {
                TopicArn = _settings.CatalogEventsTopicArn,
                Message = message
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogDebug(
                "Event published to SNS: MessageId={MessageId}, TopicArn={TopicArn}",
                response.MessageId,
                _settings.CatalogEventsTopicArn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish event to SNS: TopicArn={TopicArn}",
                _settings.CatalogEventsTopicArn);

            // Don't throw - event publishing failure shouldn't fail the main operation
            // In production, you might want to implement retry logic or dead-letter queue
        }
    }
}

#region Event DTOs

/// <summary>
/// Catalog event envelope
/// </summary>
public class CatalogEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Payload { get; set; }
}

/// <summary>
/// Product payload for create/update events
/// </summary>
public class ProductPayload
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? DepartmentSlug { get; set; }
    public string? Category { get; set; }
    public string? CategorySlug { get; set; }
    public string? Subcategory { get; set; }
    public string? SubcategorySlug { get; set; }
    public decimal Price { get; set; }
    public decimal CompareAtPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsDeal { get; set; }
    public bool IsClearance { get; set; }
    public bool IsNewArrival { get; set; }
    public bool IsBestSeller { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Minimal payload for delete events
/// </summary>
public class DeletedProductPayload
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

#endregion
