# AI Integration Architecture Patterns

Integration patterns and best practices for incorporating AI/ML services into the Gearify microservices architecture.

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Client Layer                                    │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  Angular Frontend (gearify-web)                                  │  │
│  │  - Product browsing, search, cart, checkout                      │  │
│  │  - Real-time interactions tracking                               │  │
│  └───────────────────────────┬──────────────────────────────────────┘  │
└────────────────────────────────┼───────────────────────────────────────┘
                                 │ HTTPS
┌────────────────────────────────▼───────────────────────────────────────┐
│                         API Gateway (YARP)                              │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  - Request routing                                                │  │
│  │  - Authentication/Authorization                                   │  │
│  │  - Rate limiting                                                  │  │
│  │  - Event tracking middleware                                      │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└─────┬───────────┬────────────┬─────────────┬────────────┬──────────────┘
      │           │            │             │            │
      │           │            │             │            │
┌─────▼───┐  ┌───▼─────┐  ┌──▼──────┐  ┌──▼──────┐  ┌─▼────────┐
│ Catalog │  │  Media  │  │  Order  │  │  Auth   │  │  Notif   │
│ Service │  │ Service │  │ Service │  │ Service │  │  Service │
└─────┬───┘  └───┬─────┘  └──┬──────┘  └─────────┘  └─┬────────┘
      │          │            │                        │
      │ AI Integration Points │                        │
┌─────▼──────────▼────────────▼────────────────────────▼──────────┐
│                  AWS AI/ML Services Layer                        │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌──────────────┐   │
│  │Personalize│  │Comprehend │  │Rekognition│ │  Lex V2      │   │
│  │(Recomm.)  │  │  (NLP)    │  │ (Vision)  │  │  (Chatbot)   │   │
│  └──────────┘  └───────────┘  └──────────┘  └──────────────┘   │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌──────────────┐   │
│  │ Forecast │  │   Fraud   │  │SageMaker │  │  Textract    │   │
│  │(Demand)  │  │ Detector  │  │ (Custom) │  │  (OCR)       │   │
│  └──────────┘  └───────────┘  └──────────┘  └──────────────┘   │
└─────────────────────────┬────────────────────────────────────────┘
                          │
┌─────────────────────────▼────────────────────────────────────────┐
│                  Data & Infrastructure Layer                      │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌──────────────┐   │
│  │ DynamoDB │  │    S3     │  │   SQS    │  │     SNS      │   │
│  │ (NoSQL)  │  │ (Storage) │  │ (Queue)  │  │  (PubSub)    │   │
│  └──────────┘  └───────────┘  └──────────┘  └──────────────┘   │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌──────────────┐   │
│  │  Redis   │  │PostgreSQL │  │ Lambda   │  │  EventBridge │   │
│  │ (Cache)  │  │   (SQL)   │  │(.NET Fn) │  │   (Events)   │   │
│  └──────────┘  └───────────┘  └──────────┘  └──────────────┘   │
└──────────────────────────────────────────────────────────────────┘
```

---

## Pattern 1: Event-Driven AI Processing

### Use Cases
- User interaction tracking for recommendations
- Product image processing and tagging
- Order fraud detection
- Review sentiment analysis

### Architecture

```
User Action → API Gateway → Service → SQS Queue → Lambda/Worker → AI Service → DynamoDB
```

### Implementation

#### 1. Event Publishing (Catalog Service)

```csharp
// File: Gearify.CatalogService/Infrastructure/Events/EventPublisher.cs

using Amazon.SQS;
using Amazon.SQS.Model;

public interface IEventPublisher
{
    Task PublishUserInteractionAsync(UserInteractionEvent evt);
    Task PublishProductUpdatedAsync(ProductUpdatedEvent evt);
}

public class SqsEventPublisher : IEventPublisher
{
    private readonly IAmazonSQS _sqs;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqsEventPublisher> _logger;

    private string UserEventsQueueUrl => _configuration["AWS:SQS:UserEventsQueue"];
    private string ProductEventsQueueUrl => _configuration["AWS:SQS:ProductEventsQueue"];

    public SqsEventPublisher(
        IAmazonSQS sqs,
        IConfiguration configuration,
        ILogger<SqsEventPublisher> logger)
    {
        _sqs = sqs;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishUserInteractionAsync(UserInteractionEvent evt)
    {
        try
        {
            var message = new SendMessageRequest
            {
                QueueUrl = UserEventsQueueUrl,
                MessageBody = JsonSerializer.Serialize(evt),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    { "EventType", new MessageAttributeValue { StringValue = evt.EventType, DataType = "String" } },
                    { "TenantId", new MessageAttributeValue { StringValue = evt.TenantId, DataType = "String" } }
                }
            };

            await _sqs.SendMessageAsync(message);

            _logger.LogDebug(
                "Published user interaction event: User={UserId}, Product={ProductId}, Type={EventType}",
                evt.UserId, evt.ProductId, evt.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish user interaction event");
            // Don't throw - event publishing shouldn't break user flow
        }
    }

    public async Task PublishProductUpdatedAsync(ProductUpdatedEvent evt)
    {
        var message = new SendMessageRequest
        {
            QueueUrl = ProductEventsQueueUrl,
            MessageBody = JsonSerializer.Serialize(evt),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "EventType", new MessageAttributeValue { StringValue = "ProductUpdated", DataType = "String" } },
                { "ProductId", new MessageAttributeValue { StringValue = evt.ProductId, DataType = "String" } }
            }
        };

        await _sqs.SendMessageAsync(message);
    }
}

public record UserInteractionEvent
{
    public string UserId { get; init; }
    public string ProductId { get; init; }
    public string TenantId { get; init; }
    public string EventType { get; init; } // view, add_to_cart, purchase
    public decimal? EventValue { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; init; }
}

public record ProductUpdatedEvent
{
    public string ProductId { get; init; }
    public string TenantId { get; init; }
    public string UpdateType { get; init; } // created, updated, deleted
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

#### 2. Middleware for Automatic Event Tracking

```csharp
// File: Gearify.ApiGateway/Middleware/EventTrackingMiddleware.cs

public class EventTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IEventPublisher _eventPublisher;

    public EventTrackingMiddleware(RequestDelegate next, IEventPublisher eventPublisher)
    {
        _next = next;
        _eventPublisher = eventPublisher;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Track after response to not block user
        _ = Task.Run(async () =>
        {
            if (context.Response.StatusCode == 200)
            {
                await TrackEventAsync(context);
            }
        });
    }

    private async Task TrackEventAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        // Track product views
        if (method == "GET" && path.StartsWith("/api/catalog/products/"))
        {
            var productId = path.Split('/').LastOrDefault();
            var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";

            await _eventPublisher.PublishUserInteractionAsync(new UserInteractionEvent
            {
                UserId = userId,
                ProductId = productId,
                TenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default",
                EventType = "view",
                Metadata = new Dictionary<string, string>
                {
                    { "referrer", context.Request.Headers["Referer"].FirstOrDefault() ?? "" },
                    { "userAgent", context.Request.Headers["User-Agent"].FirstOrDefault() ?? "" }
                }
            });
        }

        // Track cart additions
        if (method == "POST" && path.Contains("/cart/items"))
        {
            // Extract product ID from request body
            // Publish add_to_cart event
        }
    }
}

// Register in Startup.cs
public void Configure(IApplicationBuilder app)
{
    app.UseMiddleware<EventTrackingMiddleware>();
}
```

#### 3. Background Worker (Processes events from SQS)

```csharp
// File: Gearify.ML.Worker/Services/UserInteractionProcessor.cs

using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.PersonalizeEvents;
using Amazon.PersonalizeEvents.Model;

public class UserInteractionProcessor : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonPersonalizeEvents _personalizeEvents;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserInteractionProcessor> _logger;

    private string QueueUrl => _configuration["AWS:SQS:UserEventsQueue"];
    private string TrackingId => _configuration["AWS:Personalize:TrackingId"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20, // Long polling
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqs.ReceiveMessageAsync(request, stoppingToken);

                if (response.Messages.Any())
                {
                    await ProcessMessagesAsync(response.Messages, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user interaction events");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMessagesAsync(List<Message> messages, CancellationToken cancellationToken)
    {
        var events = new List<Event>();

        foreach (var message in messages)
        {
            try
            {
                var interaction = JsonSerializer.Deserialize<UserInteractionEvent>(message.Body);

                events.Add(new Event
                {
                    EventType = interaction.EventType,
                    UserId = interaction.UserId,
                    ItemId = interaction.ProductId,
                    SentAt = interaction.Timestamp,
                    Properties = interaction.EventValue.HasValue
                        ? JsonSerializer.Serialize(new { eventValue = interaction.EventValue.Value })
                        : null
                });

                // Delete message after successful processing
                await _sqs.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = QueueUrl,
                    ReceiptHandle = message.ReceiptHandle
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
            }
        }

        // Batch send to Personalize
        if (events.Any())
        {
            await _personalizeEvents.PutEventsAsync(new PutEventsRequest
            {
                TrackingId = TrackingId,
                UserId = events.First().UserId,
                SessionId = Guid.NewGuid().ToString(),
                EventList = events
            }, cancellationToken);

            _logger.LogInformation("Sent {Count} events to AWS Personalize", events.Count);
        }
    }
}
```

---

## Pattern 2: Synchronous AI Enrichment

### Use Cases
- Real-time fraud detection during checkout
- NLP query understanding for search
- Image analysis on upload
- Sentiment analysis on review submission

### Architecture

```
API Request → Service → AI Service (sync call) → Response enriched with AI data
```

### Implementation

#### Fraud Detection in Order Service

```csharp
// File: Gearify.OrderService/Application/Commands/CreateOrderCommandHandler.cs

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IFraudDetectionService _fraudDetection;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public async Task<Result<Order>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Create order object
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Items = command.Items,
            TotalAmount = command.Items.Sum(i => i.Price * i.Quantity),
            ShippingAddress = command.ShippingAddress,
            PaymentMethod = command.PaymentMethod,
            CreatedAt = DateTime.UtcNow
        };

        // 2. SYNCHRONOUS AI CALL - Fraud detection
        var fraudAssessment = await _fraudDetection.AssessOrderAsync(order, cancellationToken);

        if (fraudAssessment.RiskLevel == RiskLevel.High)
        {
            _logger.LogWarning(
                "Order {OrderId} blocked due to high fraud risk. Score: {Score}",
                order.Id, fraudAssessment.RiskScore);

            return Result<Order>.Failure("Your order could not be processed. Please contact support.");
        }

        if (fraudAssessment.RiskLevel == RiskLevel.Medium)
        {
            order.Status = OrderStatus.PendingReview;
            order.FraudAssessment = fraudAssessment;

            // Notify fraud team (async)
            _ = _notificationService.NotifyFraudTeamAsync(order, fraudAssessment);
        }
        else
        {
            order.Status = OrderStatus.Confirmed;
        }

        // 3. Save order
        await _orderRepository.CreateAsync(order, cancellationToken);

        return Result<Order>.Success(order);
    }
}
```

#### NLP Query Understanding in Search

```csharp
// File: Gearify.CatalogService/Application/Queries/SmartSearchQueryHandler.cs

public class SmartSearchQueryHandler : IRequestHandler<SmartSearchQuery, SearchResults>
{
    private readonly ISearchService _searchService;
    private readonly IQueryUnderstandingService _queryUnderstanding;

    public async Task<SearchResults> Handle(
        SmartSearchQuery query,
        CancellationToken cancellationToken)
    {
        // 1. SYNCHRONOUS AI CALL - Understand the query using NLP
        var searchIntent = await _queryUnderstanding.AnalyzeQueryAsync(
            query.QueryText,
            cancellationToken);

        // 2. Build search request with AI-extracted filters
        var searchRequest = new ProductSearchRequest
        {
            Query = query.QueryText,
            Category = searchIntent.Filters.Category,
            Brands = searchIntent.Filters.Brands,
            MinPrice = searchIntent.Filters.MinPrice,
            MaxPrice = searchIntent.Filters.MaxPrice,
            SortBy = DetermineSortOrder(searchIntent.Sentiment),
            Page = query.Page,
            PageSize = query.PageSize
        };

        // 3. Execute search
        var results = await _searchService.SearchAsync(searchRequest, cancellationToken);

        return new SearchResults
        {
            Products = results.Products,
            TotalCount = results.TotalCount,
            AppliedFilters = searchIntent.Filters,
            QueryUnderstanding = searchIntent
        };
    }

    private SortOrder DetermineSortOrder(Sentiment sentiment)
    {
        // Buying intent → Sort by relevance + rating
        // Browsing intent → Sort by popularity
        return sentiment == Sentiment.POSITIVE
            ? SortOrder.RelevanceAndRating
            : SortOrder.Popularity;
    }
}
```

---

## Pattern 3: Cached AI Predictions

### Use Cases
- Product recommendations
- Similar items
- Demand forecasts
- Customer segmentation

### Architecture

```
Request → Check Redis → (Miss) → AI Service → Store in Redis → Return
                     → (Hit) → Return from cache
```

### Implementation

#### Multi-Layer Caching Strategy

```csharp
// File: Gearify.Shared/Caching/CachedAIService.cs

public abstract class CachedAIService<TRequest, TResponse>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger _logger;
    protected abstract TimeSpan CacheDuration { get; }
    protected abstract string CacheKeyPrefix { get; }

    protected CachedAIService(IDistributedCache cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected async Task<TResponse> GetOrComputeAsync(
        string cacheKey,
        Func<Task<TResponse>> computeFunc,
        CancellationToken cancellationToken = default)
    {
        // Try L1 cache (Redis)
        var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedValue != null)
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<TResponse>(cachedValue);
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);

        // Compute from AI service
        var result = await computeFunc();

        // Store in cache
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            },
            cancellationToken);

        return result;
    }

    protected string BuildCacheKey(params object[] parts)
    {
        var key = $"{CacheKeyPrefix}:{string.Join(":", parts)}";
        return key;
    }

    protected async Task InvalidateCacheAsync(string pattern)
    {
        // Pattern-based invalidation
        // Requires Redis SCAN command support
        _logger.LogInformation("Invalidating cache for pattern: {Pattern}", pattern);
        // Implementation depends on caching library
    }
}

// Usage example
public class CachedRecommendationService : CachedAIService<string, List<ProductRecommendation>>
{
    private readonly IAmazonPersonalizeRuntime _personalize;

    protected override TimeSpan CacheDuration => TimeSpan.FromHours(1);
    protected override string CacheKeyPrefix => "rec";

    public async Task<List<ProductRecommendation>> GetRecommendationsAsync(
        string userId,
        int numResults = 10)
    {
        var cacheKey = BuildCacheKey("user", userId, numResults);

        return await GetOrComputeAsync(
            cacheKey,
            async () =>
            {
                // Call AWS Personalize
                var response = await _personalize.GetRecommendationsAsync(new GetRecommendationsRequest
                {
                    CampaignArn = _campaignArn,
                    UserId = userId,
                    NumResults = numResults
                });

                return await EnrichWithProductDataAsync(response.ItemList);
            });
    }

    public async Task InvalidateUserRecommendationsAsync(string userId)
    {
        await InvalidateCacheAsync($"rec:user:{userId}:*");
    }
}
```

---

## Pattern 4: Batch AI Processing

### Use Cases
- Daily demand forecasting
- Bulk image processing
- Nightly recommendation model updates
- Customer segmentation refresh

### Architecture

```
Scheduled Job → Batch Processor → AI Service (batch API) → Store Results → Notify
```

### Implementation

#### Hangfire Scheduled Jobs

```csharp
// File: Gearify.ML.Jobs/DailyDemandForecastJob.cs

using Hangfire;
using Amazon.ForecastService;

public class DailyDemandForecastJob
{
    private readonly IForecastService _forecastService;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DailyDemandForecastJob> _logger;

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting daily demand forecast job");

        var startTime = DateTime.UtcNow;

        try
        {
            // 1. Get all active products
            var products = await _productRepository.GetAllActiveAsync();

            // 2. Generate forecasts in batches
            var batchSize = 100;
            var forecasts = new List<DemandForecast>();

            for (int i = 0; i < products.Count; i += batchSize)
            {
                var batch = products.Skip(i).Take(batchSize).ToList();

                var batchForecasts = await _forecastService.GetBatchForecastAsync(
                    productIds: batch.Select(p => p.Id).ToList(),
                    daysAhead: 30
                );

                forecasts.AddRange(batchForecasts);

                _logger.LogInformation(
                    "Processed batch {BatchNumber}/{TotalBatches}",
                    (i / batchSize) + 1,
                    (products.Count + batchSize - 1) / batchSize);
            }

            // 3. Generate inventory recommendations
            var recommendations = new List<InventoryRecommendation>();

            foreach (var forecast in forecasts)
            {
                var currentStock = await _inventoryRepository.GetStockLevelAsync(forecast.ProductId);

                if (currentStock < forecast.PredictedDemand30Days * 0.5)
                {
                    recommendations.Add(new InventoryRecommendation
                    {
                        ProductId = forecast.ProductId,
                        CurrentStock = currentStock,
                        ForecastedDemand = forecast.PredictedDemand30Days,
                        RecommendedOrderQuantity = forecast.PredictedDemand30Days - currentStock,
                        Urgency = currentStock < forecast.PredictedDemand30Days * 0.25 ? "High" : "Medium"
                    });
                }
            }

            // 4. Store forecasts
            await _inventoryRepository.StoreForecastsAsync(forecasts);

            // 5. Notify inventory team
            if (recommendations.Any())
            {
                await _notificationService.NotifyInventoryTeamAsync(recommendations);
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Daily demand forecast job completed. Processed {ProductCount} products in {Duration}. {RecommendationCount} restock recommendations generated.",
                products.Count, duration, recommendations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily demand forecast job failed");
            throw; // Hangfire will retry
        }
    }
}

// Register in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddHangfire(config => config
        .UseRedisStorage(Configuration["Redis:ConnectionString"]));

    services.AddHangfireServer();
}

public void Configure(IApplicationBuilder app)
{
    // Schedule jobs
    RecurringJob.AddOrUpdate<DailyDemandForecastJob>(
        "daily-demand-forecast",
        job => job.ExecuteAsync(),
        Cron.Daily(2) // Run at 2 AM daily
    );

    RecurringJob.AddOrUpdate<WeeklyCustomerSegmentationJob>(
        "weekly-customer-segmentation",
        job => job.ExecuteAsync(),
        Cron.Weekly(DayOfWeek.Monday, 3) // Monday 3 AM
    );
}
```

---

## Pattern 5: Circuit Breaker for AI Services

### Purpose
Protect against AI service failures and ensure graceful degradation.

### Implementation

```csharp
// File: Gearify.Shared/Resilience/AICircuitBreakerPolicy.cs

using Polly;
using Polly.CircuitBreaker;

public class AICircuitBreakerPolicy
{
    public static AsyncCircuitBreakerPolicy Create(
        string serviceName,
        ILogger logger)
    {
        return Policy
            .Handle<AmazonServiceException>()
            .Or<TimeoutException>()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    logger.LogWarning(
                        "Circuit breaker opened for {ServiceName}. Duration: {Duration}. Exception: {Exception}",
                        serviceName, duration, exception.Message);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset for {ServiceName}", serviceName);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open for {ServiceName}", serviceName);
                }
            );
    }
}

// Usage
public class ResilientRecommendationService
{
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger _logger;

    public ResilientRecommendationService(
        IRecommendationService recommendationService,
        ILogger<ResilientRecommendationService> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
        _circuitBreaker = AICircuitBreakerPolicy.Create("Personalize", logger);
    }

    public async Task<List<ProductRecommendation>> GetRecommendationsAsync(
        string userId,
        int numResults = 10)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
                await _recommendationService.GetPersonalizedRecommendationsAsync(userId, numResults));
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Circuit breaker is open. Using fallback recommendations for user {UserId}", userId);

            // Fallback to rule-based recommendations
            return await GetFallbackRecommendationsAsync(userId, numResults);
        }
    }

    private async Task<List<ProductRecommendation>> GetFallbackRecommendationsAsync(
        string userId,
        int numResults)
    {
        // Return popular/trending products when AI service is down
        _logger.LogInformation("Serving fallback recommendations for user {UserId}", userId);
        return await _recommendationService.GetPopularProductsAsync(numResults);
    }
}
```

---

## Pattern 6: AI Model Versioning

### Use Cases
- A/B testing different AI models
- Gradual rollout of new models
- Rollback capability

### Implementation

```csharp
// File: Gearify.Shared/AI/ModelVersionManager.cs

public interface IModelVersionManager
{
    Task<string> GetActiveCampaignArnAsync(string modelType);
    Task<ModelVersion> GetModelVersionAsync(string userId, string modelType);
    Task PromoteModelVersionAsync(string modelType, string versionId);
}

public class ModelVersionManager : IModelVersionManager
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly IExperimentService _experimentService;

    public async Task<string> GetActiveCampaignArnAsync(string modelType)
    {
        var cacheKey = $"model:active:{modelType}";

        var cachedArn = await _cache.GetStringAsync(cacheKey);
        if (cachedArn != null)
            return cachedArn;

        // Fetch from configuration
        var arn = _configuration[$"AWS:Personalize:Campaigns:{modelType}"];

        await _cache.SetStringAsync(cacheKey, arn, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });

        return arn;
    }

    public async Task<ModelVersion> GetModelVersionAsync(string userId, string modelType)
    {
        // Check if user is in an A/B test
        var experimentVariant = await _experimentService.GetVariantAsync(userId, $"model-{modelType}");

        return experimentVariant switch
        {
            "v2" => new ModelVersion
            {
                Version = "v2",
                CampaignArn = _configuration[$"AWS:Personalize:Campaigns:{modelType}-v2"]
            },
            "v1" or _ => new ModelVersion
            {
                Version = "v1",
                CampaignArn = _configuration[$"AWS:Personalize:Campaigns:{modelType}-v1"]
            }
        };
    }

    public async Task PromoteModelVersionAsync(string modelType, string versionId)
    {
        var newArn = _configuration[$"AWS:Personalize:Campaigns:{modelType}-{versionId}"];

        // Update configuration (in production, update in DynamoDB/Parameter Store)
        _configuration[$"AWS:Personalize:Campaigns:{modelType}"] = newArn;

        // Invalidate cache
        await _cache.RemoveAsync($"model:active:{modelType}");

        // Log model promotion
        _logger.LogInformation(
            "Promoted model version: Type={ModelType}, Version={Version}, ARN={ARN}",
            modelType, versionId, newArn);
    }
}

// Usage in service
public class VersionedRecommendationService
{
    private readonly IAmazonPersonalizeRuntime _personalize;
    private readonly IModelVersionManager _versionManager;

    public async Task<List<ProductRecommendation>> GetRecommendationsAsync(
        string userId,
        int numResults = 10)
    {
        // Get the appropriate model version for this user
        var modelVersion = await _versionManager.GetModelVersionAsync(userId, "user-personalization");

        var request = new GetRecommendationsRequest
        {
            CampaignArn = modelVersion.CampaignArn,
            UserId = userId,
            NumResults = numResults
        };

        var response = await _personalize.GetRecommendationsAsync(request);

        // Track which model version was used (for metrics)
        await _metricsService.RecordModelUsageAsync(userId, modelVersion.Version);

        return await EnrichRecommendationsAsync(response.ItemList);
    }
}
```

---

## Pattern 7: LocalStack Development Setup

### docker-compose.yml Enhancement

```yaml
# File: gearify-umbrella/docker-compose.yml

services:
  localstack:
    image: localstack/localstack-pro:latest
    ports:
      - "4566:4566"
      - "4571:4571"
    environment:
      - SERVICES=s3,dynamodb,sqs,sns,ses,personalize,comprehend,rekognition,lex,forecast,frauddetector
      - DEBUG=1
      - LOCALSTACK_API_KEY=${LOCALSTACK_API_KEY}
      - PERSISTENCE=1
      - LAMBDA_EXECUTOR=docker
      - LAMBDA_RUNTIME_ENVIRONMENT_TIMEOUT=300
    volumes:
      - "./localstack/init-aws.sh:/etc/localstack/init/ready.d/init-aws.sh"
      - "./localstack/data:/var/lib/localstack"
      - "/var/run/docker.sock:/var/run/docker.sock"

  # ML Worker Service
  ml-worker:
    build:
      context: ../gearify-ml-worker
      dockerfile: Dockerfile
    environment:
      - AWS_ACCESS_KEY_ID=test
      - AWS_SECRET_ACCESS_KEY=test
      - AWS_REGION=us-east-1
      - AWS_ENDPOINT=http://localstack:4566
      - REDIS_URL=redis:6379
      - SQS_USER_EVENTS_QUEUE=http://localstack:4566/000000000000/gearify-user-events-queue
    depends_on:
      - localstack
      - redis
```

### LocalStack Initialization Script Enhancement

```bash
# File: gearify-umbrella/localstack/init-aws.sh

#!/bin/bash

echo "Setting up AI/ML resources in LocalStack..."

# Create SQS queues for event processing
echo "Creating SQS queues..."
awslocal sqs create-queue --queue-name gearify-user-events-queue
awslocal sqs create-queue --queue-name gearify-product-events-queue
awslocal sqs create-queue --queue-name gearify-ml-processing-queue

# Create S3 bucket for ML data
echo "Creating S3 bucket for ML data..."
awslocal s3 mb s3://gearify-ml-data

# Upload sample data
awslocal s3 cp /etc/localstack/data/ml/interactions.csv s3://gearify-ml-data/personalize/interactions.csv

# Create AWS Personalize resources (if supported by LocalStack Pro)
echo "Setting up AWS Personalize..."
# Note: LocalStack Pro has limited Personalize support
# For full testing, use AWS sandbox environment

# Create DynamoDB table for user events
echo "Creating user events table..."
awslocal dynamodb create-table \
  --table-name gearify-user-events \
  --attribute-definitions \
    AttributeName=UserId,AttributeType=S \
    AttributeName=Timestamp,AttributeType=N \
  --key-schema \
    AttributeName=UserId,KeyType=HASH \
    AttributeName=Timestamp,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1

echo "AI/ML resources setup complete!"
```

---

## Pattern 8: Monitoring & Observability

### CloudWatch Metrics

```csharp
// File: Gearify.Shared/Observability/AIMetricsPublisher.cs

using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

public interface IAIMetricsPublisher
{
    Task RecordLatencyAsync(string serviceName, double milliseconds);
    Task RecordCacheHitAsync(string serviceName, bool isHit);
    Task RecordAIServiceCallAsync(string serviceName, bool success);
}

public class CloudWatchMetricsPublisher : IAIMetricsPublisher
{
    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly string _namespace = "Gearify/AI";

    public async Task RecordLatencyAsync(string serviceName, double milliseconds)
    {
        await PutMetricAsync(new MetricDatum
        {
            MetricName = "ServiceLatency",
            Value = milliseconds,
            Unit = StandardUnit.Milliseconds,
            Timestamp = DateTime.UtcNow,
            Dimensions = new List<Dimension>
            {
                new Dimension { Name = "ServiceName", Value = serviceName }
            }
        });
    }

    public async Task RecordCacheHitAsync(string serviceName, bool isHit)
    {
        await PutMetricAsync(new MetricDatum
        {
            MetricName = "CacheHitRate",
            Value = isHit ? 1 : 0,
            Unit = StandardUnit.Count,
            Timestamp = DateTime.UtcNow,
            Dimensions = new List<Dimension>
            {
                new Dimension { Name = "ServiceName", Value = serviceName },
                new Dimension { Name = "CacheResult", Value = isHit ? "Hit" : "Miss" }
            }
        });
    }

    public async Task RecordAIServiceCallAsync(string serviceName, bool success)
    {
        await PutMetricAsync(new MetricDatum
        {
            MetricName = "AIServiceCalls",
            Value = 1,
            Unit = StandardUnit.Count,
            Timestamp = DateTime.UtcNow,
            Dimensions = new List<Dimension>
            {
                new Dimension { Name = "ServiceName", Value = serviceName },
                new Dimension { Name = "Status", Value = success ? "Success" : "Failure" }
            }
        });
    }

    private async Task PutMetricAsync(MetricDatum metric)
    {
        await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
        {
            Namespace = _namespace,
            MetricData = new List<MetricDatum> { metric }
        });
    }
}

// Usage with instrumentation
public class InstrumentedRecommendationService
{
    private readonly IRecommendationService _recommendationService;
    private readonly IAIMetricsPublisher _metrics;

    public async Task<List<ProductRecommendation>> GetRecommendationsAsync(
        string userId,
        int numResults = 10)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _recommendationService.GetPersonalizedRecommendationsAsync(userId, numResults);

            stopwatch.Stop();
            await _metrics.RecordLatencyAsync("Personalize", stopwatch.ElapsedMilliseconds);
            await _metrics.RecordAIServiceCallAsync("Personalize", true);

            return result;
        }
        catch (Exception)
        {
            await _metrics.RecordAIServiceCallAsync("Personalize", false);
            throw;
        }
    }
}
```

---

## Best Practices

### 1. Cost Management
- Use caching aggressively (Redis with 1-24 hour TTL)
- Implement batch processing for non-real-time features
- Start with minimum TPS for Personalize campaigns (1 TPS)
- Monitor CloudWatch costs weekly

### 2. Performance
- Cache AI predictions (recommendations, forecasts)
- Use async/background processing for non-critical paths
- Implement request batching where possible
- Set appropriate timeouts (Personalize: 500ms, Comprehend: 1s)

### 3. Reliability
- Always have fallback logic (rule-based recommendations)
- Implement circuit breakers for all AI services
- Use retry policies with exponential backoff
- Monitor error rates and set up alerts

### 4. Data Quality
- Validate data before sending to AI services
- Clean historical data before training models
- Implement data versioning for reproducibility
- Monitor data drift and model performance

### 5. Security
- Use IAM roles, not access keys in production
- Encrypt data in transit and at rest
- Implement rate limiting on AI endpoints
- Audit AI service usage

---

## Testing Strategies

### 1. Unit Testing with Mocks

```csharp
// Mock AWS Personalize
var mockPersonalize = new Mock<IAmazonPersonalizeRuntime>();
mockPersonalize
    .Setup(p => p.GetRecommendationsAsync(It.IsAny<GetRecommendationsRequest>(), default))
    .ReturnsAsync(new GetRecommendationsResponse
    {
        ItemList = new List<PredictedItem>
        {
            new PredictedItem { ItemId = "prod-1", Score = 0.95 }
        }
    });
```

### 2. Integration Testing with LocalStack

```csharp
// Use LocalStack for integration tests
var client = new AmazonPersonalizeRuntimeClient(new AmazonPersonalizeRuntimeConfig
{
    ServiceURL = "http://localhost:4566"
});
```

### 3. Load Testing

```csharp
// Use NBomber or k6 for load testing
var scenario = Scenario.Create("recommendation_load_test", async context =>
{
    var userId = $"user-{context.ScenarioInfo.ThreadId}";
    var response = await _httpClient.GetAsync($"/api/recommendations/for-you?userId={userId}");
    return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(
    Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromMinutes(5))
);
```

---

## Deployment Checklist

- [ ] Configure AWS SDK with proper endpoints
- [ ] Set up IAM roles/policies for AI services
- [ ] Create SQS queues for event processing
- [ ] Set up Redis for caching
- [ ] Deploy background workers (Hangfire)
- [ ] Configure CloudWatch metrics and alarms
- [ ] Set up LocalStack for development
- [ ] Implement circuit breakers
- [ ] Configure retry policies
- [ ] Set up monitoring dashboards
- [ ] Test fallback mechanisms
- [ ] Perform load testing
- [ ] Document AI service limits and quotas

---

**Next Steps**: Review individual feature documentation for detailed implementation guides.
