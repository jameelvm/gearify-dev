# Product Recommendations — Implementation Guide

## Feature Summary

| Property | Value |
|---|---|
| **Feature** | Product Recommendations (AWS Personalize) |
| **Priority** | P0 |
| **Phase** | Phase 1 — Foundation |
| **Service** | `gearify-catalog-svc` |
| **Status** | Implemented |

Personalized product recommendations powered by AWS Personalize, with a multi-tier fallback chain that gracefully degrades when the ML service is unavailable or not yet trained.

---

## Architecture

```
Client Request
    │
    ▼
API Gateway (YARP)  ─── /api/recommendations/* ──►  Catalog Service
                                                          │
                                              ┌───────────┼───────────┐
                                              ▼           ▼           ▼
                                         Redis Cache  Personalize  Fallback
                                         (hit? return)  (ML API)   (rules)
                                              │           │           │
                                              └───────────┼───────────┘
                                                          ▼
                                                   Enriched Response
                                                   (product details)
```

### Fallback Chain (in order)

1. **Redis Cache** — Cached results returned immediately
2. **AWS Personalize** — ML-powered recommendations via campaign ARN
3. **Category-based** — Products from the same category (for similar/complementary)
4. **Popular/Featured** — BestSeller and Featured products (for personalized)

If Personalize is not configured (empty ARN), the service skips directly to fallback — no errors, no delays.

### Circuit Breaker (Polly)

All Personalize API calls are wrapped in a circuit breaker:

- **Failure ratio:** 50% over sampling window
- **Minimum throughput:** 5 calls before circuit can open
- **Sampling duration:** 30 seconds
- **Break duration:** 1 minute (circuit stays open, all calls skip to fallback)

Configured via `appsettings.json` → `AI:CircuitBreaker`.

---

## Files Created

### SharedKernel — AI Infrastructure (shared by all future AI features)

| File | Purpose |
|---|---|
| `gearify-shared-kernel/AI/IAIService.cs` | Base interface with health check contract |
| `gearify-shared-kernel/AI/AIServiceConfiguration.cs` | Centralized AI config (regions, ARNs, cache TTLs, circuit breaker) |
| `gearify-shared-kernel/AI/AIServiceExtensions.cs` | `services.AddAIInfrastructure(config)` DI extension |
| `gearify-shared-kernel/AI/Resilience/AICircuitBreakerPolicy.cs` | Polly circuit breaker with per-service pipelines |
| `gearify-shared-kernel/AI/Caching/IAICacheService.cs` | Cache abstraction with `GetOrSetAsync` |
| `gearify-shared-kernel/AI/Caching/RedisCacheService.cs` | Redis implementation with `ai:` key prefix |
| `gearify-shared-kernel/AI/Monitoring/BedrockCostTracker.cs` | Token usage + cost tracking (for future Bedrock features) |

### Catalog Service — Recommendation Feature

| File | Purpose |
|---|---|
| `Application/Services/IRecommendationService.cs` | Service interface — 5 methods |
| `Application/Services/RecommendationService.cs` | Full implementation with Personalize + fallback chain |
| `Application/DTOs/ProductRecommendation.cs` | `ProductRecommendation` DTO + `RecommendationResponse` record |
| `API/Controllers/RecommendationsController.cs` | REST controller — 4 endpoints |
| `Infrastructure/ML/PersonalizeDataExporter.cs` | CSV export pipeline for Personalize training data |

### Files Modified

| File | Change |
|---|---|
| `gearify-shared-kernel/Gearify.SharedKernel.csproj` | Added `Polly 8.4.2`, `AWSSDK.DynamoDBv2` |
| `gearify-catalog-svc/Gearify.CatalogService.csproj` | Added `AWSSDK.Personalize*`, `CsvHelper` |
| `gearify-catalog-svc/Startup.cs` | Registered AI infrastructure, Personalize clients, `IRecommendationService` |
| `gearify-catalog-svc/appsettings.json` | Added `AI` configuration section |
| `gearify-api-gateway/appsettings.json` | Added `recommendations-route` YARP route |

---

## API Endpoints

### `GET /api/recommendations/for-you`

Personalized recommendations for the authenticated user.

**Headers:**
- `X-User-Id` (required) — User identifier
- `X-Tenant-Id` (required) — Tenant context

**Query Parameters:**
- `limit` (optional, default 10) — Number of results

**Response:**
```json
{
  "items": [
    {
      "productId": "abc-123",
      "name": "SS Ton English Willow Bat",
      "price": 14999.00,
      "thumbnailUrl": "https://...",
      "category": "Cricket Bats",
      "brand": "SS",
      "score": 0.95,
      "recommendationReason": "Recommended for you"
    }
  ],
  "source": "personalize",
  "totalCount": 10
}
```

**Source values:** `"personalize"`, `"cache"`, `"popular-fallback"`

---

### `GET /api/recommendations/products/{productId}/similar`

Products similar to the given product (same category, same brand, etc.).

**Response source values:** `"personalize"`, `"cache"`, `"category-fallback"`

---

### `GET /api/recommendations/products/{productId}/complementary`

Products that complement the given product. Uses cricket-specific rules:

| Product Category | Complementary Categories |
|---|---|
| Cricket Bats | Batting Pads, Batting Gloves, Cricket Helmets, Bat Grips, Bat Covers |
| Batting Pads | Cricket Bats, Batting Gloves, Cricket Helmets, Thigh Guards |
| Batting Gloves | Cricket Bats, Batting Pads, Inner Gloves |
| Cricket Helmets | Cricket Bats, Batting Pads, Batting Gloves |
| Cricket Balls | Cricket Bats, Wicket Keeping Gloves, Bowling Shoes |
| Cricket Shoes | Cricket Socks, Cricket Whites, Cricket Bags |
| Cricket Bags | Cricket Bats, Cricket Shoes, Cricket Whites |

**Response source values:** `"complementary-rules"`, `"cache"`, `"category-fallback"`

---

### `POST /api/recommendations/interactions`

Record a user interaction for Personalize model training.

**Request body:**
```json
{
  "productId": "abc-123",
  "eventType": "View",
  "eventValue": null
}
```

---

## Configuration

### `appsettings.json` — AI Section

```json
{
  "AI": {
    "Region": "us-east-1",
    "UseLocalStack": true,
    "LocalStackEndpoint": "http://localhost:4566",
    "PersonalizeCampaignArn": "",
    "PersonalizeSimilarItemsCampaignArn": "",
    "PersonalizeEventTrackerArn": "",
    "DataExportBucket": "gearify-ml-data",
    "Cache": {
      "UserRecommendations": "01:00:00",
      "SimilarItems": "1.00:00:00"
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "BreakDuration": "00:01:00",
      "SamplingDuration": "00:00:30"
    }
  }
}
```

**Key points:**
- Leave `PersonalizeCampaignArn` empty during development — fallback logic activates automatically
- Set cache TTLs to `"00:00:00"` to disable caching during development
- Circuit breaker applies per-pipeline (personalize, personalize-similar, personalize-rerank)

### Environment Variables (Docker/Production)

| Variable | Purpose |
|---|---|
| `PERSONALIZERUNTIME_ENDPOINT` | Override Personalize endpoint (LocalStack: `http://localstack:4566`) |
| `AWS_ACCESS_KEY_ID` | AWS credentials |
| `AWS_SECRET_ACCESS_KEY` | AWS credentials |

---

## Data Export Pipeline

`PersonalizeDataExporter` exports product catalog data as CSV to S3, formatted for AWS Personalize dataset import.

**Items dataset columns:**
`ITEM_ID, CATEGORY, SUBCATEGORY, BRAND, PRICE, DEPARTMENT, CREATION_TIMESTAMP, IS_DEAL, IS_BEST_SELLER, RATING_AVERAGE`

**S3 path:** `s3://gearify-ml-data/personalize/{tenantId}/items/{date}/items.csv`

To trigger an export (e.g., from a Hangfire job or admin endpoint), inject `PersonalizeDataExporter` and call:
```csharp
await exporter.ExportItemsDatasetAsync(tenantId);
```

---

## Caching Strategy

| Data Type | Cache Key Pattern | TTL | Rationale |
|---|---|---|---|
| Personalized recs | `ai:reco:personal:{tenant}:{userId}:{count}` | 1 hour | User preferences shift over a session |
| Similar items | `ai:reco:similar:{tenant}:{itemId}:{count}` | 24 hours | Item similarity is stable |
| Complementary items | `ai:reco:complementary:{tenant}:{itemId}:{count}` | 24 hours | Rule-based, rarely changes |

All caching uses the shared `IAICacheService` (Redis, `ai:` key prefix). Cache misses trigger the full pipeline; hits return immediately with `source: "cache"`.

---

## How It Works End-to-End

1. **User visits product page** → frontend calls `GET /api/recommendations/products/{id}/similar`
2. **API Gateway** routes `/api/recommendations/*` to Catalog Service via YARP
3. **RecommendationsController** extracts tenant from middleware, calls `IRecommendationService`
4. **RecommendationService** checks Redis cache → misses → calls Personalize (or falls back)
5. **Enrichment** — Raw Personalize item IDs are looked up in DynamoDB to get full product details
6. **Response** — Enriched `RecommendationResponse` with product names, prices, images, scores
7. **Cache write** — Result stored in Redis for next request

---

## Testing Approach

### Without AWS Personalize (Development)

The service works out of the box without Personalize. When `PersonalizeCampaignArn` is empty:
- Personalized → returns popular/featured products
- Similar → returns same-category products
- Complementary → uses cricket-specific rules table

### Unit Tests (suggested)

```csharp
// Test fallback when Personalize ARN is empty
[Fact]
public async Task GetPersonalized_WhenNoCampaignArn_ReturnsFallback()
{
    var config = Options.Create(new AIServiceConfiguration { PersonalizeCampaignArn = "" });
    var service = new RecommendationService(..., config);
    var result = await service.GetPersonalizedRecommendationsAsync("user1");
    Assert.Equal("popular-fallback", result.Source);
}

// Test cache hit path
[Fact]
public async Task GetSimilar_WhenCached_ReturnsCacheSource()
{
    await cache.SetAsync("reco:similar:tenant:item1:10", cachedResponse, TimeSpan.FromHours(1));
    var result = await service.GetSimilarItemsAsync("item1");
    Assert.Equal("cache", result.Source);
}

// Test complementary cricket rules
[Fact]
public async Task GetComplementary_ForCricketBat_ReturnsPadsGlovesHelmets()
{
    // Product is in "Cricket Bats" category
    var result = await service.GetComplementaryItemsAsync("bat-123");
    Assert.Equal("complementary-rules", result.Source);
    Assert.Contains(result.Items, i => i.Category == "Batting Pads");
}
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `AWSSDK.Personalize` | 3.7.0 | Personalize management API |
| `AWSSDK.PersonalizeRuntime` | 3.7.0 | Real-time recommendations |
| `AWSSDK.PersonalizeEvents` | 3.7.0 | Record user interactions |
| `CsvHelper` | 31.0.0 | Data export to CSV for Personalize training |
| `Polly` | 8.4.2 | Circuit breaker resilience (in SharedKernel) |

---

## What's Next

This feature provides the foundation for AI-powered recommendations. Future enhancements:
- **Phase 0: Event Tracking** — Capture user views/clicks/purchases to train the Personalize model
- **Personalize Model Training** — Once interaction data accumulates, create Personalize dataset group + solution + campaign
- **A/B Testing** — Compare Personalize recommendations vs. rule-based fallback conversion rates
