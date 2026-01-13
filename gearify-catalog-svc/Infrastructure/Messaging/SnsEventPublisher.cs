using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.CatalogService.Domain.Events;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of ISnsEventPublisher for catalog events.
/// Wraps domain events in CatalogEvent envelope for backward compatibility.
/// </summary>
public class SnsEventPublisher : ISnsEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly MessagingConfiguration _settings;
    private readonly ILogger<SnsEventPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingConfiguration> settings,
        ILogger<SnsEventPublisher> logger)
    {
        _snsClient = snsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {

        var catalogEventsTopicArn = _settings.SNS.CatalogEventsTopicArn;
        if (string.IsNullOrEmpty(catalogEventsTopicArn))
        {
            _logger.LogWarning("CatalogEventsTopicArn is not configured, skipping event publish");
            return;
        }

        try
        {
            var catalogEvent = CreateCatalogEvent(domainEvent);
            var message = JsonSerializer.Serialize(catalogEvent, JsonOptions);

            var request = new PublishRequest
            {
                TopicArn = catalogEventsTopicArn,
                Message = message,
                Subject = typeof(TEvent).Name
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Published {EventType} to topic {TopicArn}. MessageId: {MessageId}",
                typeof(TEvent).Name,
                catalogEventsTopicArn,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} to SNS: TopicArn={TopicArn}",
                typeof(TEvent).Name,
                catalogEventsTopicArn);

            // Don't throw - event publishing failure shouldn't fail the main operation
        }
    }

    private CatalogEvent CreateCatalogEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        var (eventType, tenantId, payload) = domainEvent switch
        {
            ProductCreatedEvent e => ("ProductCreated", e.TenantId, MapToPayload(e)),
            ProductUpdatedEvent e => ("ProductUpdated", e.TenantId, MapToPayload(e)),
            ProductDeletedEvent e => ("ProductDeleted", e.TenantId, (object)new DeletedProductPayload { Id = e.ProductId, TenantId = e.TenantId }),
            _ => throw new InvalidOperationException($"Unknown event type: {typeof(TEvent).Name}")
        };

        return new CatalogEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            TenantId = tenantId,
            Timestamp = domainEvent.OccurredAt,
            Payload = payload
        };
    }

    private ProductPayload MapToPayload(ProductCreatedEvent e) => new()
    {
        Id = e.ProductId,
        TenantId = e.TenantId,
        Sku = e.Sku,
        Name = e.Name,
        Description = e.Description,
        Brand = e.Brand,
        BrandSlug = e.BrandSlug,
        Department = e.Department,
        DepartmentSlug = e.DepartmentSlug,
        Category = e.Category,
        CategorySlug = e.CategorySlug,
        Subcategory = e.Subcategory,
        SubcategorySlug = e.SubcategorySlug,
        Price = e.Price,
        CompareAtPrice = e.CompareAtPrice,
        DiscountPercentage = e.DiscountPercentage,
        Currency = e.Currency,
        RatingAverage = e.RatingAverage,
        RatingCount = e.RatingCount,
        ThumbnailUrl = e.ThumbnailUrl,
        ImageUrls = e.ImageUrls,
        Tags = e.Tags,
        IsActive = e.IsActive,
        IsDeal = e.IsDeal,
        IsClearance = e.IsClearance,
        IsNewArrival = e.IsNewArrival,
        IsBestSeller = e.IsBestSeller,
        IsFeatured = e.IsFeatured,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private ProductPayload MapToPayload(ProductUpdatedEvent e) => new()
    {
        Id = e.ProductId,
        TenantId = e.TenantId,
        Sku = e.Sku,
        Name = e.Name,
        Description = e.Description,
        Brand = e.Brand,
        BrandSlug = e.BrandSlug,
        Department = e.Department,
        DepartmentSlug = e.DepartmentSlug,
        Category = e.Category,
        CategorySlug = e.CategorySlug,
        Subcategory = e.Subcategory,
        SubcategorySlug = e.SubcategorySlug,
        Price = e.Price,
        CompareAtPrice = e.CompareAtPrice,
        DiscountPercentage = e.DiscountPercentage,
        Currency = e.Currency,
        RatingAverage = e.RatingAverage,
        RatingCount = e.RatingCount,
        ThumbnailUrl = e.ThumbnailUrl,
        ImageUrls = e.ImageUrls,
        Tags = e.Tags,
        IsActive = e.IsActive,
        IsDeal = e.IsDeal,
        IsClearance = e.IsClearance,
        IsNewArrival = e.IsNewArrival,
        IsBestSeller = e.IsBestSeller,
        IsFeatured = e.IsFeatured,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}

#region Event DTOs

/// <summary>
/// Catalog event envelope for backward compatibility with consumers
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
