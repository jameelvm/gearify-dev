# Product Recommendations - Detailed Design

Complete technical specification for the product recommendation engine using AWS Personalize and .NET integration.

## Overview

The recommendation engine powers personalized product suggestions across the Gearify platform, driving cross-sell, upsell, and discovery.

### Business Objectives
- Increase average order value by 20-35%
- Improve product discovery and browsing experience
- Drive cross-sell and complementary product purchases
- Reduce time to purchase decision

### Technical Approach
- **Primary**: AWS Personalize (Managed ML service)
- **Fallback**: ML.NET (Custom collaborative filtering)
- **Hybrid**: Combine collaborative + content-based filtering

---

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                         Frontend (Angular)                      │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐  │
│  │ Product Detail   │  │ Homepage         │  │ Cart Page    │  │
│  │ "Similar Items"  │  │ "For You"        │  │ "Add These"  │  │
│  └────────┬─────────┘  └────────┬─────────┘  └──────┬───────┘  │
└───────────┼────────────────────┼────────────────────┼───────────┘
            │                    │                    │
            └────────────────────┼────────────────────┘
                                 │
            ┌────────────────────▼────────────────────┐
            │         API Gateway (YARP)              │
            │    /catalog/recommendations/*           │
            └────────────────────┬────────────────────┘
                                 │
            ┌────────────────────▼────────────────────┐
            │      Catalog Service (.NET 8.0)         │
            │  ┌────────────────────────────────────┐ │
            │  │  RecommendationService             │ │
            │  │  ┌──────────────┐ ┌──────────────┐ │ │
            │  │  │ AWS          │ │ Redis        │ │ │
            │  │  │ Personalize  │ │ Cache        │ │ │
            │  │  └──────┬───────┘ └───────▲──────┘ │ │
            │  └─────────┼─────────────────┼────────┘ │
            └────────────┼─────────────────┼──────────┘
                         │                 │
            ┌────────────▼─────────────────┼──────────┐
            │    AWS Personalize           │          │
            │  ┌───────────────────────┐   │          │
            │  │ Dataset Group         │   │          │
            │  │ - Interactions        │   │          │
            │  │ - Items (Products)    │   │          │
            │  │ - Users               │   │          │
            │  └───────────────────────┘   │          │
            │  ┌───────────────────────┐   │          │
            │  │ Solutions             │   │          │
            │  │ - User Personalization│   │          │
            │  │ - Similar Items       │   │          │
            │  │ - Personalized Ranking│   │          │
            │  └───────────────────────┘   │          │
            │  ┌───────────────────────┐   │          │
            │  │ Campaigns (TPS=1)     │   │          │
            │  └───────────────────────┘   │          │
            └───────────────────────────────┘          │
                         │                             │
            ┌────────────▼─────────────────────────────▼──┐
            │           Redis Cache                       │
            │  Key: "rec:{userId}:{type}" TTL: 1h         │
            └─────────────────────────────────────────────┘
```

---

## AWS Personalize Setup

### 1. Dataset Group Creation

```bash
# Create dataset group
awslocal personalize create-dataset-group \
  --name gearify-recommendations \
  --region us-east-1
```

**Response**:
```json
{
  "datasetGroupArn": "arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations"
}
```

### 2. Schema Definitions

#### Interactions Schema
```json
{
  "type": "record",
  "name": "Interactions",
  "namespace": "com.gearify.personalize",
  "fields": [
    {
      "name": "USER_ID",
      "type": "string"
    },
    {
      "name": "ITEM_ID",
      "type": "string"
    },
    {
      "name": "TIMESTAMP",
      "type": "long"
    },
    {
      "name": "EVENT_TYPE",
      "type": "string"
    },
    {
      "name": "EVENT_VALUE",
      "type": "float"
    }
  ],
  "version": "1.0"
}
```

**Event Types**:
- `view` (weight: 1) - Product page view
- `add_to_cart` (weight: 3) - Added to cart
- `purchase` (weight: 5) - Completed purchase
- `favorite` (weight: 2) - Added to wishlist

#### Items (Products) Schema
```json
{
  "type": "record",
  "name": "Items",
  "namespace": "com.gearify.personalize",
  "fields": [
    {
      "name": "ITEM_ID",
      "type": "string"
    },
    {
      "name": "CATEGORY",
      "type": "string",
      "categorical": true
    },
    {
      "name": "BRAND",
      "type": "string",
      "categorical": true
    },
    {
      "name": "PRICE",
      "type": "float"
    },
    {
      "name": "CREATION_TIMESTAMP",
      "type": "long"
    }
  ],
  "version": "1.0"
}
```

#### Users Schema
```json
{
  "type": "record",
  "name": "Users",
  "namespace": "com.gearify.personalize",
  "fields": [
    {
      "name": "USER_ID",
      "type": "string"
    },
    {
      "name": "AGE_GROUP",
      "type": "string",
      "categorical": true
    },
    {
      "name": "PREFERRED_CATEGORY",
      "type": "string",
      "categorical": true
    },
    {
      "name": "CUSTOMER_SEGMENT",
      "type": "string",
      "categorical": true
    }
  ],
  "version": "1.0"
}
```

### 3. Data Preparation

#### Export Historical Data (.NET Service)

```csharp
// File: Gearify.CatalogService/Infrastructure/ML/DataExporter.cs

using System.Globalization;
using CsvHelper;

public class PersonalizeDataExporter
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUserActivityRepository _userActivityRepository;
    private readonly IProductRepository _productRepository;
    private readonly IS3Client _s3Client;

    public async Task ExportInteractionsAsync(DateTime since)
    {
        var interactions = new List<InteractionRecord>();

        // Export purchases
        var orders = await _orderRepository.GetAllSinceAsync(since);
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                interactions.Add(new InteractionRecord
                {
                    UserId = order.UserId,
                    ItemId = item.ProductId,
                    Timestamp = new DateTimeOffset(order.CreatedAt).ToUnixTimeSeconds(),
                    EventType = "purchase",
                    EventValue = (float)item.Price
                });
            }
        }

        // Export views and cart additions
        var activities = await _userActivityRepository.GetAllSinceAsync(since);
        foreach (var activity in activities)
        {
            interactions.Add(new InteractionRecord
            {
                UserId = activity.UserId,
                ItemId = activity.ProductId,
                Timestamp = new DateTimeOffset(activity.Timestamp).ToUnixTimeSeconds(),
                EventType = activity.EventType, // "view", "add_to_cart"
                EventValue = 1.0f
            });
        }

        // Write to CSV
        var csvPath = Path.GetTempFileName();
        using (var writer = new StreamWriter(csvPath))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(interactions);
        }

        // Upload to S3
        await _s3Client.UploadFileAsync(
            bucketName: "gearify-ml-data",
            key: $"personalize/interactions-{DateTime.UtcNow:yyyyMMdd}.csv",
            filePath: csvPath
        );
    }

    public async Task ExportItemsAsync()
    {
        var products = await _productRepository.GetAllAsync();

        var itemRecords = products.Select(p => new ItemRecord
        {
            ItemId = p.Id,
            Category = p.Category,
            Brand = p.Brand,
            Price = (float)p.Price,
            CreationTimestamp = new DateTimeOffset(p.CreatedAt).ToUnixTimeSeconds()
        }).ToList();

        var csvPath = Path.GetTempFileName();
        using (var writer = new StreamWriter(csvPath))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(itemRecords);
        }

        await _s3Client.UploadFileAsync(
            bucketName: "gearify-ml-data",
            key: "personalize/items.csv",
            filePath: csvPath
        );
    }

    public async Task ExportUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();

        var userRecords = users.Select(u => new UserRecord
        {
            UserId = u.Id,
            AgeGroup = DetermineAgeGroup(u.DateOfBirth),
            PreferredCategory = DeterminePreferredCategory(u.Id),
            CustomerSegment = DetermineCustomerSegment(u)
        }).ToList();

        var csvPath = Path.GetTempFileName();
        using (var writer = new StreamWriter(csvPath))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(userRecords);
        }

        await _s3Client.UploadFileAsync(
            bucketName: "gearify-ml-data",
            key: "personalize/users.csv",
            filePath: csvPath
        );
    }

    private string DetermineAgeGroup(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue) return "unknown";

        var age = DateTime.UtcNow.Year - dateOfBirth.Value.Year;

        return age switch
        {
            < 18 => "junior",
            < 30 => "young_adult",
            < 45 => "adult",
            _ => "senior"
        };
    }

    private async Task<string> DeterminePreferredCategory(string userId)
    {
        var recentOrders = await _orderRepository.GetRecentByUserAsync(userId, limit: 10);
        var categoryCount = recentOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.Category)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return categoryCount?.Key ?? "none";
    }

    private string DetermineCustomerSegment(User user)
    {
        var totalSpent = user.TotalSpent;
        var orderCount = user.OrderCount;

        if (orderCount == 0) return "new";
        if (orderCount > 10 && totalSpent > 50000) return "vip";
        if (orderCount > 5) return "loyal";
        return "regular";
    }
}

public record InteractionRecord
{
    [Name("USER_ID")]
    public string UserId { get; init; }

    [Name("ITEM_ID")]
    public string ItemId { get; init; }

    [Name("TIMESTAMP")]
    public long Timestamp { get; init; }

    [Name("EVENT_TYPE")]
    public string EventType { get; init; }

    [Name("EVENT_VALUE")]
    public float EventValue { get; init; }
}

public record ItemRecord
{
    [Name("ITEM_ID")]
    public string ItemId { get; init; }

    [Name("CATEGORY")]
    public string Category { get; init; }

    [Name("BRAND")]
    public string Brand { get; init; }

    [Name("PRICE")]
    public float Price { get; init; }

    [Name("CREATION_TIMESTAMP")]
    public long CreationTimestamp { get; init; }
}

public record UserRecord
{
    [Name("USER_ID")]
    public string UserId { get; init; }

    [Name("AGE_GROUP")]
    public string AgeGroup { get; init; }

    [Name("PREFERRED_CATEGORY")]
    public string PreferredCategory { get; init; }

    [Name("CUSTOMER_SEGMENT")]
    public string CustomerSegment { get; init; }
}
```

### 4. Create Datasets

```bash
# Create interactions dataset
awslocal personalize create-dataset \
  --name gearify-interactions \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --dataset-type INTERACTIONS \
  --schema-arn arn:aws:personalize:us-east-1:000000000000:schema/interactions-schema

# Create items dataset
awslocal personalize create-dataset \
  --name gearify-items \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --dataset-type ITEMS \
  --schema-arn arn:aws:personalize:us-east-1:000000000000:schema/items-schema

# Create users dataset
awslocal personalize create-dataset \
  --name gearify-users \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --dataset-type USERS \
  --schema-arn arn:aws:personalize:us-east-1:000000000000:schema/users-schema
```

### 5. Import Data

```bash
# Import interactions
awslocal personalize create-dataset-import-job \
  --job-name gearify-interactions-import \
  --dataset-arn arn:aws:personalize:us-east-1:000000000000:dataset/gearify-interactions \
  --data-source dataLocation=s3://gearify-ml-data/personalize/interactions-20260101.csv \
  --role-arn arn:aws:iam::000000000000:role/PersonalizeRole

# Import items
awslocal personalize create-dataset-import-job \
  --job-name gearify-items-import \
  --dataset-arn arn:aws:personalize:us-east-1:000000000000:dataset/gearify-items \
  --data-source dataLocation=s3://gearify-ml-data/personalize/items.csv \
  --role-arn arn:aws:iam::000000000000:role/PersonalizeRole

# Import users
awslocal personalize create-dataset-import-job \
  --job-name gearify-users-import \
  --dataset-arn arn:aws:personalize:us-east-1:000000000000:dataset/gearify-users \
  --data-source dataLocation=s3://gearify-ml-data/personalize/users.csv \
  --role-arn arn:aws:iam::000000000000:role/PersonalizeRole
```

### 6. Create Solutions

#### User Personalization (Recommended for You)
```bash
awslocal personalize create-solution \
  --name gearify-user-personalization \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --recipe-arn arn:aws:personalize:::recipe/aws-user-personalization
```

#### Similar Items (Product Detail Page)
```bash
awslocal personalize create-solution \
  --name gearify-similar-items \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --recipe-arn arn:aws:personalize:::recipe/aws-sims
```

#### Personalized Ranking (Rerank search results)
```bash
awslocal personalize create-solution \
  --name gearify-personalized-ranking \
  --dataset-group-arn arn:aws:personalize:us-east-1:000000000000:dataset-group/gearify-recommendations \
  --recipe-arn arn:aws:personalize:::recipe/aws-personalized-ranking
```

### 7. Create Solution Versions (Train Models)

```bash
# Train user personalization
awslocal personalize create-solution-version \
  --solution-arn arn:aws:personalize:us-east-1:000000000000:solution/gearify-user-personalization

# Train similar items
awslocal personalize create-solution-version \
  --solution-arn arn:aws:personalize:us-east-1:000000000000:solution/gearify-similar-items

# Training takes 1-2 hours in production, instant in LocalStack
```

### 8. Create Campaigns (Inference Endpoints)

```bash
# User personalization campaign
awslocal personalize create-campaign \
  --name gearify-user-personalization-campaign \
  --solution-version-arn arn:aws:personalize:us-east-1:000000000000:solution/gearify-user-personalization/version/1 \
  --min-provisioned-tps 1

# Similar items campaign
awslocal personalize create-campaign \
  --name gearify-similar-items-campaign \
  --solution-version-arn arn:aws:personalize:us-east-1:000000000000:solution/gearify-similar-items/version/1 \
  --min-provisioned-tps 1
```

---

## .NET Implementation

### 1. Service Registration

```csharp
// File: Gearify.CatalogService/Startup.cs

using Amazon.PersonalizeRuntime;

public void ConfigureServices(IServiceCollection services)
{
    // AWS Personalize Runtime client
    services.AddAWSService<IAmazonPersonalizeRuntime>();

    // Recommendation service
    services.AddScoped<IRecommendationService, RecommendationService>();

    // Redis caching
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = Configuration["Redis:ConnectionString"];
        options.InstanceName = "Gearify:";
    });
}
```

### 2. Configuration

```json
// File: appsettings.json

{
  "AWS": {
    "Personalize": {
      "Campaigns": {
        "UserPersonalization": "arn:aws:personalize:us-east-1:000000000000:campaign/gearify-user-personalization-campaign",
        "SimilarItems": "arn:aws:personalize:us-east-1:000000000000:campaign/gearify-similar-items-campaign",
        "PersonalizedRanking": "arn:aws:personalize:us-east-1:000000000000:campaign/gearify-personalized-ranking-campaign"
      },
      "CacheDuration": "01:00:00"
    }
  }
}
```

### 3. Recommendation Service

```csharp
// File: Gearify.CatalogService/Application/Services/RecommendationService.cs

using Amazon.PersonalizeRuntime;
using Amazon.PersonalizeRuntime.Model;
using Microsoft.Extensions.Caching.Distributed;

public interface IRecommendationService
{
    Task<List<ProductRecommendation>> GetPersonalizedRecommendationsAsync(
        string userId,
        int numResults = 10,
        CancellationToken cancellationToken = default);

    Task<List<ProductRecommendation>> GetSimilarItemsAsync(
        string itemId,
        int numResults = 10,
        CancellationToken cancellationToken = default);

    Task<List<ProductRecommendation>> GetComplementaryItemsAsync(
        string itemId,
        int numResults = 10,
        CancellationToken cancellationToken = default);

    Task<List<string>> RerankPersonalizedAsync(
        string userId,
        List<string> itemIds,
        CancellationToken cancellationToken = default);

    Task RecordInteractionAsync(
        string userId,
        string itemId,
        string eventType,
        float? eventValue = null,
        CancellationToken cancellationToken = default);
}

public class RecommendationService : IRecommendationService
{
    private readonly IAmazonPersonalizeRuntime _personalizeRuntime;
    private readonly IAmazonPersonalizeEvents _personalizeEvents;
    private readonly IProductRepository _productRepository;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecommendationService> _logger;

    private string UserPersonalizationCampaignArn =>
        _configuration["AWS:Personalize:Campaigns:UserPersonalization"];

    private string SimilarItemsCampaignArn =>
        _configuration["AWS:Personalize:Campaigns:SimilarItems"];

    private string PersonalizedRankingCampaignArn =>
        _configuration["AWS:Personalize:Campaigns:PersonalizedRanking"];

    public RecommendationService(
        IAmazonPersonalizeRuntime personalizeRuntime,
        IAmazonPersonalizeEvents personalizeEvents,
        IProductRepository productRepository,
        IDistributedCache cache,
        IConfiguration configuration,
        ILogger<RecommendationService> logger)
    {
        _personalizeRuntime = personalizeRuntime;
        _personalizeEvents = personalizeEvents;
        _productRepository = productRepository;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ProductRecommendation>> GetPersonalizedRecommendationsAsync(
        string userId,
        int numResults = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"rec:user:{userId}:personalized";

        // Try cache first
        var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            return JsonSerializer.Deserialize<List<ProductRecommendation>>(cachedResult);
        }

        try
        {
            var request = new GetRecommendationsRequest
            {
                CampaignArn = UserPersonalizationCampaignArn,
                UserId = userId,
                NumResults = numResults
            };

            var response = await _personalizeRuntime.GetRecommendationsAsync(request, cancellationToken);

            var recommendations = await EnrichRecommendationsAsync(
                response.ItemList,
                cancellationToken);

            // Cache for 1 hour
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(recommendations),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                },
                cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} personalized recommendations for user {UserId}",
                recommendations.Count, userId);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get personalized recommendations for user {UserId}", userId);

            // Fallback to popular products
            return await GetFallbackRecommendationsAsync(numResults, cancellationToken);
        }
    }

    public async Task<List<ProductRecommendation>> GetSimilarItemsAsync(
        string itemId,
        int numResults = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"rec:item:{itemId}:similar";

        var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            return JsonSerializer.Deserialize<List<ProductRecommendation>>(cachedResult);
        }

        try
        {
            var request = new GetRecommendationsRequest
            {
                CampaignArn = SimilarItemsCampaignArn,
                ItemId = itemId,
                NumResults = numResults
            };

            var response = await _personalizeRuntime.GetRecommendationsAsync(request, cancellationToken);

            var recommendations = await EnrichRecommendationsAsync(
                response.ItemList,
                cancellationToken);

            // Cache for 24 hours (similar items don't change often)
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(recommendations),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                },
                cancellationToken);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get similar items for {ItemId}", itemId);

            // Fallback to category-based recommendations
            return await GetFallbackSimilarItemsAsync(itemId, numResults, cancellationToken);
        }
    }

    public async Task<List<ProductRecommendation>> GetComplementaryItemsAsync(
        string itemId,
        int numResults = 10,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(itemId, cancellationToken);
        if (product == null) return new List<ProductRecommendation>();

        // Cricket-specific complementary logic
        var complementaryCategories = GetComplementaryCategories(product.Category);

        var complementaryProducts = new List<Product>();

        foreach (var category in complementaryCategories)
        {
            var products = await _productRepository.GetByCategoryAsync(
                category,
                limit: 3,
                cancellationToken: cancellationToken);

            complementaryProducts.AddRange(products);
        }

        return complementaryProducts
            .Take(numResults)
            .Select(p => new ProductRecommendation
            {
                ProductId = p.Id,
                Name = p.Name,
                Price = p.Price,
                ThumbnailUrl = p.ThumbnailUrl,
                Category = p.Category,
                Brand = p.Brand,
                RecommendationReason = $"Complements your {product.Category}"
            })
            .ToList();
    }

    public async Task<List<string>> RerankPersonalizedAsync(
        string userId,
        List<string> itemIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPersonalizedRankingRequest
            {
                CampaignArn = PersonalizedRankingCampaignArn,
                UserId = userId,
                InputList = itemIds
            };

            var response = await _personalizeRuntime.GetPersonalizedRankingAsync(request, cancellationToken);

            return response.PersonalizedRanking.Select(r => r.ItemId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rerank items for user {UserId}", userId);
            return itemIds; // Return original order on failure
        }
    }

    public async Task RecordInteractionAsync(
        string userId,
        string itemId,
        string eventType,
        float? eventValue = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutEventsRequest
            {
                TrackingId = _configuration["AWS:Personalize:TrackingId"],
                UserId = userId,
                SessionId = Guid.NewGuid().ToString(),
                EventList = new List<Event>
                {
                    new Event
                    {
                        EventType = eventType,
                        ItemId = itemId,
                        SentAt = DateTime.UtcNow,
                        Properties = eventValue.HasValue
                            ? JsonSerializer.Serialize(new { eventValue = eventValue.Value })
                            : null
                    }
                }
            };

            await _personalizeEvents.PutEventsAsync(request, cancellationToken);

            _logger.LogDebug(
                "Recorded interaction: User={UserId}, Item={ItemId}, Event={EventType}",
                userId, itemId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record interaction for user {UserId}", userId);
            // Don't throw - interaction tracking shouldn't break user flow
        }
    }

    private async Task<List<ProductRecommendation>> EnrichRecommendationsAsync(
        List<PredictedItem> items,
        CancellationToken cancellationToken)
    {
        var productIds = items.Select(i => i.ItemId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        return items
            .Select(i =>
            {
                var product = products.FirstOrDefault(p => p.Id == i.ItemId);
                if (product == null) return null;

                return new ProductRecommendation
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    ThumbnailUrl = product.ThumbnailUrl,
                    Category = product.Category,
                    Brand = product.Brand,
                    Score = i.Score,
                    RecommendationReason = DetermineRecommendationReason(i.Score)
                };
            })
            .Where(r => r != null)
            .ToList();
    }

    private async Task<List<ProductRecommendation>> GetFallbackRecommendationsAsync(
        int numResults,
        CancellationToken cancellationToken)
    {
        // Fallback to trending/popular products
        var popularProducts = await _productRepository.GetTrendingAsync(
            limit: numResults,
            cancellationToken: cancellationToken);

        return popularProducts.Select(p => new ProductRecommendation
        {
            ProductId = p.Id,
            Name = p.Name,
            Price = p.Price,
            ThumbnailUrl = p.ThumbnailUrl,
            Category = p.Category,
            Brand = p.Brand,
            RecommendationReason = "Popular"
        }).ToList();
    }

    private async Task<List<ProductRecommendation>> GetFallbackSimilarItemsAsync(
        string itemId,
        int numResults,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(itemId, cancellationToken);
        if (product == null) return new List<ProductRecommendation>();

        // Fallback to same category products
        var similarProducts = await _productRepository.GetByCategoryAsync(
            product.Category,
            limit: numResults,
            cancellationToken: cancellationToken);

        return similarProducts
            .Where(p => p.Id != itemId)
            .Select(p => new ProductRecommendation
            {
                ProductId = p.Id,
                Name = p.Name,
                Price = p.Price,
                ThumbnailUrl = p.ThumbnailUrl,
                Category = p.Category,
                Brand = p.Brand,
                RecommendationReason = $"Similar {product.Category}"
            })
            .ToList();
    }

    private List<string> GetComplementaryCategories(string category)
    {
        return category switch
        {
            "Bats" => new List<string> { "Pads", "Gloves", "Helmets", "Bags" },
            "Balls" => new List<string> { "Bats", "Stumps" },
            "Shoes" => new List<string> { "Socks", "Accessories" },
            "Helmets" => new List<string> { "Pads", "Gloves", "Bats" },
            "Pads" => new List<string> { "Gloves", "Bats", "Helmets" },
            "Gloves" => new List<string> { "Pads", "Bats", "Helmets" },
            _ => new List<string>()
        };
    }

    private string DetermineRecommendationReason(double? score)
    {
        if (!score.HasValue) return "Recommended for you";

        return score.Value switch
        {
            > 0.8 => "Highly recommended",
            > 0.6 => "Recommended for you",
            _ => "You might like this"
        };
    }
}

public record ProductRecommendation
{
    public string ProductId { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
    public string ThumbnailUrl { get; init; }
    public string Category { get; init; }
    public string Brand { get; init; }
    public double? Score { get; init; }
    public string RecommendationReason { get; init; }
}
```

### 4. API Controllers

```csharp
// File: Gearify.CatalogService/API/Controllers/RecommendationsController.cs

[ApiController]
[Route("api/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    [HttpGet("for-you")]
    public async Task<ActionResult<List<ProductRecommendation>>> GetPersonalizedRecommendations(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID is required");

        var recommendations = await _recommendationService.GetPersonalizedRecommendationsAsync(
            userId,
            limit,
            cancellationToken);

        return Ok(recommendations);
    }

    [HttpGet("products/{productId}/similar")]
    public async Task<ActionResult<List<ProductRecommendation>>> GetSimilarProducts(
        string productId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await _recommendationService.GetSimilarItemsAsync(
            productId,
            limit,
            cancellationToken);

        return Ok(recommendations);
    }

    [HttpGet("products/{productId}/complementary")]
    public async Task<ActionResult<List<ProductRecommendation>>> GetComplementaryProducts(
        string productId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await _recommendationService.GetComplementaryItemsAsync(
            productId,
            limit,
            cancellationToken);

        return Ok(recommendations);
    }

    [HttpPost("interactions")]
    public async Task<IActionResult> RecordInteraction(
        [FromBody] RecordInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _recommendationService.RecordInteractionAsync(
            request.UserId,
            request.ProductId,
            request.EventType,
            request.EventValue,
            cancellationToken);

        return Accepted();
    }
}

public record RecordInteractionRequest
{
    public string UserId { get; init; }
    public string ProductId { get; init; }
    public string EventType { get; init; }  // "view", "add_to_cart", "purchase"
    public float? EventValue { get; init; }
}
```

---

## Frontend Integration (Angular)

### 1. Recommendations Service

```typescript
// File: gearify-web/src/app/services/recommendations.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProductRecommendation {
  productId: string;
  name: string;
  price: number;
  thumbnailUrl: string;
  category: string;
  brand: string;
  score?: number;
  recommendationReason: string;
}

@Injectable({
  providedIn: 'root'
})
export class RecommendationsService {
  private apiUrl = `${environment.apiGatewayUrl}/catalog/recommendations`;

  constructor(private http: HttpClient) {}

  getPersonalizedRecommendations(userId: string, limit = 10): Observable<ProductRecommendation[]> {
    const headers = new HttpHeaders({ 'X-User-Id': userId });
    return this.http.get<ProductRecommendation[]>(
      `${this.apiUrl}/for-you?limit=${limit}`,
      { headers }
    );
  }

  getSimilarProducts(productId: string, limit = 10): Observable<ProductRecommendation[]> {
    return this.http.get<ProductRecommendation[]>(
      `${this.apiUrl}/products/${productId}/similar?limit=${limit}`
    );
  }

  getComplementaryProducts(productId: string, limit = 10): Observable<ProductRecommendation[]> {
    return this.http.get<ProductRecommendation[]>(
      `${this.apiUrl}/products/${productId}/complementary?limit=${limit}`
    );
  }

  recordInteraction(userId: string, productId: string, eventType: string, eventValue?: number): void {
    this.http.post(`${this.apiUrl}/interactions`, {
      userId,
      productId,
      eventType,
      eventValue
    }).subscribe({
      error: (err) => console.error('Failed to record interaction:', err)
    });
  }
}
```

### 2. Product Detail Component

```typescript
// File: gearify-web/src/app/components/product-detail/product-detail.component.ts

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { RecommendationsService, ProductRecommendation } from '../../services/recommendations.service';

@Component({
  selector: 'app-product-detail',
  templateUrl: './product-detail.component.html'
})
export class ProductDetailComponent implements OnInit {
  productId: string;
  similarProducts: ProductRecommendation[] = [];
  complementaryProducts: ProductRecommendation[] = [];

  constructor(
    private route: ActivatedRoute,
    private recommendationsService: RecommendationsService
  ) {}

  ngOnInit() {
    this.productId = this.route.snapshot.paramMap.get('id');

    // Load similar products
    this.recommendationsService.getSimilarProducts(this.productId, 6)
      .subscribe(products => this.similarProducts = products);

    // Load complementary products (frequently bought together)
    this.recommendationsService.getComplementaryProducts(this.productId, 4)
      .subscribe(products => this.complementaryProducts = products);

    // Record view event
    const userId = this.getUserId(); // From auth service
    this.recommendationsService.recordInteraction(userId, this.productId, 'view');
  }

  onAddToCart() {
    const userId = this.getUserId();
    this.recommendationsService.recordInteraction(userId, this.productId, 'add_to_cart');
  }

  private getUserId(): string {
    // Get from auth service or session
    return localStorage.getItem('userId') || 'anonymous';
  }
}
```

### 3. Homepage Component

```typescript
// File: gearify-web/src/app/components/homepage/homepage.component.ts

@Component({
  selector: 'app-homepage',
  templateUrl: './homepage.component.html'
})
export class HomepageComponent implements OnInit {
  recommendedProducts: ProductRecommendation[] = [];

  constructor(private recommendationsService: RecommendationsService) {}

  ngOnInit() {
    const userId = this.getUserId();

    this.recommendationsService.getPersonalizedRecommendations(userId, 12)
      .subscribe(products => this.recommendedProducts = products);
  }
}
```

### 4. HTML Template

```html
<!-- File: gearify-web/src/app/components/product-detail/product-detail.component.html -->

<div class="product-detail">
  <!-- Main product details -->
  ...

  <!-- Similar Products -->
  <section class="recommendations">
    <h2>Similar Products</h2>
    <div class="product-grid">
      <div *ngFor="let product of similarProducts" class="product-card">
        <img [src]="product.thumbnailUrl" [alt]="product.name">
        <h3>{{ product.name }}</h3>
        <p class="price">₹{{ product.price | number }}</p>
        <span class="recommendation-reason">{{ product.recommendationReason }}</span>
      </div>
    </div>
  </section>

  <!-- Frequently Bought Together -->
  <section class="recommendations">
    <h2>Frequently Bought Together</h2>
    <div class="product-grid">
      <div *ngFor="let product of complementaryProducts" class="product-card">
        <img [src]="product.thumbnailUrl" [alt]="product.name">
        <h3>{{ product.name }}</h3>
        <p class="price">₹{{ product.price | number }}</p>
      </div>
    </div>
  </section>
</div>
```

---

## Testing & Validation

### 1. Unit Tests

```csharp
// File: Gearify.CatalogService.Tests/Services/RecommendationServiceTests.cs

using Xunit;
using Moq;

public class RecommendationServiceTests
{
    [Fact]
    public async Task GetPersonalizedRecommendations_ReturnsFromCache_WhenAvailable()
    {
        // Arrange
        var cachedRecommendations = new List<ProductRecommendation>
        {
            new ProductRecommendation { ProductId = "prod-1", Name = "Test Product" }
        };

        var mockCache = new Mock<IDistributedCache>();
        mockCache
            .Setup(c => c.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync(JsonSerializer.Serialize(cachedRecommendations));

        var service = new RecommendationService(
            Mock.Of<IAmazonPersonalizeRuntime>(),
            Mock.Of<IAmazonPersonalizeEvents>(),
            Mock.Of<IProductRepository>(),
            mockCache.Object,
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<RecommendationService>>()
        );

        // Act
        var result = await service.GetPersonalizedRecommendationsAsync("user-123");

        // Assert
        Assert.Single(result);
        Assert.Equal("prod-1", result[0].ProductId);
    }

    [Fact]
    public async Task GetSimilarItems_CallsPersonalize_WhenCacheMiss()
    {
        // Arrange
        var mockPersonalize = new Mock<IAmazonPersonalizeRuntime>();
        mockPersonalize
            .Setup(p => p.GetRecommendationsAsync(It.IsAny<GetRecommendationsRequest>(), default))
            .ReturnsAsync(new GetRecommendationsResponse
            {
                ItemList = new List<PredictedItem>
                {
                    new PredictedItem { ItemId = "prod-2", Score = 0.95 }
                }
            });

        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<string>>(), default))
            .ReturnsAsync(new List<Product>
            {
                new Product { Id = "prod-2", Name = "Similar Product", Price = 1000 }
            });

        var service = new RecommendationService(
            mockPersonalize.Object,
            Mock.Of<IAmazonPersonalizeEvents>(),
            mockProductRepo.Object,
            Mock.Of<IDistributedCache>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<RecommendationService>>()
        );

        // Act
        var result = await service.GetSimilarItemsAsync("prod-1");

        // Assert
        Assert.Single(result);
        Assert.Equal("prod-2", result[0].ProductId);
        mockPersonalize.Verify(
            p => p.GetRecommendationsAsync(It.IsAny<GetRecommendationsRequest>(), default),
            Times.Once
        );
    }
}
```

### 2. Integration Tests

```csharp
// File: Gearify.IntegrationTests/RecommendationsEndpointTests.cs

public class RecommendationsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RecommendationsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPersonalizedRecommendations_ReturnsOk()
    {
        // Act
        _client.DefaultRequestHeaders.Add("X-User-Id", "test-user-123");
        var response = await _client.GetAsync("/api/recommendations/for-you?limit=5");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var recommendations = JsonSerializer.Deserialize<List<ProductRecommendation>>(content);

        Assert.NotNull(recommendations);
        Assert.True(recommendations.Count <= 5);
    }
}
```

---

## Performance Optimization

### 1. Caching Strategy

**Cache Keys**:
- `rec:user:{userId}:personalized` - TTL: 1 hour
- `rec:item:{itemId}:similar` - TTL: 24 hours
- `rec:item:{itemId}:complementary` - TTL: 6 hours

**Cache Invalidation**:
- User recommendations: Invalidate on new purchase
- Similar items: Invalidate on product update
- Complementary items: Invalidate on category changes

### 2. Batch Processing

For homepage with multiple users:
```csharp
public async Task<Dictionary<string, List<ProductRecommendation>>> GetBatchRecommendationsAsync(
    List<string> userIds,
    int numResults = 10)
{
    var tasks = userIds.Select(userId =>
        GetPersonalizedRecommendationsAsync(userId, numResults)
    );

    var results = await Task.WhenAll(tasks);

    return userIds.Zip(results, (userId, recommendations) => new { userId, recommendations })
        .ToDictionary(x => x.userId, x => x.recommendations);
}
```

### 3. Fallback Mechanisms

**Fallback Chain**:
1. AWS Personalize (primary)
2. Redis cache
3. Rule-based recommendations (category, brand)
4. Popular/trending products

---

## Monitoring & Analytics

### 1. Key Metrics

**CloudWatch Metrics**:
```csharp
// Record recommendation performance
_metrics.RecordMetric("Recommendations.ApiLatency", stopwatch.ElapsedMilliseconds);
_metrics.RecordMetric("Recommendations.CacheHitRate", cacheHits / totalRequests);
_metrics.RecordMetric("Recommendations.PersonalizeApiCalls", 1);
```

**Business Metrics**:
- Recommendation click-through rate (CTR)
- Conversion rate from recommendations
- Average order value (AOV) increase
- Revenue attributed to recommendations

### 2. A/B Testing

```csharp
public async Task<List<ProductRecommendation>> GetRecommendationsWithExperiment(
    string userId,
    int numResults = 10)
{
    var experimentVariant = _experimentService.GetVariant(userId, "recommendation-algorithm");

    return experimentVariant switch
    {
        "personalize" => await GetPersonalizedRecommendationsAsync(userId, numResults),
        "rule-based" => await GetRuleBasedRecommendationsAsync(userId, numResults),
        "popular" => await GetPopularProductsAsync(numResults),
        _ => await GetPersonalizedRecommendationsAsync(userId, numResults)
    };
}
```

---

## Cost Optimization

### 1. Campaign TPS (Transactions Per Second)

Start with minimum TPS (1) and scale based on traffic:
- 1 TPS = ~2.6M requests/month = $100/month
- Monitor p99 latency; if > 500ms, increase TPS

### 2. Batch Inference

For email campaigns, use batch inference instead of real-time:
```bash
awslocal personalize create-batch-inference-job \
  --job-name daily-recommendations \
  --solution-version-arn ... \
  --input dataLocation=s3://gearify-ml-data/batch-input.json \
  --output dataLocation=s3://gearify-ml-data/batch-output/
```

**Cost**: $0.40 per 1,000 recommendations (vs. real-time $4-10/month base cost)

---

## Deployment Checklist

- [ ] Export historical data (interactions, items, users)
- [ ] Upload data to S3
- [ ] Create dataset group in AWS Personalize
- [ ] Import datasets
- [ ] Create and train solutions (user-personalization, similar-items)
- [ ] Create campaigns with min TPS=1
- [ ] Configure .NET service with campaign ARNs
- [ ] Set up Redis caching
- [ ] Deploy API endpoints
- [ ] Integrate frontend components
- [ ] Set up CloudWatch monitoring
- [ ] Configure A/B testing framework
- [ ] Enable real-time event tracking
- [ ] Set up weekly model retraining

**Estimated Setup Time**: 1-2 weeks

---

**Next**: See [Smart Search](./smart-search.md) for NLP-powered search implementation.
