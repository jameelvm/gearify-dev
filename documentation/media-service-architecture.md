# Media Service Architecture & Integration Design

**Document Version:** 1.0
**Date:** December 26, 2024
**Status:** Draft - Pending Review

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Overview](#system-overview)
3. [Service Communication Patterns](#service-communication-patterns)
4. [Recommended Architecture](#recommended-architecture)
5. [Alternative Approaches](#alternative-approaches)
6. [Implementation Details](#implementation-details)
7. [Infrastructure Requirements](#infrastructure-requirements)
8. [Data Flow Diagrams](#data-flow-diagrams)
9. [Error Handling & Resilience](#error-handling--resilience)
10. [Security Considerations](#security-considerations)
11. [Performance & Scalability](#performance--scalability)
12. [Migration Strategy](#migration-strategy)
13. [Decision Matrix](#decision-matrix)
14. [Appendix](#appendix)

---

## Executive Summary

### Purpose
This document defines the architecture for the Media Service and its integration with the Catalog Service in the Gearify e-commerce platform. It evaluates multiple communication patterns and provides a recommended approach.

### Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Upload Strategy** | Backend (Server-Side) | Better security, validation, and automatic processing |
| **Communication Pattern** | Hybrid (Sync + Async) | Balance between UX and decoupling |
| **Primary Integration** | HTTP/REST | Immediate feedback for user operations |
| **Event Bus** | SNS/SQS | Cascade deletes and cross-service notifications |
| **Data Ownership** | Separate databases | Each service owns its data |

### Business Value
- ✅ Fast time-to-market with simple HTTP integration
- ✅ Excellent user experience with immediate feedback
- ✅ Foundation for future scalability with event-driven patterns
- ✅ Lower initial cost and complexity

---

## System Overview

### Media Service Responsibilities

```
┌─────────────────────────────────────────────────────────┐
│                    Media Service                        │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ✓ Image upload and storage (S3)                      │
│  ✓ Image processing (resize, optimize)                │
│  ✓ Generate multiple variants (thumbnail, medium,     │
│    large, original)                                    │
│  ✓ Metadata management (DynamoDB)                     │
│  ✓ URL generation (public, pre-signed)                │
│  ✓ Image validation (type, size, dimensions)          │
│  ✓ Multi-tenant isolation                             │
│  ✓ Soft/hard delete capabilities                      │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Current Implementation

**Technology Stack:**
- **Runtime:** .NET 8.0
- **Storage:** S3 (LocalStack for dev)
- **Database:** DynamoDB
- **Image Processing:** ImageSharp
- **Pattern:** CQRS with MediatR
- **API:** REST with Swagger

**Storage Structure:**
```
gearify-product-images/
└── tenants/
    └── {tenantId}/
        └── {entityType}/          # product, brand, category
            └── {entityId}/
                ├── original/
                ├── thumbnail/     # 150x150, 75% quality
                ├── medium/        # 600x600, 85% quality
                └── large/         # 1200x1200, 90% quality
```

**API Endpoints:**
```
POST   /api/media/upload                    - Upload image
GET    /api/media/{mediaId}                 - Get image metadata
GET    /api/media/{entityType}/{entityId}   - Get all images for entity
DELETE /api/media/{mediaId}                 - Delete image
GET    /health                              - Health check
```

---

## Service Communication Patterns

### Pattern 1: Client Orchestration

```
┌────────┐
│ Client │
└───┬────┘
    │
    │ 1. Upload images
    ├─────────────────────────┐
    │                         ▼
    │                 ┌──────────────┐
    │                 │Media Service │
    │                 └──────┬───────┘
    │                        │
    │  ◄─────────────────────┘ Returns: mediaIds
    │
    │ 2. Create/update product with mediaIds
    ├─────────────────────────┐
    │                         ▼
    │                 ┌────────────────┐
    │                 │Catalog Service │
    │                 └────────────────┘
    └─────────────────────────┘
```

**Characteristics:**
- Client makes multiple API calls
- Services are independent
- Client handles orchestration logic

**Pros:**
- ✅ Services completely decoupled
- ✅ Simple service implementations
- ✅ Clear separation of concerns
- ✅ Easy to debug

**Cons:**
- ❌ Poor UX (multiple round trips)
- ❌ Client complexity increases
- ❌ No atomicity (partial failures possible)
- ❌ Client needs to handle rollback logic

**Use Case:** Public APIs where clients are sophisticated applications

---

### Pattern 2: Backend Orchestration (HTTP)

```
┌────────┐
│ Client │
└───┬────┘
    │ Single request: product + images
    ▼
┌────────────────┐
│Catalog Service │
└────┬───────────┘
     │
     │ HTTP Request
     ├──────────────────────────┐
     │                          ▼
     │                  ┌──────────────┐
     │                  │Media Service │
     │                  └──────┬───────┘
     │                         │
     │  ◄──────────────────────┘ Returns: mediaIds
     │
     ├── Save product with mediaIds
     │
     └── Return complete product
```

**Characteristics:**
- Catalog Service calls Media Service synchronously
- Single API call from client
- Catalog Service owns the workflow

**Pros:**
- ✅ Excellent UX (single API call)
- ✅ Transaction-like behavior
- ✅ Catalog Service controls the flow
- ✅ Can implement rollback logic
- ✅ Client doesn't know about Media Service

**Cons:**
- ❌ Catalog Service depends on Media Service availability
- ❌ Tighter coupling between services
- ❌ Network latency adds up
- ❌ Potential cascading failures

**Use Case:** Admin interfaces, internal tools, moderate traffic

---

### Pattern 3: Event-Driven (Full Async)

```
┌────────┐
│ Client │
└───┬────┘
    │ POST /products
    ▼
┌────────────────┐
│Catalog Service │
└────┬───────────┘
     │
     │ 1. Create product (status: processing)
     │ 2. Publish event to SQS
     │
     ├──────────────────────────┐
     │                          ▼
     │                  ┌─────────────────┐
     │                  │SQS: media-queue │
     │                  └────────┬────────┘
     │                           │ Poll
     │                           ▼
     │                  ┌──────────────┐
     │                  │Media Service │
     │                  │  (Worker)    │
     │                  └──────┬───────┘
     │                         │
     │                         │ Publish SNS
     │                         ▼
     │                  ┌─────────────────┐
     │                  │SNS: ImageUploaded│
     │                  └────────┬────────┘
     │                           │ Subscribe
     │  ◄────────────────────────┘
     │
     └── Update product (status: ready)
```

**Characteristics:**
- Fully asynchronous communication
- Message queues decouple services
- Eventual consistency

**Pros:**
- ✅ Maximum decoupling
- ✅ Highly scalable
- ✅ Resilient (messages queue if service down)
- ✅ Can handle traffic spikes
- ✅ Natural audit trail

**Cons:**
- ❌ Complex to implement and debug
- ❌ Eventual consistency (UX impact)
- ❌ Harder error handling
- ❌ Need message deduplication
- ❌ Requires distributed tracing
- ❌ Higher infrastructure cost

**Use Case:** High volume, bulk operations, multiple service dependencies

---

### Pattern 4: Hybrid (Recommended)

```
┌────────┐
│ Client │
└───┬────┘
    │
    │ User Operations (Sync)
    ▼
┌────────────────┐
│Catalog Service │
└────┬───────────┘
     │
     ├── HTTP (Sync) ────────▶ Media Service
     │                         Upload images
     │                         Returns mediaIds immediately
     │
     ├── Save product with mediaIds
     │
     └── Publish SNS ────────▶ ProductCreated Event
                               │
                               ├─▶ Search Service (index)
                               ├─▶ Notification Service
                               └─▶ Analytics Service

When deleting product:

     ├── Delete product from DB
     │
     └── Publish SNS ────────▶ ProductDeleted Event
                               │
                               └─▶ Media Service (async cascade delete)
```

**Characteristics:**
- Synchronous for user-facing operations
- Asynchronous for background tasks
- Best of both worlds

**Pros:**
- ✅ Great UX (immediate feedback)
- ✅ Decoupled for non-critical operations
- ✅ Scalable where needed
- ✅ Simple for common operations
- ✅ Can evolve to full async later

**Cons:**
- ❌ Two patterns to maintain
- ❌ Need to decide sync vs async per operation

**Use Case:** E-commerce platforms, admin tools with moderate scale

---

## Recommended Architecture

### Selection: **Hybrid Pattern** ✅

**Rationale:**
1. **User Experience First:** Product creation with images needs immediate feedback
2. **Pragmatic Start:** HTTP is simpler to implement and debug
3. **Future-Proof:** Event bus enables future scalability
4. **Cost-Effective:** Lower infrastructure costs initially
5. **Developer Velocity:** Faster time-to-market

### Operation Breakdown

#### Synchronous Operations (HTTP)

| Operation | Method | Endpoint | Reason |
|-----------|--------|----------|--------|
| Upload product images | POST | /api/catalog/products | User expects immediate upload |
| Update product images | PUT | /api/catalog/products/{id}/images | User wants to see changes now |
| Get product with images | GET | /api/catalog/products/{id} | Need URLs immediately |
| Single image upload | POST | /api/media/upload | Simple, fast operation |

#### Asynchronous Operations (SNS/SQS)

| Event | Publisher | Subscribers | Reason |
|-------|-----------|-------------|--------|
| ProductCreated | Catalog Service | Search, Notification, Analytics | Non-blocking notifications |
| ProductUpdated | Catalog Service | Search, Cache | Background reindexing |
| ProductDeleted | Catalog Service | Media, Search, Cache | Cascade cleanup |
| ImageUploaded | Media Service | Analytics, Audit | Background tracking |
| BulkImportRequested | Catalog Service | Import Worker | Long-running process |

---

## Implementation Details

### Phase 1: HTTP Integration (Immediate)

#### 1. Create Media Service Client in Catalog Service

**File:** `gearify-catalog-svc/Infrastructure/Clients/IMediaServiceClient.cs`

```csharp
public interface IMediaServiceClient
{
    /// <summary>
    /// Upload a single image for a product
    /// </summary>
    Task<MediaUploadResponse> UploadImageAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        string entityType,
        string entityId,
        int displayOrder = 0,
        string? altText = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload multiple images for a product
    /// </summary>
    Task<List<MediaUploadResponse>> UploadImagesAsync(
        List<ImageUploadRequest> images,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all images for an entity
    /// </summary>
    Task<List<MediaMetadataDto>> GetImagesForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a single image
    /// </summary>
    Task DeleteImageAsync(
        string mediaId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default);
}

public record MediaUploadResponse(
    string MediaId,
    Dictionary<string, string> Urls);

public record MediaMetadataDto(
    string Id,
    string EntityType,
    string EntityId,
    string FileName,
    Dictionary<string, string> Urls,
    int DisplayOrder,
    string? AltText);

public record ImageUploadRequest(
    Stream Stream,
    string FileName,
    string ContentType,
    int DisplayOrder = 0,
    string? AltText = null);
```

#### 2. Implement HTTP Client

**File:** `gearify-catalog-svc/Infrastructure/Clients/MediaServiceClient.cs`

```csharp
public class MediaServiceClient : IMediaServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MediaServiceClient> _logger;

    public MediaServiceClient(
        HttpClient httpClient,
        ITenantContext tenantContext,
        ILogger<MediaServiceClient> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<MediaUploadResponse> UploadImageAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        string entityType,
        string entityId,
        int displayOrder = 0,
        string? altText = null,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(entityType), "entityType");
        content.Add(new StringContent(entityId), "entityId");
        content.Add(new StringContent(displayOrder.ToString()), "displayOrder");

        if (!string.IsNullOrEmpty(altText))
        {
            content.Add(new StringContent(altText), "altText");
        }

        // Add tenant header
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantContext.TenantId);

        var response = await _httpClient.PostAsync(
            "/api/media/upload",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Media upload failed: {StatusCode} - {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Media upload failed: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken);

        return new MediaUploadResponse(
            MediaId: result.mediaId,
            Urls: JsonSerializer.Deserialize<Dictionary<string, string>>(
                result.urls.ToString()));
    }

    public async Task<List<MediaUploadResponse>> UploadImagesAsync(
        List<ImageUploadRequest> images,
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<MediaUploadResponse>();

        foreach (var image in images)
        {
            image.Stream.Position = 0;
            var result = await UploadImageAsync(
                image.Stream,
                image.FileName,
                image.ContentType,
                entityType,
                entityId,
                image.DisplayOrder,
                image.AltText,
                cancellationToken);

            results.Add(result);
        }

        return results;
    }

    public async Task<List<MediaMetadataDto>> GetImagesForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantContext.TenantId);

        var response = await _httpClient.GetAsync(
            $"/api/media/{entityType}/{entityId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<MediaMetadataDto>>(
            cancellationToken) ?? new List<MediaMetadataDto>();
    }

    public async Task DeleteImageAsync(
        string mediaId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantContext.TenantId);

        var url = $"/api/media/{mediaId}?hardDelete={hardDelete}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
```

#### 3. Register in Startup with Resilience

**File:** `gearify-catalog-svc/Startup.cs`

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... existing services

    // Media Service HTTP Client with Polly resilience
    services.AddHttpClient<IMediaServiceClient, MediaServiceClient>(client =>
    {
        var mediaServiceUrl = Configuration["Services:MediaService:Url"]
            ?? "http://media-svc:80";

        client.BaseAddress = new Uri(mediaServiceUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning(
                    "Media Service call failed. Retry {RetryCount} after {Delay}s",
                    retryCount, timespan.TotalSeconds);
            }))
    .AddTransientHttpErrorPolicy(policy =>
        policy.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                var logger = services.BuildServiceProvider()
                    .GetRequiredService<ILogger<Startup>>();
                logger.LogError(
                    "Media Service circuit breaker opened for {Duration}s",
                    duration.TotalSeconds);
            },
            onReset: () =>
            {
                var logger = services.BuildServiceProvider()
                    .GetRequiredService<ILogger<Startup>>();
                logger.LogInformation("Media Service circuit breaker reset");
            }));
}
```

#### 4. Update Product Entity

**File:** `gearify-catalog-svc/Domain/Entities/Product.cs`

```csharp
public class Product
{
    // ... existing fields

    // Image references
    public List<string> ImageMediaIds { get; set; } = new();
    public string? PrimaryImageMediaId { get; set; }

    // Computed at query time (not stored in DB)
    [JsonIgnore]
    public List<ProductImage>? Images { get; set; }

    [JsonIgnore]
    public ProductImage? PrimaryImage { get; set; }
}

public class ProductImage
{
    public string MediaId { get; set; } = string.Empty;
    public Dictionary<string, string> Urls { get; set; } = new();
    public int DisplayOrder { get; set; }
    public string? AltText { get; set; }
}
```

#### 5. Update Command Handlers

**File:** `gearify-catalog-svc/Application/Commands/CreateProductCommandHandler.cs`

```csharp
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _productRepository;
    private readonly IMediaServiceClient _mediaServiceClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public async Task<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var productId = $"prod-{Guid.NewGuid():N}";
        var mediaIds = new List<string>();

        try
        {
            // 1. Upload images to Media Service
            if (request.Images?.Any() == true)
            {
                _logger.LogInformation(
                    "Uploading {Count} images for product {ProductId}",
                    request.Images.Count, productId);

                var uploadResults = await _mediaServiceClient.UploadImagesAsync(
                    request.Images,
                    "product",
                    productId,
                    cancellationToken);

                mediaIds = uploadResults.Select(r => r.MediaId).ToList();

                _logger.LogInformation(
                    "Successfully uploaded {Count} images: {MediaIds}",
                    mediaIds.Count, string.Join(", ", mediaIds));
            }

            // 2. Create product with image references
            var product = new Product
            {
                PK = $"TENANT#{tenantId}",
                SK = $"PRODUCT#{productId}",
                GSI1PK = $"TENANT#{tenantId}#PRODUCTS",
                GSI1SK = $"PRODUCT#{productId}",
                Id = productId,
                TenantId = tenantId,
                Name = request.Name,
                Sku = request.Sku,
                Description = request.Description,
                Price = request.Price,
                Department = request.Department,
                DepartmentSlug = request.DepartmentSlug,
                Category = request.Category,
                CategorySlug = request.CategorySlug,
                Subcategory = request.Subcategory,
                SubcategorySlug = request.SubcategorySlug,
                Brand = request.Brand,
                BrandSlug = request.BrandSlug,
                ImageMediaIds = mediaIds,
                PrimaryImageMediaId = mediaIds.FirstOrDefault(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _productRepository.CreateAsync(product);

            _logger.LogInformation(
                "Created product {ProductId} with {ImageCount} images",
                productId, mediaIds.Count);

            return new CreateProductResult(
                Success: true,
                ProductId: productId,
                ImageCount: mediaIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating product {ProductId}. Rolling back uploaded images.",
                productId);

            // Rollback: Delete uploaded images
            if (mediaIds.Any())
            {
                foreach (var mediaId in mediaIds)
                {
                    try
                    {
                        await _mediaServiceClient.DeleteImageAsync(
                            mediaId,
                            hardDelete: true,
                            cancellationToken);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx,
                            "Failed to cleanup image {MediaId} during rollback",
                            mediaId);
                    }
                }
            }

            return new CreateProductResult(
                Success: false,
                ErrorMessage: "Failed to create product. Please try again.");
        }
    }
}
```

#### 6. Update Query Handlers (Enrich with Image URLs)

**File:** `gearify-catalog-svc/Application/Queries/GetProductByIdQueryHandler.cs`

```csharp
public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMediaServiceClient _mediaServiceClient;
    private readonly ILogger<GetProductByIdQueryHandler> _logger;

    public async Task<Product?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(
            request.ProductId,
            request.TenantId);

        if (product == null)
            return null;

        // Enrich product with image URLs
        if (product.ImageMediaIds?.Any() == true)
        {
            try
            {
                var mediaItems = await _mediaServiceClient.GetImagesForEntityAsync(
                    "product",
                    product.Id,
                    cancellationToken);

                product.Images = mediaItems
                    .Select(m => new ProductImage
                    {
                        MediaId = m.Id,
                        Urls = m.Urls,
                        DisplayOrder = m.DisplayOrder,
                        AltText = m.AltText
                    })
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();

                product.PrimaryImage = product.Images.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch images for product {ProductId}. Returning product without images.",
                    product.Id);
                // Return product without images rather than failing
            }
        }

        return product;
    }
}
```

#### 7. Configuration

**File:** `gearify-catalog-svc/appsettings.json`

```json
{
  "Services": {
    "MediaService": {
      "Url": "http://media-svc:80",
      "TimeoutSeconds": 30,
      "RetryCount": 3,
      "CircuitBreakerThreshold": 5
    }
  }
}
```

**File:** `gearify-catalog-svc/appsettings.Development.json`

```json
{
  "Services": {
    "MediaService": {
      "Url": "http://localhost:5009"
    }
  }
}
```

---

### Phase 2: Event Bus Integration (Future)

#### 1. Event Definitions

**File:** `gearify-shared-kernel/Events/ProductEvents.cs`

```csharp
public record ProductCreatedEvent : IEvent
{
    public string ProductId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<string> ImageMediaIds { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

public record ProductUpdatedEvent : IEvent
{
    public string ProductId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> AddedImageIds { get; init; } = new();
    public List<string> RemovedImageIds { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

public record ProductDeletedEvent : IEvent
{
    public string ProductId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> ImageMediaIds { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

public interface IEvent
{
    string TenantId { get; }
    DateTime Timestamp { get; }
}
```

#### 2. SNS Publisher

**File:** `gearify-shared-kernel/Messaging/ISnsPublisher.cs`

```csharp
public interface ISnsPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent;
}
```

**File:** `gearify-shared-kernel/Messaging/SnsPublisher.cs`

```csharp
public class SnsPublisher : ISnsPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SnsPublisher> _logger;

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IEvent
    {
        var topicArn = _configuration["AWS:SNS:EventsTopic"];
        var eventType = typeof(T).Name;

        var message = new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(@event),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "EventType", new MessageAttributeValue
                    { DataType = "String", StringValue = eventType } },
                { "TenantId", new MessageAttributeValue
                    { DataType = "String", StringValue = @event.TenantId } },
                { "Timestamp", new MessageAttributeValue
                    { DataType = "String", StringValue = @event.Timestamp.ToString("o") } }
            }
        };

        await _sns.PublishAsync(message, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} for tenant {TenantId}",
            eventType, @event.TenantId);
    }
}
```

#### 3. Event Handler in Media Service

**File:** `gearify-media-svc/Application/EventHandlers/ProductDeletedEventHandler.cs`

```csharp
public class ProductDeletedEventHandler : IEventHandler<ProductDeletedEvent>
{
    private readonly IMediaRepository _mediaRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<ProductDeletedEventHandler> _logger;

    public async Task HandleAsync(
        ProductDeletedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling ProductDeleted event for {ProductId} with {ImageCount} images",
            @event.ProductId, @event.ImageMediaIds.Count);

        foreach (var mediaId in @event.ImageMediaIds)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(
                    mediaId,
                    @event.TenantId);

                if (media != null)
                {
                    // Delete from S3
                    var keys = new[]
                    {
                        media.OriginalKey,
                        media.ThumbnailKey,
                        media.MediumKey,
                        media.LargeKey
                    }.Where(k => !string.IsNullOrEmpty(k));

                    await _storageService.DeleteMultipleAsync(keys, cancellationToken);

                    // Delete from DynamoDB
                    await _mediaRepository.HardDeleteAsync(mediaId, @event.TenantId);

                    _logger.LogInformation(
                        "Deleted image {MediaId} for product {ProductId}",
                        mediaId, @event.ProductId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete image {MediaId} for product {ProductId}",
                    mediaId, @event.ProductId);
                // Continue with other images
            }
        }

        _logger.LogInformation(
            "Completed cascade delete for product {ProductId}",
            @event.ProductId);
    }
}
```

#### 4. SQS Background Worker

**File:** `gearify-media-svc/Workers/EventProcessorWorker.cs`

```csharp
public class EventProcessorWorker : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventProcessorWorker> _logger;
    private readonly string _queueUrl;

    public EventProcessorWorker(
        IAmazonSQS sqs,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EventProcessorWorker> logger)
    {
        _sqs = sqs;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _queueUrl = configuration["AWS:SQS:MediaQueueUrl"]
            ?? "http://localhost:4566/000000000000/gearify-media-queue";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Processor Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20, // Long polling
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqs.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);

                    // Delete message after successful processing
                    await _sqs.DeleteMessageAsync(
                        _queueUrl,
                        message.ReceiptHandle,
                        stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SQS messages");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Event Processor Worker stopped");
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            // SNS wraps the actual message
            var snsMessage = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body);

            if (snsMessage?.MessageAttributes == null)
            {
                _logger.LogWarning("Received message without attributes");
                return;
            }

            var eventType = snsMessage.MessageAttributes["EventType"]?.Value;

            _logger.LogInformation("Processing event: {EventType}", eventType);

            using var scope = _serviceProvider.CreateScope();

            switch (eventType)
            {
                case nameof(ProductDeletedEvent):
                    var deletedEvent = JsonSerializer.Deserialize<ProductDeletedEvent>(
                        snsMessage.Message);
                    var deletedHandler = scope.ServiceProvider
                        .GetRequiredService<IEventHandler<ProductDeletedEvent>>();
                    await deletedHandler.HandleAsync(deletedEvent!, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {MessageId}", message.MessageId);
            throw; // Will move to DLQ if configured
        }
    }
}

public class SnsMessageWrapper
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, MessageAttributeValue>? MessageAttributes { get; set; }
}

public class MessageAttributeValue
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

#### 5. LocalStack Infrastructure Setup

**File:** `gearify-umbrella/localstack/sns/init-topics.sh`

```bash
#!/bin/bash

echo "Creating SNS topics..."

# Create SNS topic for events
awslocal sns create-topic --name gearify-events

TOPIC_ARN=$(awslocal sns list-topics --query 'Topics[?contains(TopicArn, `gearify-events`)].TopicArn' --output text)
echo "Created SNS topic: $TOPIC_ARN"

# Create SQS queues for each service
echo "Creating SQS queues..."
awslocal sqs create-queue --queue-name gearify-media-queue
awslocal sqs create-queue --queue-name gearify-search-queue
awslocal sqs create-queue --queue-name gearify-notification-queue

# Create Dead Letter Queues
awslocal sqs create-queue --queue-name gearify-media-dlq
awslocal sqs create-queue --queue-name gearify-search-dlq
awslocal sqs create-queue --queue-name gearify-notification-dlq

# Get queue ARNs
MEDIA_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url http://localhost:4566/000000000000/gearify-media-queue \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

SEARCH_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url http://localhost:4566/000000000000/gearify-search-queue \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

# Subscribe queues to SNS topic
echo "Subscribing queues to SNS topic..."

awslocal sns subscribe \
    --topic-arn "$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$MEDIA_QUEUE_ARN" \
    --attributes '{"RawMessageDelivery":"false","FilterPolicy":"{\"EventType\":[\"ProductDeletedEvent\"]}"}'

awslocal sns subscribe \
    --topic-arn "$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$SEARCH_QUEUE_ARN" \
    --attributes '{"RawMessageDelivery":"false","FilterPolicy":"{\"EventType\":[\"ProductCreatedEvent\",\"ProductUpdatedEvent\",\"ProductDeletedEvent\"]}"}'

echo "SNS/SQS infrastructure setup complete!"
```

**File:** `gearify-umbrella/localstack/init-aws.sh` (Update)

```bash
#!/bin/bash
set -e

echo "Initializing AWS services in LocalStack..."

# Run existing initialization scripts
./ready.d/dynamodb/init-tables.sh
./ready.d/s3/init-buckets.sh
./ready.d/sns/init-topics.sh  # NEW

echo "AWS services initialization complete!"
```

---

## Infrastructure Requirements

### LocalStack Services

```yaml
# docker-compose.yml - LocalStack configuration
localstack:
  environment:
    - SERVICES=cognito-idp,dynamodb,s3,sqs,sns,ses,secretsmanager,ssm,lambda,logs
```

### Required AWS Resources

| Resource | Name | Purpose |
|----------|------|---------|
| **S3 Bucket** | gearify-product-images | Store product images |
| **DynamoDB Table** | gearify-media | Store media metadata |
| **SNS Topic** | gearify-events | Publish domain events |
| **SQS Queue** | gearify-media-queue | Media Service event queue |
| **SQS Queue** | gearify-search-queue | Search Service event queue |
| **SQS Queue** | gearify-media-dlq | Dead letter queue for failed messages |

### Service Dependencies

```
Catalog Service:
  - Media Service (HTTP)
  - SNS (Publish)

Media Service:
  - S3 (Storage)
  - DynamoDB (Metadata)
  - SQS (Subscribe)
  - SNS (Publish)

Search Service:
  - SQS (Subscribe)
```

---

## Data Flow Diagrams

### Create Product with Images (Synchronous)

```
┌─────────┐
│  Admin  │
│  Client │
└────┬────┘
     │
     │ POST /api/catalog/products
     │ Content-Type: multipart/form-data
     │ {
     │   "name": "4K Camera",
     │   "price": 299.99,
     │   "images": [File1, File2, File3]
     │ }
     ▼
┌────────────────────────────────────┐
│      API Gateway (port 8080)       │
└────────────────┬───────────────────┘
                 │ Route: /api/catalog/*
                 ▼
┌────────────────────────────────────┐
│       Catalog Service              │
│                                    │
│  1. Extract product data           │
│  2. Generate productId             │
│     productId = "prod-abc123"      │
│                                    │
│  3. Upload images to Media Service │
│     ┌──────────────────────────┐   │
│     │ HTTP POST to Media Svc   │   │
│     │ /api/media/upload        │   │
│     │ X-Tenant-Id: default     │   │
│     └────────┬─────────────────┘   │
└──────────────┼─────────────────────┘
               │
               ▼
┌────────────────────────────────────┐
│        Media Service               │
│                                    │
│  1. Validate image                 │
│     - Check file type (jpg/png)    │
│     - Check size (< 10MB)          │
│     - Verify it's a valid image    │
│                                    │
│  2. Process image                  │
│     ┌──────────────────────────┐   │
│     │ ImageProcessor           │   │
│     │ - Generate thumbnail     │   │
│     │ - Generate medium        │   │
│     │ - Generate large         │   │
│     │ - Keep original          │   │
│     └────────┬─────────────────┘   │
│              │                     │
│  3. Upload to S3                   │
│     ┌──────────────────────────┐   │
│     │ S3StorageService         │   │
│     │ Upload 4 variants to:    │   │
│     │ tenants/default/product/ │   │
│     │   prod-abc123/           │   │
│     │     ├─ original/         │   │
│     │     ├─ thumbnail/        │   │
│     │     ├─ medium/           │   │
│     │     └─ large/            │   │
│     └────────┬─────────────────┘   │
│              │                     │
│  4. Save metadata                  │
│     ┌──────────────────────────┐   │
│     │ DynamoDB                 │   │
│     │ Table: gearify-media     │   │
│     │ PK: TENANT#default       │   │
│     │ SK: MEDIA#media-xyz789   │   │
│     │ GSI1PK: TENANT#default#  │   │
│     │         PRODUCT#prod-abc │   │
│     └────────┬─────────────────┘   │
│              │                     │
│  5. Return response                │
│     {                              │
│       "mediaId": "media-xyz789",   │
│       "urls": {                    │
│         "thumbnail": "http://...", │
│         "medium": "http://...",    │
│         "large": "http://..."      │
│       }                            │
│     }                              │
└────────────────┬───────────────────┘
                 │
                 │ Returns: mediaIds
                 ▼
┌────────────────────────────────────┐
│       Catalog Service              │
│                                    │
│  4. Store product with images      │
│     ┌──────────────────────────┐   │
│     │ DynamoDB                 │   │
│     │ Table: gearify-products  │   │
│     │ {                        │   │
│     │   "id": "prod-abc123",   │   │
│     │   "name": "4K Camera",   │   │
│     │   "imageMediaIds": [     │   │
│     │     "media-xyz789",      │   │
│     │     "media-xyz790"       │   │
│     │   ]                      │   │
│     │ }                        │   │
│     └──────────────────────────┘   │
│                                    │
│  5. Publish ProductCreated event   │
│     ┌──────────────────────────┐   │
│     │ SNS: gearify-events      │   │
│     │ EventType:               │   │
│     │   ProductCreatedEvent    │   │
│     └──────────────────────────┘   │
│                                    │
│  6. Return success                 │
│     {                              │
│       "success": true,             │
│       "productId": "prod-abc123",  │
│       "imageCount": 2              │
│     }                              │
└────────────────┬───────────────────┘
                 │
                 ▼
┌────────────────────────────────────┐
│           Client                   │
│  Receives immediate confirmation   │
│  Product created with images!      │
└────────────────────────────────────┘

Time: ~2-3 seconds (synchronous)
```

### Delete Product (Asynchronous Cascade)

```
┌─────────┐
│  Admin  │
│  Client │
└────┬────┘
     │
     │ DELETE /api/catalog/products/prod-abc123
     ▼
┌────────────────────────────────────┐
│       Catalog Service              │
│                                    │
│  1. Get product                    │
│     product = {                    │
│       "id": "prod-abc123",         │
│       "imageMediaIds": [           │
│         "media-xyz789",            │
│         "media-xyz790"             │
│       ]                            │
│     }                              │
│                                    │
│  2. Delete from DynamoDB           │
│     DELETE TENANT#default/         │
│            PRODUCT#prod-abc123     │
│                                    │
│  3. Publish ProductDeleted event   │
│     ┌──────────────────────────┐   │
│     │ SNS: gearify-events      │   │
│     │ {                        │   │
│     │   "eventType":           │   │
│     │     "ProductDeleted",    │   │
│     │   "productId":           │   │
│     │     "prod-abc123",       │   │
│     │   "imageMediaIds": [     │   │
│     │     "media-xyz789",      │   │
│     │     "media-xyz790"       │   │
│     │   ]                      │   │
│     │ }                        │   │
│     └──────┬───────────────────┘   │
└────────────┼───────────────────────┘
             │ Fan-out to subscribers
             │
     ┌───────┴────────┬──────────────┐
     ▼                ▼              ▼
┌─────────┐  ┌──────────────┐  ┌──────────┐
│ Media   │  │   Search     │  │Analytics │
│ Queue   │  │   Queue      │  │  Queue   │
└────┬────┘  └──────┬───────┘  └─────┬────┘
     │              │                 │
     │ (async)      │ (async)         │ (async)
     ▼              ▼                 ▼
┌─────────────┐ ┌────────────┐ ┌──────────┐
│   Media     │ │  Search    │ │Analytics │
│  Service    │ │  Service   │ │ Service  │
│             │ │            │ │          │
│ Delete      │ │ Remove     │ │ Track    │
│ images      │ │ from index │ │ deletion │
│ from S3     │ │            │ │          │
└─────────────┘ └────────────┘ └──────────┘

Client receives immediate response (200 OK)
Background: Images deleted within 5-10 seconds
```

---

## Error Handling & Resilience

### HTTP Communication Resilience

**Retry Policy:**
```
Failed Request → Wait 1s → Retry
              → Wait 2s → Retry
              → Wait 4s → Retry
              → Give up (return error)
```

**Circuit Breaker:**
```
Normal State (Closed)
  ↓ (5 consecutive failures)
Open State (Block requests for 30s)
  ↓ (After 30s)
Half-Open State (Test with 1 request)
  ↓ (Success)
Closed State (Resume normal)
```

**Timeout:**
- HTTP request timeout: 30 seconds
- Circuit breaker duration: 30 seconds

### Event Processing Resilience

**SQS Message Handling:**
```
Receive Message
  → Process
  → Success? Delete from queue
  → Failure? Return to queue (retry)
  → Max retries exceeded? Move to DLQ
```

**Dead Letter Queue (DLQ):**
- Messages move to DLQ after 3 failed processing attempts
- Ops team monitors DLQ
- Failed messages can be replayed

**Idempotency:**
- Event handlers check if action already performed
- Use unique messageId or correlationId
- Example: Check if image already deleted before deleting again

### Rollback Strategies

**Product Creation Failure:**
```csharp
try
{
    // Upload images
    var mediaIds = await UploadImages();

    // Create product
    await CreateProduct(mediaIds);
}
catch
{
    // Rollback: Delete uploaded images
    foreach (var mediaId in mediaIds)
    {
        await DeleteImage(mediaId, hardDelete: true);
    }
    throw;
}
```

**Partial Success Handling:**
- If 2 out of 3 images upload, continue with 2
- Log warnings for failed uploads
- Return partial success to client

---

## Security Considerations

### Authentication & Authorization

**Service-to-Service:**
- Internal network (Docker network) - no internet exposure
- Consider mTLS for production
- API Gateway handles external auth

**Tenant Isolation:**
- Every request includes X-Tenant-Id header
- Services validate tenant on every operation
- DynamoDB keys include tenant prefix
- S3 paths include tenant folder

### Input Validation

**File Upload:**
```csharp
// Media Service validates:
- File size (< 10MB)
- File type (image/jpeg, image/png, image/webp only)
- File is actually an image (ImageSharp validation)
- Filename sanitization (prevent path traversal)
```

**API Input:**
```csharp
// Catalog Service validates:
- Product data (required fields, data types)
- Price ranges (> 0)
- String lengths (prevent DoS)
```

### S3 Security

**Bucket Policy:**
- Block public access by default
- Services use IAM roles (not access keys in production)
- Pre-signed URLs for temporary access

**CORS Configuration:**
```json
{
  "AllowedOrigins": ["http://localhost:4200", "https://*.gearify.com"],
  "AllowedMethods": ["GET", "PUT", "POST"],
  "AllowedHeaders": ["*"],
  "MaxAgeSeconds": 3000
}
```

### Event Security

**SNS/SQS:**
- Messages include tenantId for validation
- Event handlers validate tenant context
- Message encryption at rest (AWS KMS)

---

## Performance & Scalability

### Current Performance Characteristics

| Operation | Time | Throughput |
|-----------|------|------------|
| Upload 1 image | ~1-2s | N/A |
| Upload 3 images | ~3-5s | Sequential |
| Create product + images | ~4-6s | N/A |
| Get product with images | ~200-500ms | Parallel fetches |
| Delete product (sync) | ~50ms | Catalog only |
| Delete images (async) | ~2-5s | Background |

### Bottlenecks

**Current:**
1. Sequential image uploads (3 images = 3x time)
2. HTTP round-trip latency
3. ImageSharp processing time

**Solutions:**
```csharp
// Parallel upload
var uploadTasks = images.Select(img =>
    _mediaServiceClient.UploadImageAsync(img));
var results = await Task.WhenAll(uploadTasks);
```

### Scaling Strategies

**Horizontal Scaling:**
```
┌──────────┐
│ Client   │
└────┬─────┘
     │
     ▼
┌────────────┐
│ API Gateway│
└────┬───────┘
     │
     ├─▶ Catalog Service (Instance 1)
     ├─▶ Catalog Service (Instance 2)
     ├─▶ Catalog Service (Instance 3)
     │
     ├─▶ Media Service (Instance 1)
     ├─▶ Media Service (Instance 2)
     └─▶ Media Service (Instance 3)
```

**Caching:**
```
Get Product → Check Redis → Hit? Return
                          → Miss? Fetch from DB + Media Service
                                  → Store in Redis (TTL: 5 min)
                                  → Return
```

**CDN:**
```
Client → CloudFront (CDN) → S3 (origin)
      ← Cached image ←─────
```

### Load Testing Targets

| Metric | Target |
|--------|--------|
| Concurrent users | 100 |
| Requests per second | 50 |
| Product creation | < 10s (p95) |
| Product listing | < 500ms (p95) |
| Image load time | < 300ms (p95) |

---

## Migration Strategy

### Phase 1: HTTP Integration (Week 1-2)

**Goals:**
- ✅ Create MediaServiceClient
- ✅ Update CreateProductCommand
- ✅ Update GetProductQuery
- ✅ Test in local environment

**Deliverables:**
- Working product creation with images
- Image display on product detail page
- Error handling and rollback

**Testing:**
```
1. Create product with 3 images
   → Verify all 4 variants created
   → Verify metadata in DynamoDB
   → Verify URLs work

2. Create product, fail product save
   → Verify images rolled back

3. Get product
   → Verify images enriched
   → Verify URLs correct

4. Delete product
   → Manual: Clean up images
```

### Phase 2: Event Bus (Week 3-4)

**Goals:**
- ✅ Set up SNS/SQS infrastructure
- ✅ Implement event publisher
- ✅ Implement event handlers
- ✅ Test cascade deletes

**Deliverables:**
- ProductCreated events published
- ProductDeleted cascades to images
- Dead letter queue monitoring

**Testing:**
```
1. Create product
   → Verify ProductCreated event published
   → Verify search service receives event

2. Delete product
   → Verify ProductDeleted event published
   → Verify images deleted within 10s
   → Verify search index updated

3. Simulate Media Service down
   → Verify messages queue up
   → Bring service back
   → Verify messages processed
```

### Phase 3: Optimization (Week 5+)

**Goals:**
- ✅ Parallel image uploads
- ✅ Response caching (Redis)
- ✅ CDN setup
- ✅ Performance monitoring

**Deliverables:**
- < 5s product creation with images
- < 300ms image load times
- Grafana dashboards

---

## Decision Matrix

### Communication Pattern Decision

| Criteria | Client Orch | HTTP Sync | Event-Driven | **Hybrid** |
|----------|-------------|-----------|--------------|------------|
| **User Experience** | ❌ Poor | ✅ Excellent | ❌ Delayed | ✅ Excellent |
| **Simplicity** | ✅ Simple | ✅ Simple | ❌ Complex | ⚠️ Moderate |
| **Decoupling** | ✅ Best | ❌ Tight | ✅ Best | ✅ Good |
| **Error Handling** | ❌ Client | ✅ Service | ❌ Complex | ✅ Both |
| **Scalability** | ⚠️ OK | ⚠️ OK | ✅ Excellent | ✅ Good |
| **Debugging** | ✅ Easy | ✅ Easy | ❌ Hard | ⚠️ Moderate |
| **Cost** | ✅ Low | ✅ Low | ❌ High | ⚠️ Moderate |
| **Time to Market** | ✅ Fast | ✅ Fast | ❌ Slow | ⚠️ Moderate |
| **Our Use Case** | ❌ No | ⚠️ OK | ❌ Overkill | ✅ **Perfect** |

### Recommendation: **Hybrid Pattern** ✅

**Reasoning:**
1. **80% of operations are user-facing** → Need immediate feedback (HTTP)
2. **20% are background tasks** → Can be async (Events)
3. **Moderate scale expected** → Not millions of products/day
4. **Admin users** → Expect professional tools, not "processing..." delays
5. **Cost-conscious** → Don't want to pay for unnecessary infrastructure
6. **Fast time-to-market** → Start simple, scale later

---

## Appendix

### A. NuGet Packages Required

**Catalog Service:**
```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.2.0" />
<PackageReference Include="AWSSDK.SimpleNotificationService" Version="3.7.400" />
```

**Media Service:**
```xml
<PackageReference Include="AWSSDK.SQS" Version="3.7.400" />
<PackageReference Include="AWSSDK.SimpleNotificationService" Version="3.7.400" />
```

### B. Environment Variables

**Catalog Service:**
```bash
MEDIA_SERVICE_URL=http://media-svc:80
SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:gearify-events
```

**Media Service:**
```bash
SQS_QUEUE_URL=http://localhost:4566/000000000000/gearify-media-queue
SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:gearify-events
```

### C. Monitoring Metrics

**Key Metrics to Track:**
```
Catalog Service:
- media_service_calls_total (counter)
- media_service_call_duration_seconds (histogram)
- media_service_circuit_breaker_state (gauge: 0=closed, 1=open)
- product_creation_duration_seconds (histogram)
- product_creation_image_count (histogram)

Media Service:
- image_upload_duration_seconds (histogram)
- image_processing_duration_seconds (histogram)
- s3_upload_duration_seconds (histogram)
- image_variants_generated_total (counter)
- sqs_messages_processed_total (counter)
- sqs_message_processing_duration_seconds (histogram)
```

### D. Useful Commands

**Check Media Service Health:**
```bash
curl http://localhost:5009/health
```

**Upload Test Image:**
```bash
curl -X POST http://localhost:8080/api/catalog/products \
  -H "Content-Type: multipart/form-data" \
  -H "X-Tenant-Id: default" \
  -F "name=Test Product" \
  -F "price=99.99" \
  -F "images=@test-image.jpg"
```

**Check SQS Queue:**
```bash
awslocal sqs receive-message \
  --queue-url http://localhost:4566/000000000000/gearify-media-queue
```

**Publish Test Event:**
```bash
awslocal sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-events \
  --message '{"eventType":"ProductDeleted","productId":"prod-123"}'
```

### E. Troubleshooting Guide

**Problem: "Media Service not reachable"**
```bash
# Check if service is running
docker ps | grep media-svc

# Check service logs
docker logs gearify-media-svc

# Test connectivity from catalog service
docker exec gearify-catalog-svc curl http://media-svc:80/health
```

**Problem: "Images not showing"**
```bash
# Check DynamoDB for media records
awslocal dynamodb scan --table-name gearify-media

# Check S3 for uploaded files
awslocal s3 ls s3://gearify-product-images/tenants/default/products/

# Check Media Service logs
docker logs gearify-media-svc | grep -i error
```

**Problem: "Events not processed"**
```bash
# Check SQS queue depth
awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-media-queue \
  --attribute-names ApproximateNumberOfMessages

# Check DLQ for failed messages
awslocal sqs receive-message \
  --queue-url http://localhost:4566/000000000000/gearify-media-dlq
```

---

## Approval Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **Technical Lead** | | | |
| **Product Owner** | | | |
| **DevOps Lead** | | | |
| **Security Team** | | | |

---

**Document Status:** Draft - Pending Review

**Next Steps:**
1. Review architecture document
2. Approve communication pattern
3. Confirm implementation phases
4. Begin Phase 1 implementation

---

**End of Document**
