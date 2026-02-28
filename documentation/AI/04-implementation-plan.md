# AI Features — Implementation Plan

Concrete, file-level implementation plan that maps the AI feature roadmap to the existing Gearify codebase. Each feature section specifies exactly which files to create, which to modify, what packages to add, and how the pieces connect.

---

## Existing Infrastructure (Ready to Use)

| Component | Location | Status |
|---|---|---|
| Redis (caching/idempotency) | SharedKernel + services | StackExchange.Redis v2.7-2.9 |
| AWS SDK (SNS/SQS/SES/S3/DynamoDB) | All services | AWSSDK packages installed |
| Elasticsearch (NEST v7.13.2) | `gearify-search-svc` | Operational |
| OpenTelemetry | All services | Distributed tracing active |
| Serilog | All services | Structured logging with correlation IDs |
| Multi-tenancy | SharedKernel middleware | TenantContext + middleware |
| Correlation ID tracking | SharedKernel + API Gateway | CorrelationContext (AsyncLocal) |
| Outbox pattern | SharedKernel (Order + Payment) | OutboxPublisher + OutboxMessageFactory |
| YARP API Gateway | `gearify-api-gateway` | 14 routes, JWT auth, rate limiting |
| MediatR (CQRS) | Catalog, Order, Payment services | Commands/Queries pattern |
| Polly (circuit breaker) | SharedKernel | Added with AI infrastructure |

---

## Implementation Progress

| # | Feature | Phase | Status | Service |
|---|---|---|---|---|
| 0 | Shared AI Infrastructure | Foundation | **Done** | `gearify-shared-kernel` |
| 1.1 | Product Recommendations | Phase 1 | **Done** | `gearify-catalog-svc` |
| 0.2 | User Interaction Event Tracking | Phase 0 | **Done** | `gearify-api-gateway` + SharedKernel |
| 1.2 | Smart Autocomplete | Phase 1 | Already existed (search svc) | `gearify-search-svc` |
| 1.3 | Cart Abandonment Prevention | Phase 1 | Pending | `gearify-notification-svc` |
| 2.1 | AI Product Descriptions (Bedrock) | Phase 2 | Pending | `gearify-catalog-svc` |
| 2.2 | NLP Smart Search (Comprehend) | Phase 2 | Pending | `gearify-catalog-svc` |
| 2.3 | Intelligent Chatbot (Bedrock) | Phase 2 | Pending | `gearify-notification-svc` |
| 2.4 | Review Summarization (Bedrock) | Phase 2 | Pending | `gearify-catalog-svc` |
| 2.5 | Fraud Detection | Phase 2 | Pending | `gearify-order-svc` |
| 3.1 | Demand Forecasting | Phase 3 | Pending | `gearify-catalog-svc` |
| 3.2 | Visual Search (Rekognition) | Phase 3 | Pending | `gearify-media-svc` |
| 3.3 | Dynamic Pricing (ML.NET) | Phase 3 | Pending | `gearify-catalog-svc` |
| 4.1 | Customer Behavior Analytics | Phase 4 | Pending | New worker service |
| 4.2 | Churn Prediction | Phase 4 | Pending | New worker service |
| 4.3 | Customer Lifetime Value | Phase 4 | Pending | New worker service |
| 4.4 | Sentiment Analysis | Phase 4 | Pending | `gearify-catalog-svc` |

---

## What Was Built: Shared AI Infrastructure

All AI features share a common infrastructure layer in `gearify-shared-kernel/AI/`. This was built as the foundation before any individual feature.

### Files

```
gearify-shared-kernel/AI/
├── IAIService.cs                          # Base interface (ServiceName, IsHealthyAsync)
├── AIServiceConfiguration.cs              # Config: regions, ARNs, cache TTLs, circuit breaker
├── AIServiceExtensions.cs                 # services.AddAIInfrastructure(config)
├── Resilience/
│   └── AICircuitBreakerPolicy.cs          # Polly circuit breaker, per-service pipelines
├── Caching/
│   ├── IAICacheService.cs                 # GetAsync, SetAsync, GetOrSetAsync
│   └── RedisCacheService.cs              # Redis impl with "ai:" key prefix
└── Monitoring/
    └── BedrockCostTracker.cs              # Token usage + USD cost per model
```

### How Services Use It

```csharp
// In any service's Startup.cs:
services.AddAIInfrastructure(Configuration);

// Inject in your service:
public MyAIService(IAICacheService cache, AICircuitBreakerPolicy circuitBreaker, ...)
```

### Packages Added to SharedKernel

- `Polly 8.4.2` — Circuit breaker resilience
- `AWSSDK.DynamoDBv2 3.7.0` — DynamoDB client for event storage

---

## What Was Built: Feature 1.1 — Product Recommendations

See [product-recommendations-implementation.md](./features/product-recommendations-implementation.md) for full details.

### Summary

- **4 API endpoints** on `gearify-catalog-svc` routed through YARP at `/api/recommendations/*`
- **AWS Personalize** integration with 3-tier fallback: Personalize → Category-based → Popular
- **Cricket-specific complementary rules** (Bats → Pads+Gloves+Helmets, etc.)
- **Redis caching** (1h personalized, 24h similar items)
- **Circuit breaker** wrapping all Personalize API calls
- **Data export pipeline** for Personalize training (CSV → S3)
- Works without Personalize configured — fallbacks provide useful results immediately

---

## What Was Built: Phase 0 — User Interaction Event Tracking

See [user-interaction-event-tracking.md](./features/user-interaction-event-tracking.md) for full details.

### Summary

- **EventTrackingMiddleware** in the API Gateway detects user interactions from HTTP request/response patterns
- **SQS publisher** sends events asynchronously (fire-and-forget, never blocks user responses)
- **Background processor** long-polls SQS and persists events to DynamoDB with 90-day TTL
- **4 event types:** View, AddToCart, Purchase, Search
- **Multi-tenant** — events tagged with TenantId from request headers
- Works without AWS configured — graceful degradation when queue URL is not set

---

## Pending: Feature 1.3 — Cart Abandonment Prevention

**Goal:** Detect abandoned carts and send recovery emails with discount incentives.

### Packages to Add

| Package | Version | Service |
|---|---|---|
| `Hangfire.Core` | 1.8.x | `gearify-notification-svc` |
| `Hangfire.AspNetCore` | 1.8.x | `gearify-notification-svc` |
| `Hangfire.InMemory` | 1.0.x | `gearify-notification-svc` (dev) |

### What to Create

| File | Purpose |
|---|---|
| `gearify-notification-svc/BackgroundJobs/CartAbandonmentJob.cs` | Hangfire RecurringJob (every 30 min): query abandoned carts → calculate discount → send email → mark notified |
| `gearify-notification-svc/Infrastructure/EmailTemplates/CartAbandonment.html` | Recovery email: cart items, discount badge, CTA button |

### What to Modify

| File | Change |
|---|---|
| `gearify-notification-svc/Gearify.NotificationService.csproj` | Add Hangfire packages |
| `gearify-notification-svc/Startup.cs` | Register Hangfire with InMemory storage, schedule recurring job |
| `gearify-cart-svc/` | Ensure cart entity has `LastActivityAt` and `IsAbandoned` flag (may need migration) |

### Recovery Logic

| Cart Value | Strategy |
|---|---|
| > 10,000 INR | 10% discount offer |
| > 3,000 INR | Free shipping |
| Any | Stock scarcity, EMI reminder |

---

## Pending: Feature 2.1 — AI Product Description Generator (Bedrock)

**Goal:** Generate SEO-optimized, cricket-specific product descriptions using Claude via Amazon Bedrock.

### Packages to Add

| Package | Version | Service |
|---|---|---|
| `AWSSDK.BedrockRuntime` | 3.7.x | `gearify-catalog-svc` |

### What to Create

| File | Purpose |
|---|---|
| `gearify-catalog-svc/Application/Services/IProductDescriptionService.cs` | Interface: `GenerateDescriptionAsync`, `GenerateBulletPointsAsync`, `GenerateSEOMetaDescriptionAsync` |
| `gearify-catalog-svc/Application/Services/ProductDescriptionService.cs` | Bedrock Claude 3.5 Sonnet calls, cricket-specific prompts, structured output parsing, Redis cache (7d) |
| `gearify-catalog-svc/Application/DTOs/GeneratedDescription.cs` | LongDescription, ShortDescription, BulletPoints, SEOKeywords |
| `gearify-catalog-svc/API/Controllers/ProductDescriptionController.cs` | `POST .../generate`, `POST .../apply`, `GET .../preview` |

### What to Modify

| File | Change |
|---|---|
| `gearify-catalog-svc/Gearify.CatalogService.csproj` | Add `AWSSDK.BedrockRuntime` |
| `gearify-catalog-svc/Startup.cs` | Register `IAmazonBedrockRuntime`, `IProductDescriptionService` |
| `gearify-catalog-svc/appsettings.json` | Add `BedrockModelId` to AI section |
| `gearify-api-gateway/appsettings.json` | Routes already covered by `/api/catalog/*` |

### Prompt Strategy

- System prompt with Gearify domain knowledge (cricket equipment, Indian audience, IPL context)
- Structured output markers: `[LONG_DESCRIPTION]`, `[SHORT_DESCRIPTION]`, `[BULLET_POINTS]`, `[SEO_KEYWORDS]`
- Model: `anthropic.claude-3-5-sonnet-20240620-v1:0` ($0.003/$0.015 per 1K tokens)
- Cost tracking via `BedrockCostTracker`

---

## Pending: Feature 2.2 — Natural Language Search (Comprehend)

**Goal:** Understand search queries using NLP — extract brands, categories, price ranges, and buying intent.

### Packages to Add

| Package | Version | Service |
|---|---|---|
| `AWSSDK.Comprehend` | 3.7.x | `gearify-catalog-svc` |

### What to Create

| File | Purpose |
|---|---|
| `gearify-catalog-svc/Application/Services/QueryUnderstandingService.cs` | Entity extraction (brands, categories, prices via Comprehend + regex), sentiment detection, key phrase extraction, cricket-specific filter mapping |
| `gearify-catalog-svc/Application/DTOs/SearchIntent.cs` | OriginalQuery, Entities, KeyPhrases, Sentiment, Filters |
| `gearify-catalog-svc/Application/DTOs/SearchFilters.cs` | Category, Brands[], MinPrice, MaxPrice |

### What to Modify

| File | Change |
|---|---|
| `gearify-catalog-svc/Gearify.CatalogService.csproj` | Add `AWSSDK.Comprehend` |
| `gearify-catalog-svc/Startup.cs` | Register `IAmazonComprehend`, `QueryUnderstandingService` |
| `gearify-search-svc/API/Controllers/SearchController.cs` | Add `GET /api/search/smart?q={query}` endpoint that calls QueryUnderstanding → builds Elasticsearch query |

### Cricket-Specific Mappings

- **Known brands:** SS, MRF, SG, Kookaburra, DSC, Gray-Nicolls, Puma
- **Category keywords:** bat → Cricket Bats, ball → Cricket Balls, shoe/boot → Cricket Shoes, helmet → Cricket Helmets
- **Price extraction:** regex for "under 15000", "below 5000", "less than 10k", "5000-10000"

### Example

```
Input:  "best SS english willow bat under 15000"
Output: { Category: "Cricket Bats", Brands: ["SS"], MaxPrice: 15000, Sentiment: "POSITIVE" }
```

---

## Pending: Feature 2.3 — Intelligent Chatbot (Bedrock)

**Goal:** AI shopping assistant using Claude via Bedrock with product/order context.

### Packages to Add

| Package | Version | Service |
|---|---|---|
| `AWSSDK.BedrockRuntime` | 3.7.x | `gearify-notification-svc` |
| `StackExchange.Redis` | 2.8.x | `gearify-notification-svc` |

### What to Create

| File | Purpose |
|---|---|
| `gearify-notification-svc/Application/Services/IChatbotService.cs` | `HandleMessageAsync(userId, message, sessionId)`, `GetConversationHistoryAsync(sessionId)` |
| `gearify-notification-svc/Application/Services/BedrockChatbotService.cs` | Claude 3.5 Sonnet calls, context gathering (products, orders, user prefs), conversation history in Redis (24h TTL, last 20 msgs), human escalation detection, token tracking |
| `gearify-notification-svc/Application/DTOs/ChatbotResponse.cs` | Message, SessionId, NeedsHumanEscalation, TokensUsed |
| `gearify-notification-svc/Application/DTOs/ChatMessage.cs` | Role (user/assistant), Content, Timestamp |
| `gearify-notification-svc/Application/DTOs/ChatContext.cs` | RelevantProducts, RecentOrders, UserPreferences |
| `gearify-notification-svc/API/Controllers/ChatbotController.cs` | `POST /api/chatbot/message`, `GET /api/chatbot/history/{sessionId}` |

### What to Modify

| File | Change |
|---|---|
| `gearify-notification-svc/Gearify.NotificationService.csproj` | Add `AWSSDK.BedrockRuntime`, `StackExchange.Redis` |
| `gearify-notification-svc/Startup.cs` | Register Bedrock client, Redis, `IChatbotService` |
| `gearify-api-gateway/appsettings.json` | Add `/api/chatbot/*` route to notification-cluster |

### System Prompt Design

```
You are Gearify's cricket equipment shopping assistant.
You help customers find the right cricket gear for their needs.

Context:
- Indian cricket equipment market
- Price ranges in INR
- Know cricket terminology (willow types, weight, sizes)
- Can recommend products, check orders, explain size charts
- If you cannot help, say "Let me connect you with our team" (triggers escalation)
```

### Conversation Storage

- Redis key: `ai:chat:{sessionId}` → JSON array of ChatMessage
- TTL: 24 hours
- Max history: 20 messages (sliding window)

---

## Pending: Feature 2.4 — Review Summarization (Bedrock)

**Goal:** Summarize product reviews into pros/cons/themes using Claude 3 Haiku (cheaper model).

### What to Create

| File | Purpose |
|---|---|
| `gearify-catalog-svc/Application/Services/IReviewAnalysisService.cs` | `SummarizeReviewsAsync(productId)`, `ExtractKeyThemesAsync(reviews)` |
| `gearify-catalog-svc/Application/Services/ReviewAnalysisService.cs` | Claude 3 Haiku calls, structured output (summary, pros, cons, themes, buying recommendation), Redis cache (24h) |
| `gearify-catalog-svc/Application/DTOs/ReviewSummary.cs` | ProductId, Summary, Pros[], Cons[], KeyThemes[], BuyingRecommendation, TotalReviews, AverageRating |

### Notes

- Uses `BedrockHaikuModelId` from AI config (`anthropic.claude-3-haiku-20240307-v1:0`)
- Much cheaper than Sonnet: $0.00025/$0.00125 per 1K tokens
- Cache key: `ai:review-summary:{tenantId}:{productId}` with 24h TTL
- Requires existing review data (assumes reviews are stored in product or separate collection)

---

## Pending: Feature 2.5 — Fraud Detection (AWS Fraud Detector)

**Goal:** Risk-score orders before payment confirmation. Block/review high-risk orders.

### Packages to Add

| Package | Version | Service |
|---|---|---|
| `AWSSDK.FraudDetector` | 3.7.x | `gearify-order-svc` |

### What to Create

| File | Purpose |
|---|---|
| `gearify-order-svc/Application/Services/FraudDetectionService.cs` | `AssessOrderAsync(order, user)` → FraudAssessment. Risk scoring: Low (<300), Medium (300-700), High (>700). Signals: high-value first order, multiple failed payments, new account, intl shipping |
| `gearify-order-svc/Application/DTOs/FraudAssessment.cs` | OrderId, RiskScore, RiskLevel, Outcome, RecommendedAction, FraudSignals[] |

### What to Modify

| File | Change |
|---|---|
| `gearify-order-svc/Gearify.OrderService.csproj` | Add `AWSSDK.FraudDetector` |
| `gearify-order-svc/Startup.cs` | Register `IAmazonFraudDetector`, `FraudDetectionService` |
| Order creation command handler | Add fraud check before confirmation. Medium → PendingReview + notify. High → Decline |

### Action Matrix

| Risk Score | Risk Level | Action |
|---|---|---|
| 0-299 | Low | Approve automatically |
| 300-699 | Medium | PendingReview status + notify fraud team |
| 700-1000 | High | Decline with user-friendly message |

---

## Pending: Phase 3 — Optimization Features

### 3.1 Demand Forecasting (AWS Forecast)

| Item | Detail |
|---|---|
| **Package** | `AWSSDK.ForecastService` in `gearify-catalog-svc` |
| **Create** | `Application/Services/DemandForecastService.cs` — 30-day demand prediction per product |
| **Create** | `Application/DTOs/InventoryRecommendation.cs` — Restock alerts when stock < 50% of forecast |
| **Pattern** | Batch processing: Hangfire job runs daily at 2 AM |

### 3.2 Visual Search (AWS Rekognition)

| Item | Detail |
|---|---|
| **Package** | `AWSSDK.Rekognition` in `gearify-media-svc` |
| **Create** | `Application/Services/VisualSearchService.cs` — Upload photo → detect equipment type → find similar |
| **Endpoint** | `POST /api/search/visual` — accepts image upload, returns matching products |
| **Pattern** | Synchronous AI enrichment (user waits for result) |

### 3.3 Dynamic Pricing (ML.NET)

| Item | Detail |
|---|---|
| **Packages** | `Microsoft.ML`, `Microsoft.ML.Recommender` in `gearify-catalog-svc` |
| **Create** | `Application/Services/DynamicPricingService.cs` — ML.NET regression model |
| **Rules** | Price floor 70%, ceiling 120% of base price |
| **Pattern** | Batch processing: retrain weekly, predict daily |

---

## Pending: Phase 4 — Advanced Analytics

These features are less concrete and may evolve based on data availability:

| Feature | Approach | Technology |
|---|---|---|
| Customer Behavior Analytics | Shopping pattern analysis, cart abandonment prediction, journey mapping | AWS QuickSight + DynamoDB Streams |
| Churn Prediction | Classify customers likely to stop purchasing (no purchase 180d, reduced engagement) | ML.NET classification |
| Customer Lifetime Value | RFM analysis, segment into Champions/Potential Loyalists/At Risk/Lost | ML.NET regression |
| Sentiment Analysis | AWS Comprehend on review submission, alert on negative spikes | `AWSSDK.Comprehend` |

---

## Architecture Patterns

Every AI feature follows one of these patterns:

### Pattern 1: Event-Driven AI (async)
```
User Action → API Gateway → Service → SQS Queue → Worker → AI Service → DynamoDB
```
**Used by:** Event tracking, image processing, sentiment analysis

### Pattern 2: Synchronous AI Enrichment
```
API Request → Service → AI Service (sync) → Enriched Response
```
**Used by:** Fraud detection at checkout, NLP search, chatbot

### Pattern 3: Cached AI Predictions
```
Request → Redis Cache → (miss) → AI Service → Store in Redis → Return
```
**Used by:** Recommendations (1h), similar items (24h), descriptions (7d), review summaries (24h)

### Pattern 4: Circuit Breaker + Fallback
```
Request → Circuit Breaker → AI Service
                          → (open) → Fallback (rule-based / popular products)
```
**Used by:** All AI service calls. Polly config: 5 failures → 1 min break.

### Pattern 5: Batch Processing
```
Scheduled Job (Hangfire) → Batch Processor → AI Service → Store Results
```
**Used by:** Demand forecasting (daily 2AM), customer segmentation (weekly), data exports

---

## Recommended Implementation Order

| Order | Feature | Dependencies | Effort |
|---|---|---|---|
| **Done** | Shared AI Infrastructure | None | -- |
| **Done** | Feature 1.1: Product Recommendations | SharedKernel AI | -- |
| **Done** | Phase 0: Event Tracking | SharedKernel AI | -- |
| 2 | Feature 1.3: Cart Abandonment Prevention | Notification svc | 2-3 days |
| 3 | Feature 2.1: AI Product Descriptions | Bedrock SDK | 2-3 days |
| 4 | Feature 2.2: NLP Smart Search | Comprehend SDK + Search svc | 2-3 days |
| 5 | Feature 2.3: Chatbot | Bedrock SDK + Notification svc | 3-4 days |
| 6 | Feature 2.4: Review Summarization | Bedrock setup from 2.1 | 1-2 days |
| 7 | Feature 2.5: Fraud Detection | Order svc | 2-3 days |
| 8+ | Phase 3-4 features | Phases 1-2 complete | Varies |

---

## Cost Estimates (Monthly, ~10K users)

| Phase | Services | Estimated |
|---|---|---|
| Phase 1 | Personalize ($100-150), Elasticsearch ($50), Redis (existing), SES ($0.10/1K emails) | ~$200/month |
| Phase 2 | Bedrock ($200), Comprehend ($50), Fraud Detector ($75) | ~$325/month |
| Phase 3 | Forecast ($50), Rekognition ($50), ML.NET (free) | ~$100/month |
| Phase 4 | QuickSight, ML.NET, Comprehend | ~$75/month |
| **Total** | | **~$700/month** |

**Development cost: $0** — all features work with LocalStack + mock/fallback implementations.

---

## Testing Strategy

### Unit Tests (no Docker, no AWS)

- Mock AWS services with NSubstitute (`IAmazonPersonalizeRuntime`, `IAmazonBedrockRuntime`, etc.)
- Test fallback chains (Personalize down → category-based → popular)
- Test circuit breaker behavior (open/closed/half-open)
- Test cache hit/miss paths
- Test prompt construction for Bedrock calls
- Test fraud signal detection rules

### Integration Tests (LocalStack)

- SQS event publishing and consumption
- DynamoDB read/write for event storage
- S3 data export pipeline
- Redis cache operations

### For Services Without LocalStack Support

```csharp
// Development — use mock implementations
if (builder.Environment.IsDevelopment())
    services.AddScoped<IForecastService, MockForecastService>();
else
    services.AddScoped<IForecastService, AwsForecastService>();
```

---

## Key Files Reference

### Existing (to modify per feature)

| File | Notes |
|---|---|
| `gearify-catalog-svc/Gearify.CatalogService.csproj` | Add AWS SDK packages per feature |
| `gearify-catalog-svc/Startup.cs` | Register AI services |
| `gearify-catalog-svc/Domain/Entities/Product.cs` | Rich product model (already complete) |
| `gearify-search-svc/` | Existing Elasticsearch with NEST |
| `gearify-notification-svc/Startup.cs` | Register chatbot + Hangfire |
| `gearify-order-svc/Startup.cs` | Register fraud detection |
| `gearify-api-gateway/appsettings.json` | YARP routes for new endpoints |
| `gearify-shared-kernel/AI/AIServiceConfiguration.cs` | Add config properties per feature |

### Created (done)

| File | Feature |
|---|---|
| `gearify-shared-kernel/AI/*` (7 files) | Shared AI infrastructure |
| `gearify-catalog-svc/Application/Services/RecommendationService.cs` | Product Recommendations |
| `gearify-catalog-svc/API/Controllers/RecommendationsController.cs` | Recommendations API |
| `gearify-catalog-svc/Infrastructure/ML/PersonalizeDataExporter.cs` | Training data export |
| `gearify-shared-kernel/AI/Events/*` (4 files) | Event Tracking |
| `gearify-api-gateway/Middleware/EventTrackingMiddleware.cs` | Event Tracking |

### To Create (pending features)

| File | Feature |
|---|---|
| `gearify-notification-svc/BackgroundJobs/CartAbandonmentJob.cs` | Cart Abandonment |
| `gearify-catalog-svc/Application/Services/ProductDescriptionService.cs` | AI Descriptions |
| `gearify-catalog-svc/Application/Services/QueryUnderstandingService.cs` | NLP Search |
| `gearify-notification-svc/Application/Services/BedrockChatbotService.cs` | Chatbot |
| `gearify-catalog-svc/Application/Services/ReviewAnalysisService.cs` | Review Summaries |
| `gearify-order-svc/Application/Services/FraudDetectionService.cs` | Fraud Detection |
| `gearify-catalog-svc/Application/Services/DemandForecastService.cs` | Demand Forecasting |
| `gearify-media-svc/Application/Services/VisualSearchService.cs` | Visual Search |
| `gearify-catalog-svc/Application/Services/DynamicPricingService.cs` | Dynamic Pricing |
