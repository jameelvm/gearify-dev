# AI Features Implementation Roadmap

Phased rollout plan for Gearify AI/ML features with timelines, dependencies, and success metrics.

## Implementation Strategy

### Principles
1. **Start Small, Scale Fast**: Begin with high-impact, low-complexity features
2. **Iterative Development**: Each phase builds on previous learnings
3. **Data-Driven Decisions**: Measure everything, optimize continuously
4. **Cost-Conscious**: Use AWS Free Tier and LocalStack during development
5. **User-Centric**: Prioritize features that directly improve customer experience

### Prerequisites (Phase 0)

Before implementing any AI features, ensure these foundations are in place:

#### Data Infrastructure
- ✅ **Event Tracking System**: Capture user interactions (views, clicks, cart additions, purchases)
- ✅ **DynamoDB Tables**: Products, Orders, Users, Reviews already exist
- ✅ **SQS/SNS**: Message queues for async processing
- ✅ **S3 Buckets**: Media storage for product images
- ⚠️ **Analytics Pipeline**: Need to add event streaming to track user behavior

#### Code Setup
```csharp
// Create shared AI infrastructure service
// File: Gearify.Shared/AI/IAIService.cs

namespace Gearify.Shared.AI
{
    public interface IAIService
    {
        Task<bool> IsHealthyAsync();
    }

    public class AIServiceConfiguration
    {
        public string AwsRegion { get; set; } = "us-east-1";
        public string LocalStackEndpoint { get; set; } = "http://localhost:4566";
        public bool UseLocalStack { get; set; } = true;
        public Dictionary<string, string> ServiceEndpoints { get; set; } = new();
    }
}
```

```csharp
// Add to Startup.cs in each service
public void ConfigureServices(IServiceCollection services)
{
    // AI Configuration
    var aiConfig = Configuration.GetSection("AI").Get<AIServiceConfiguration>();
    services.AddSingleton(aiConfig);

    // AWS SDK Configuration
    var awsOptions = new AWSOptions
    {
        Region = RegionEndpoint.GetBySystemName(aiConfig.AwsRegion)
    };

    if (aiConfig.UseLocalStack)
    {
        awsOptions.DefaultClientConfig.ServiceURL = aiConfig.LocalStackEndpoint;
    }

    services.AddDefaultAWSOptions(awsOptions);
}
```

#### Event Tracking Schema
```json
{
  "eventType": "product_view",
  "userId": "user-123",
  "tenantId": "default",
  "productId": "prod-bat-ss-001",
  "sessionId": "session-xyz",
  "timestamp": "2026-01-01T12:00:00Z",
  "metadata": {
    "referrer": "/category/bats",
    "deviceType": "mobile",
    "category": "Bats",
    "price": 12500
  }
}
```

**Action Items**:
1. Create event tracking middleware in API Gateway
2. Set up SQS queue: `gearify-user-events-queue`
3. Create DynamoDB table: `gearify-user-events` for storing interaction history
4. Implement event publishing in product/cart/checkout endpoints

---

## Phase 1: Foundation (Weeks 1-4)

**Goal**: Establish AI infrastructure and deploy first customer-facing features

### 1.1 Product Recommendations (Week 1-2)

**Priority**: P0 | **Impact**: High | **Effort**: Medium

#### Week 1: Setup & Data Preparation
- [ ] Install AWS SDK packages: `AWSSDK.Personalize`, `AWSSDK.PersonalizeRuntime`
- [ ] Create schema for interactions dataset (user-product interactions)
- [ ] Export historical data from DynamoDB (orders, views, cart additions)
- [ ] Format data for AWS Personalize (CSV format)
- [ ] Upload to S3: `s3://gearify-ml-data/personalize/interactions.csv`

**Data Format**:
```csv
USER_ID,ITEM_ID,TIMESTAMP,EVENT_TYPE,EVENT_VALUE
user-001,prod-bat-ss-001,1704110400,view,1
user-001,prod-bat-ss-001,1704110500,add_to_cart,1
user-001,prod-bat-ss-001,1704111000,purchase,12500
```

#### Week 2: AWS Personalize Setup
- [ ] Create dataset group: `gearify-recommendations`
- [ ] Create datasets: interactions, items (products), users
- [ ] Create solution using `aws-hrnn` recipe (Hierarchical RNN)
- [ ] Create campaign with 1 TPS provisioned capacity
- [ ] Test recommendations via API

**Implementation**:
```csharp
// File: Gearify.CatalogService/Application/Services/RecommendationService.cs

using Amazon.PersonalizeRuntime;
using Amazon.PersonalizeRuntime.Model;

public class RecommendationService : IRecommendationService
{
    private readonly IAmazonPersonalizeRuntime _personalize;
    private readonly string _campaignArn;

    public RecommendationService(
        IAmazonPersonalizeRuntime personalize,
        IConfiguration configuration)
    {
        _personalize = personalize;
        _campaignArn = configuration["AWS:Personalize:CampaignArn"];
    }

    public async Task<List<ProductRecommendation>> GetRecommendationsAsync(
        string userId,
        int numResults = 10)
    {
        var request = new GetRecommendationsRequest
        {
            CampaignArn = _campaignArn,
            UserId = userId,
            NumResults = numResults
        };

        var response = await _personalize.GetRecommendationsAsync(request);

        // Fetch product details from DynamoDB
        var productIds = response.ItemList.Select(i => i.ItemId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);

        return products.Select(p => new ProductRecommendation
        {
            ProductId = p.Id,
            Name = p.Name,
            Price = p.Price,
            ThumbnailUrl = p.ThumbnailUrl,
            Score = response.ItemList.First(i => i.ItemId == p.Id).Score
        }).ToList();
    }

    public async Task<List<ProductRecommendation>> GetSimilarItemsAsync(
        string itemId,
        int numResults = 10)
    {
        var request = new GetRecommendationsRequest
        {
            CampaignArn = _campaignArn,
            ItemId = itemId,
            NumResults = numResults
        };

        var response = await _personalize.GetRecommendationsAsync(request);

        var productIds = response.ItemList.Select(i => i.ItemId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);

        return products.Select(p => new ProductRecommendation
        {
            ProductId = p.Id,
            Name = p.Name,
            Price = p.Price,
            ThumbnailUrl = p.ThumbnailUrl
        }).ToList();
    }
}
```

**API Endpoints**:
```csharp
// Add to CatalogService API Controller

[HttpGet("recommendations/for-you")]
public async Task<ActionResult<List<ProductRecommendation>>> GetPersonalizedRecommendations(
    [FromHeader(Name = "X-User-Id")] string userId,
    [FromQuery] int limit = 10)
{
    var recommendations = await _recommendationService.GetRecommendationsAsync(userId, limit);
    return Ok(recommendations);
}

[HttpGet("products/{productId}/similar")]
public async Task<ActionResult<List<ProductRecommendation>>> GetSimilarProducts(
    string productId,
    [FromQuery] int limit = 10)
{
    var recommendations = await _recommendationService.GetSimilarItemsAsync(productId, limit);
    return Ok(recommendations);
}
```

**Frontend Integration** (Angular):
```typescript
// File: gearify-web/src/app/services/recommendations.service.ts

export interface ProductRecommendation {
  productId: string;
  name: string;
  price: number;
  thumbnailUrl: string;
  score?: number;
}

@Injectable({ providedIn: 'root' })
export class RecommendationsService {
  private apiUrl = environment.apiGatewayUrl;

  constructor(private http: HttpClient) {}

  getPersonalizedRecommendations(limit = 10): Observable<ProductRecommendation[]> {
    return this.http.get<ProductRecommendation[]>(
      `${this.apiUrl}/catalog/recommendations/for-you?limit=${limit}`
    );
  }

  getSimilarProducts(productId: string, limit = 10): Observable<ProductRecommendation[]> {
    return this.http.get<ProductRecommendation[]>(
      `${this.apiUrl}/catalog/products/${productId}/similar?limit=${limit}`
    );
  }
}
```

**Success Metrics**:
- Recommendation API response time < 500ms
- Click-through rate (CTR) on recommendations > 5%
- 10% increase in average order value within 2 weeks

---

### 1.2 Smart Autocomplete (Week 2-3)

**Priority**: P0 | **Impact**: Medium | **Effort**: Low

#### Implementation: Elasticsearch Suggester

**Setup**:
```bash
# LocalStack - Create Elasticsearch domain
awslocal es create-elasticsearch-domain \
  --domain-name gearify-search \
  --elasticsearch-version 7.10 \
  --elasticsearch-cluster-config InstanceType=t3.small.elasticsearch,InstanceCount=1
```

**Index Products with Suggestions**:
```csharp
// File: Gearify.CatalogService/Infrastructure/Search/ElasticsearchService.cs

using Nest;

public class ElasticsearchService
{
    private readonly IElasticClient _client;

    public async Task IndexProductAsync(Product product)
    {
        var document = new ProductDocument
        {
            Id = product.Id,
            Name = product.Name,
            NameSuggest = new CompletionField
            {
                Input = new[] { product.Name, product.Brand, product.Category }
            },
            Description = product.Description,
            Category = product.Category,
            Brand = product.Brand,
            Price = product.Price,
            Tags = product.Tags
        };

        await _client.IndexAsync(document, idx => idx.Index("products"));
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(
        string query,
        int size = 10)
    {
        var response = await _client.SearchAsync<ProductDocument>(s => s
            .Index("products")
            .Suggest(su => su
                .Completion("product-suggest", cs => cs
                    .Field(f => f.NameSuggest)
                    .Prefix(query)
                    .Fuzzy(f => f.Fuzziness(Fuzziness.Auto))
                    .Size(size)
                )
            )
        );

        return response.Suggest["product-suggest"]
            .SelectMany(s => s.Options)
            .Select(o => o.Text)
            .Distinct()
            .ToList();
    }
}
```

**API Endpoint**:
```csharp
[HttpGet("search/autocomplete")]
public async Task<ActionResult<List<string>>> Autocomplete(
    [FromQuery] string q,
    [FromQuery] int limit = 10)
{
    if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        return Ok(new List<string>());

    var suggestions = await _searchService.GetAutocompleteSuggestionsAsync(q, limit);
    return Ok(suggestions);
}
```

**Success Metrics**:
- Autocomplete response time < 100ms
- 30% of searches use autocomplete suggestions
- Typo tolerance working (e.g., "Kokaburra" → "Kookaburra")

---

### 1.3 Cart Abandonment Prevention (Week 3-4)

**Priority**: P0 | **Impact**: High | **Effort**: Low

#### Implementation: Background Job with Hangfire

**Install Packages**:
```bash
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.Redis.StackExchange
```

**Setup**:
```csharp
// File: Gearify.NotificationService/Startup.cs

public void ConfigureServices(IServiceCollection services)
{
    // Hangfire with Redis storage
    services.AddHangfire(config => config
        .UseRedisStorage(Configuration["Redis:ConnectionString"]));

    services.AddHangfireServer();
}

public void Configure(IApplicationBuilder app)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}
```

**Cart Abandonment Detection**:
```csharp
// File: Gearify.NotificationService/BackgroundJobs/CartAbandonmentJob.cs

public class CartAbandonmentJob
{
    private readonly ICartRepository _cartRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<CartAbandonmentJob> _logger;

    public CartAbandonmentJob(
        ICartRepository cartRepository,
        IEmailService emailService,
        ILogger<CartAbandonmentJob> logger)
    {
        _cartRepository = cartRepository;
        _emailService = emailService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAbandonedCartsAsync()
    {
        // Find carts abandoned 1 hour ago
        var abandonedCarts = await _cartRepository.GetAbandonedCartsAsync(
            abandonedSince: TimeSpan.FromHours(1),
            notNotifiedYet: true
        );

        foreach (var cart in abandonedCarts)
        {
            try
            {
                // Calculate cart value
                var totalValue = cart.Items.Sum(i => i.Price * i.Quantity);

                // Determine recovery strategy
                var discountOffer = totalValue > 10000 ? 10 : 0; // 10% off for carts > ₹10,000
                var freeShipping = totalValue > 3000;

                // Send recovery email
                await _emailService.SendCartAbandonmentEmailAsync(new CartAbandonmentEmail
                {
                    ToEmail = cart.UserEmail,
                    UserName = cart.UserName,
                    CartItems = cart.Items,
                    TotalValue = totalValue,
                    DiscountPercentage = discountOffer,
                    FreeShipping = freeShipping,
                    CartUrl = $"https://gearify.com/cart/{cart.Id}"
                });

                // Mark as notified
                await _cartRepository.MarkAsNotifiedAsync(cart.Id);

                _logger.LogInformation(
                    "Sent cart abandonment email to {Email} for cart {CartId} (₹{Value})",
                    cart.UserEmail, cart.Id, totalValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process abandoned cart {CartId}", cart.Id);
            }
        }
    }
}
```

**Schedule Job**:
```csharp
// Run every 30 minutes
RecurringJob.AddOrUpdate<CartAbandonmentJob>(
    "process-abandoned-carts",
    job => job.ProcessAbandonedCartsAsync(),
    "*/30 * * * *" // Cron: Every 30 minutes
);
```

**Email Template**:
```html
<!-- File: Gearify.NotificationService/Templates/CartAbandonment.html -->

<!DOCTYPE html>
<html>
<head>
    <style>
        .container { max-width: 600px; margin: 0 auto; font-family: Arial, sans-serif; }
        .header { background: #1976d2; color: white; padding: 20px; text-align: center; }
        .content { padding: 20px; }
        .cart-item { border-bottom: 1px solid #eee; padding: 15px 0; }
        .cta-button { background: #ff5722; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }
        .discount-badge { background: #4caf50; color: white; padding: 10px; border-radius: 5px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🏏 Your Cricket Gear is Waiting!</h1>
        </div>
        <div class="content">
            <p>Hi {{UserName}},</p>
            <p>You left some great items in your cart. Don't miss out!</p>

            {{#each CartItems}}
            <div class="cart-item">
                <strong>{{Name}}</strong><br>
                ₹{{Price}} x {{Quantity}}
            </div>
            {{/each}}

            <p><strong>Total: ₹{{TotalValue}}</strong></p>

            {{#if DiscountPercentage}}
            <div class="discount-badge">
                🎉 Special Offer: {{DiscountPercentage}}% OFF if you complete your order now!
            </div>
            {{/if}}

            {{#if FreeShipping}}
            <p>✅ You qualify for FREE SHIPPING!</p>
            {{/if}}

            <a href="{{CartUrl}}" class="cta-button">Complete Your Order</a>

            <p style="margin-top: 30px; color: #666;">
                Still deciding? Our cricket experts are here to help!<br>
                Call us: 1800-GEARIFY or reply to this email.
            </p>
        </div>
    </div>
</body>
</html>
```

**Success Metrics**:
- Email open rate > 25%
- Recovery rate (cart completion after email) > 10%
- Additional revenue from recovered carts: ₹50,000+/month

---

## Phase 2: Intelligence (Weeks 5-10)

**Goal**: Add advanced AI capabilities for search, support, and fraud detection

### 2.1 Natural Language Search (Week 5-7)

**Priority**: P0 | **Impact**: High | **Effort**: Medium

#### AWS Comprehend Integration

**Install Package**:
```bash
dotnet add package AWSSDK.Comprehend
```

**Query Understanding Service**:
```csharp
// File: Gearify.CatalogService/Application/Services/QueryUnderstandingService.cs

using Amazon.Comprehend;
using Amazon.Comprehend.Model;

public class QueryUnderstandingService
{
    private readonly IAmazonComprehend _comprehend;

    public async Task<SearchIntent> AnalyzeSearchQueryAsync(string query)
    {
        // Extract entities (brands, product types, price ranges)
        var entitiesResponse = await _comprehend.DetectEntitiesAsync(new DetectEntitiesRequest
        {
            Text = query,
            LanguageCode = "en"
        });

        // Detect sentiment (buying intent vs browsing)
        var sentimentResponse = await _comprehend.DetectSentimentAsync(new DetectSentimentRequest
        {
            Text = query,
            LanguageCode = "en"
        });

        // Extract key phrases
        var keyPhrasesResponse = await _comprehend.DetectKeyPhrasesAsync(new DetectKeyPhrasesRequest
        {
            Text = query,
            LanguageCode = "en"
        });

        return new SearchIntent
        {
            OriginalQuery = query,
            Entities = entitiesResponse.Entities.Select(e => new SearchEntity
            {
                Type = e.Type,
                Text = e.Text,
                Score = e.Score
            }).ToList(),
            KeyPhrases = keyPhrasesResponse.KeyPhrases.Select(kp => kp.Text).ToList(),
            Sentiment = sentimentResponse.Sentiment,
            Filters = ExtractFilters(query, entitiesResponse.Entities)
        };
    }

    private SearchFilters ExtractFilters(string query, List<Entity> entities)
    {
        var filters = new SearchFilters();

        // Price range extraction
        var pricePattern = @"under\s+(\d+)|below\s+(\d+)|less\s+than\s+(\d+)|<\s*(\d+)";
        var priceMatch = Regex.Match(query.ToLower(), pricePattern);
        if (priceMatch.Success)
        {
            var priceValue = priceMatch.Groups
                .Cast<Group>()
                .Skip(1)
                .FirstOrDefault(g => g.Success)?.Value;

            if (int.TryParse(priceValue, out var maxPrice))
            {
                filters.MaxPrice = maxPrice;
            }
        }

        // Brand extraction
        var knownBrands = new[] { "SS", "MRF", "SG", "Kookaburra", "DSC", "Gray-Nicolls", "Puma" };
        filters.Brands = entities
            .Where(e => knownBrands.Any(b => e.Text.Contains(b, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Text)
            .ToList();

        // Category extraction
        var categoryKeywords = new Dictionary<string, string>
        {
            { "bat", "Bats" },
            { "ball", "Balls" },
            { "shoe", "Shoes" },
            { "helmet", "Helmets" },
            { "pad", "Pads" },
            { "glove", "Gloves" }
        };

        foreach (var kvp in categoryKeywords)
        {
            if (query.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                filters.Category = kvp.Value;
                break;
            }
        }

        return filters;
    }
}
```

**Enhanced Search API**:
```csharp
[HttpGet("search/smart")]
public async Task<ActionResult<SearchResults>> SmartSearch(
    [FromQuery] string q,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    // Understand the query using NLP
    var intent = await _queryUnderstanding.AnalyzeSearchQueryAsync(q);

    // Build Elasticsearch query with extracted filters
    var searchRequest = new SearchRequest
    {
        Query = intent.OriginalQuery,
        Filters = intent.Filters,
        Page = page,
        PageSize = pageSize
    };

    var results = await _searchService.SearchAsync(searchRequest);

    return Ok(new SearchResults
    {
        Query = q,
        UnderstandedIntent = intent,
        Products = results.Products,
        TotalCount = results.TotalCount,
        Page = page,
        PageSize = pageSize
    });
}
```

**Example Queries Handled**:
```
Query: "Best English willow bat under 15000 rupees"
→ Filters: { Category: "Bats", Material: "English Willow", MaxPrice: 15000 }
→ Sort: Rating DESC

Query: "lightweight bat for teenagers"
→ Filters: { Category: "Bats", Weight: "800-1000g", AgeGroup: "Junior" }

Query: "Kookaburra cricket shoes"
→ Filters: { Brand: "Kookaburra", Category: "Shoes" }
```

---

### 2.2 Chatbot (AWS Lex V2) (Week 7-9)

**Priority**: P0 | **Impact**: High | **Effort**: Medium

#### AWS Lex Setup

**Create Lex Bot** (via AWS Console or CLI):
```bash
# Bot configuration JSON
{
  "botName": "GearifyAssistant",
  "description": "Cricket equipment shopping assistant",
  "roleArn": "arn:aws:iam::000000000000:role/LexRole",
  "dataPrivacy": {
    "childDirected": false
  },
  "idleSessionTTLInSeconds": 300
}
```

**Define Intents**:

1. **ProductDiscovery** - Help users find products
2. **OrderTracking** - Check order status
3. **SizeGuidance** - Recommend sizes
4. **FAQs** - Answer common questions

**Slot Types**:
```json
{
  "slotTypeName": "CricketCategory",
  "slotTypeValues": [
    {"sampleValue": {"value": "Bats"}},
    {"sampleValue": {"value": "Balls"}},
    {"sampleValue": {"value": "Shoes"}},
    {"sampleValue": {"value": "Helmets"}},
    {"sampleValue": {"value": "Pads"}},
    {"sampleValue": {"value": "Gloves"}}
  ]
}
```

**Intent: ProductDiscovery**:
```json
{
  "intentName": "ProductDiscovery",
  "sampleUtterances": [
    {"utterance": "I need a cricket bat"},
    {"utterance": "Show me {Category}"},
    {"utterance": "I'm looking for {Category} under {Budget} rupees"},
    {"utterance": "Cricket bat for my {Age} year old"}
  ],
  "slots": [
    {
      "slotName": "Category",
      "slotTypeName": "CricketCategory",
      "valueElicitationSetting": {
        "slotConstraint": "Required",
        "promptSpecification": {
          "messageGroupsList": [
            {
              "message": {
                "plainTextMessage": {
                  "value": "What type of cricket equipment are you looking for?"
                }
              }
            }
          ]
        }
      }
    },
    {
      "slotName": "Budget",
      "slotTypeName": "AMAZON.Number",
      "valueElicitationSetting": {
        "slotConstraint": "Optional",
        "promptSpecification": {
          "messageGroupsList": [
            {
              "message": {
                "plainTextMessage": {
                  "value": "What's your budget in rupees?"
                }
              }
            }
          ]
        }
      }
    }
  ]
}
```

**.NET Lambda Function** (Fulfillment):
```csharp
// File: Gearify.ChatbotService/Functions/LexFulfillmentFunction.cs

using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

public class LexFulfillmentFunction
{
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;

    [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public async Task<LexV2Response> FunctionHandler(LexV2Event lexEvent, ILambdaContext context)
    {
        var intentName = lexEvent.SessionState.Intent.Name;

        return intentName switch
        {
            "ProductDiscovery" => await HandleProductDiscoveryAsync(lexEvent),
            "OrderTracking" => await HandleOrderTrackingAsync(lexEvent),
            "SizeGuidance" => await HandleSizeGuidanceAsync(lexEvent),
            _ => CreateResponse("I didn't understand that. How can I help you today?")
        };
    }

    private async Task<LexV2Response> HandleProductDiscoveryAsync(LexV2Event lexEvent)
    {
        var slots = lexEvent.SessionState.Intent.Slots;

        var category = slots.ContainsKey("Category") ? slots["Category"].Value?.InterpretedValue : null;
        var budget = slots.ContainsKey("Budget") ? slots["Budget"].Value?.InterpretedValue : null;

        if (string.IsNullOrEmpty(category))
        {
            return CreateElicitSlotResponse("Category", "What type of cricket equipment are you looking for?");
        }

        // Fetch products
        var products = await _productService.SearchAsync(new ProductSearchRequest
        {
            Category = category,
            MaxPrice = budget != null ? decimal.Parse(budget) : null,
            Limit = 5
        });

        if (!products.Any())
        {
            return CreateResponse($"Sorry, I couldn't find any {category} matching your criteria. Would you like to adjust your budget?");
        }

        var message = $"Great! I found {products.Count} {category} for you:\n\n";
        foreach (var product in products)
        {
            message += $"• {product.Name} - ₹{product.Price:N0}\n";
        }
        message += "\nWould you like more details on any of these?";

        return CreateResponse(message, new Dictionary<string, object>
        {
            { "products", products }
        });
    }

    private LexV2Response CreateResponse(string message, Dictionary<string, object> sessionAttributes = null)
    {
        return new LexV2Response
        {
            SessionState = new LexV2SessionState
            {
                DialogAction = new LexV2DialogAction
                {
                    Type = "Close"
                },
                Intent = new LexV2Intent
                {
                    Name = "ProductDiscovery",
                    State = "Fulfilled"
                },
                SessionAttributes = sessionAttributes
            },
            Messages = new List<LexV2Message>
            {
                new LexV2Message
                {
                    ContentType = "PlainText",
                    Content = message
                }
            }
        };
    }
}
```

**Frontend Integration** (Angular):
```typescript
// File: gearify-web/src/app/components/chatbot/chatbot.component.ts

import { LexRuntimeV2 } from '@aws-sdk/client-lex-runtime-v2';

export class ChatbotComponent {
  private lexClient: LexRuntimeV2;
  private sessionId: string;
  messages: ChatMessage[] = [];

  constructor() {
    this.lexClient = new LexRuntimeV2({
      region: 'us-east-1',
      credentials: {
        accessKeyId: 'test',
        secretAccessKey: 'test'
      },
      endpoint: 'http://localhost:4566' // LocalStack
    });
    this.sessionId = this.generateSessionId();
  }

  async sendMessage(text: string) {
    this.messages.push({ sender: 'user', text });

    const response = await this.lexClient.recognizeText({
      botId: 'GEARIFY_BOT_ID',
      botAliasId: 'TSTALIASID',
      localeId: 'en_US',
      sessionId: this.sessionId,
      text
    });

    const botMessage = response.messages?.[0]?.content || 'Sorry, I did not understand.';
    this.messages.push({ sender: 'bot', text: botMessage });
  }
}
```

---

### 2.3 Fraud Detection (Week 9-10)

**Priority**: P1 | **Impact**: High | **Effort**: Medium

#### AWS Fraud Detector Setup

**Install Package**:
```bash
dotnet add package AWSSDK.FraudDetector
```

**Create Fraud Detection Model**:
```csharp
// File: Gearify.OrderService/Application/Services/FraudDetectionService.cs

using Amazon.FraudDetector;
using Amazon.FraudDetector.Model;

public class FraudDetectionService
{
    private readonly IAmazonFraudDetector _fraudDetector;
    private readonly string _detectorName = "gearify-order-fraud-detector";

    public async Task<FraudAssessment> AssessOrderAsync(Order order, User user)
    {
        var request = new GetEventPredictionRequest
        {
            DetectorId = _detectorName,
            DetectorVersionId = "1",
            EventId = order.Id,
            EventTypeName = "order_placement",
            EventTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Entities = new List<Entity>
            {
                new Entity
                {
                    EntityType = "customer",
                    EntityId = user.Id
                }
            },
            EventVariables = new Dictionary<string, string>
            {
                { "order_value", order.TotalAmount.ToString() },
                { "payment_method", order.PaymentMethod },
                { "shipping_address", order.ShippingAddress.ToJson() },
                { "user_email", user.Email },
                { "user_phone", user.Phone },
                { "ip_address", order.IpAddress },
                { "user_agent", order.UserAgent },
                { "account_age_days", (DateTime.UtcNow - user.CreatedAt).TotalDays.ToString() },
                { "order_count", user.OrderCount.ToString() },
                { "failed_payment_attempts", order.FailedPaymentAttempts.ToString() }
            }
        };

        var response = await _fraudDetector.GetEventPredictionAsync(request);

        var riskScore = response.ModelScores.FirstOrDefault()?.Scores.Values.FirstOrDefault() ?? 0;
        var outcome = response.RuleResults.FirstOrDefault()?.Outcomes.FirstOrDefault();

        return new FraudAssessment
        {
            OrderId = order.Id,
            RiskScore = riskScore,
            RiskLevel = DetermineRiskLevel(riskScore),
            Outcome = outcome,
            RecommendedAction = DetermineAction(riskScore),
            FraudSignals = ExtractFraudSignals(order, user, riskScore)
        };
    }

    private RiskLevel DetermineRiskLevel(double score)
    {
        return score switch
        {
            < 300 => RiskLevel.Low,
            < 700 => RiskLevel.Medium,
            _ => RiskLevel.High
        };
    }

    private FraudAction DetermineAction(double score)
    {
        return score switch
        {
            < 300 => FraudAction.Approve,
            < 700 => FraudAction.Review,
            _ => FraudAction.Decline
        };
    }

    private List<string> ExtractFraudSignals(Order order, User user, double riskScore)
    {
        var signals = new List<string>();

        if (order.TotalAmount > 50000 && user.OrderCount == 0)
            signals.Add("High-value first-time order");

        if (order.FailedPaymentAttempts > 2)
            signals.Add("Multiple failed payment attempts");

        if ((DateTime.UtcNow - user.CreatedAt).TotalDays < 1)
            signals.Add("New account (< 1 day old)");

        if (order.ShippingAddress.Country != "India")
            signals.Add("International shipping address");

        // IP-based checks would go here (VPN detection, geo-mismatch, etc.)

        return signals;
    }
}
```

**Integration in Order Processing**:
```csharp
// File: Gearify.OrderService/Application/Commands/CreateOrderCommandHandler.cs

public async Task<Result<Order>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
{
    // Assess fraud risk
    var fraudAssessment = await _fraudDetectionService.AssessOrderAsync(command.Order, command.User);

    if (fraudAssessment.RecommendedAction == FraudAction.Decline)
    {
        _logger.LogWarning(
            "Order {OrderId} declined due to fraud risk. Score: {Score}, Signals: {Signals}",
            command.Order.Id, fraudAssessment.RiskScore, string.Join(", ", fraudAssessment.FraudSignals));

        return Result<Order>.Failure("Your order could not be processed. Please contact support.");
    }

    if (fraudAssessment.RecommendedAction == FraudAction.Review)
    {
        command.Order.Status = OrderStatus.PendingReview;
        command.Order.FraudAssessment = fraudAssessment;

        // Notify fraud review team
        await _notificationService.NotifyFraudReviewTeamAsync(command.Order, fraudAssessment);
    }

    // Continue with order creation
    await _orderRepository.CreateAsync(command.Order);

    return Result<Order>.Success(command.Order);
}
```

**Success Metrics**:
- Fraud detection accuracy > 95%
- False positive rate < 2%
- Prevented fraudulent orders: ₹100,000+/month

---

## Phase 3: Optimization (Weeks 11-16)

### 3.1 Demand Forecasting (Week 11-13)

**Priority**: P1 | **Impact**: High | **Effort**: High

#### AWS Forecast Setup

**Data Preparation**:
```csv
# File: forecast-data.csv (Historical sales data)
item_id,timestamp,demand,price,promotion
prod-bat-ss-001,2024-01-01,15,12500,0
prod-bat-ss-001,2024-01-02,12,12500,0
prod-bat-ss-001,2024-01-03,18,12500,0
...
prod-bat-ss-001,2024-03-15,45,11250,1  # IPL season spike with promotion
```

**Implementation**:
```csharp
// File: Gearify.CatalogService/Application/Services/DemandForecastService.cs

using Amazon.ForecastService;
using Amazon.ForecastService.Model;

public class DemandForecastService
{
    private readonly IAmazonForecastService _forecast;
    private readonly string _datasetGroupArn;

    public async Task<Dictionary<string, int>> GetForecastAsync(
        string productId,
        int daysAhead = 30)
    {
        var request = new QueryForecastRequest
        {
            ForecastArn = _datasetGroupArn,
            Filters = new Dictionary<string, string>
            {
                { "item_id", productId }
            },
            StartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            EndDate = DateTime.UtcNow.AddDays(daysAhead).ToString("yyyy-MM-dd")
        };

        var response = await _forecast.QueryForecastAsync(request);

        return response.Forecast.Predictions["p50"]
            .ToDictionary(
                p => p.Timestamp,
                p => (int)Math.Round(p.Value)
            );
    }

    public async Task<InventoryRecommendation> GetInventoryRecommendationAsync(string productId)
    {
        var forecast = await GetForecastAsync(productId, daysAhead: 30);
        var totalDemand = forecast.Values.Sum();
        var currentStock = await _inventoryRepository.GetStockLevelAsync(productId);

        return new InventoryRecommendation
        {
            ProductId = productId,
            CurrentStock = currentStock,
            ForecastedDemand30Days = totalDemand,
            RecommendedOrderQuantity = Math.Max(0, totalDemand - currentStock),
            StockoutRisk = currentStock < totalDemand * 0.5 ? "High" : "Low"
        };
    }
}
```

---

### 3.2 Visual Search (Week 13-15)

**Priority**: P1 | **Impact**: High | **Effort**: High

#### AWS Rekognition Custom Labels

**Training Data Structure**:
```
s3://gearify-ml-data/visual-search/
  ├── training/
  │   ├── bats/
  │   │   ├── img001.jpg
  │   │   ├── img002.jpg
  │   ├── balls/
  │   ├── helmets/
  │   └── manifest.json
  └── testing/
```

**Implementation**:
```csharp
// File: Gearify.MediaService/Application/Services/VisualSearchService.cs

using Amazon.Rekognition;
using Amazon.Rekognition.Model;

public class VisualSearchService
{
    private readonly IAmazonRekognition _rekognition;
    private readonly IProductRepository _productRepository;
    private readonly string _projectVersionArn;

    public async Task<List<VisualSearchResult>> SearchByImageAsync(Stream imageStream)
    {
        // Detect labels in uploaded image
        var detectRequest = new DetectCustomLabelsRequest
        {
            ProjectVersionArn = _projectVersionArn,
            Image = new Image
            {
                Bytes = await imageStream.ReadAllBytesAsync()
            },
            MinConfidence = 70
        };

        var detectResponse = await _rekognition.DetectCustomLabelsAsync(detectRequest);

        // Extract detected product category
        var topLabel = detectResponse.CustomLabels.OrderByDescending(l => l.Confidence).FirstOrDefault();

        if (topLabel == null)
            return new List<VisualSearchResult>();

        // Find similar products in the detected category
        var products = await _productRepository.GetByCategoryAsync(topLabel.Name);

        // For each product, compare image similarity
        var results = new List<VisualSearchResult>();

        foreach (var product in products)
        {
            var similarityScore = await CompareImagesAsync(imageStream, product.ThumbnailUrl);

            results.Add(new VisualSearchResult
            {
                Product = product,
                SimilarityScore = similarityScore,
                DetectedCategory = topLabel.Name,
                Confidence = topLabel.Confidence
            });
        }

        return results.OrderByDescending(r => r.SimilarityScore).Take(10).ToList();
    }

    private async Task<double> CompareImagesAsync(Stream sourceImage, string targetImageUrl)
    {
        var request = new CompareFacesRequest  // Use DetectLabels for products, not CompareFaces
        {
            SourceImage = new Image { Bytes = await sourceImage.ReadAllBytesAsync() },
            TargetImage = new Image { S3Object = new S3Object { Bucket = "gearify-media", Key = targetImageUrl } },
            SimilarityThreshold = 70
        };

        // Implementation would use feature comparison
        return 0.85; // Placeholder
    }
}
```

**API Endpoint**:
```csharp
[HttpPost("search/visual")]
public async Task<ActionResult<List<VisualSearchResult>>> VisualSearch(IFormFile image)
{
    if (image == null || image.Length == 0)
        return BadRequest("No image provided");

    using var stream = image.OpenReadStream();
    var results = await _visualSearchService.SearchByImageAsync(stream);

    return Ok(results);
}
```

---

### 3.3 Dynamic Pricing (Week 15-16)

**Priority**: P2 | **Impact**: High | **Effort**: Medium

**Implementation with ML.NET**:
```csharp
// File: Gearify.CatalogService/Application/Services/DynamicPricingService.cs

using Microsoft.ML;
using Microsoft.ML.Data;

public class DynamicPricingService
{
    private readonly MLContext _mlContext;
    private readonly ITransformer _model;

    public DynamicPricingService()
    {
        _mlContext = new MLContext();
        _model = LoadModel();
    }

    public decimal CalculateOptimalPrice(Product product, MarketConditions conditions)
    {
        var input = new PricingInput
        {
            BasePrice = (float)product.Price,
            CurrentStock = conditions.StockLevel,
            DemandScore = conditions.DemandScore,
            CompetitorPrice = conditions.CompetitorAveragePrice,
            SeasonalFactor = conditions.SeasonalFactor,
            DaysSinceLastSale = conditions.DaysSinceLastSale
        };

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<PricingInput, PricingOutput>(_model);
        var prediction = predictionEngine.Predict(input);

        // Apply business rules
        var optimizedPrice = (decimal)prediction.OptimalPrice;

        // Price floor: Never go below 70% of base price
        var priceFloor = product.Price * 0.7m;

        // Price ceiling: Never exceed 120% of base price
        var priceCeiling = product.Price * 1.2m;

        return Math.Clamp(optimizedPrice, priceFloor, priceCeiling);
    }
}

public class PricingInput
{
    [ColumnName("BasePrice"), LoadColumn(0)]
    public float BasePrice { get; set; }

    [ColumnName("CurrentStock"), LoadColumn(1)]
    public float CurrentStock { get; set; }

    [ColumnName("DemandScore"), LoadColumn(2)]
    public float DemandScore { get; set; }

    [ColumnName("CompetitorPrice"), LoadColumn(3)]
    public float CompetitorPrice { get; set; }

    [ColumnName("SeasonalFactor"), LoadColumn(4)]
    public float SeasonalFactor { get; set; }

    [ColumnName("DaysSinceLastSale"), LoadColumn(5)]
    public float DaysSinceLastSale { get; set; }
}

public class PricingOutput
{
    [ColumnName("Score")]
    public float OptimalPrice { get; set; }
}
```

---

## Phase 4: Advanced Features (Weeks 17-24)

### 4.1 Customer Behavior Analytics
### 4.2 Churn Prediction
### 4.3 Customer Lifetime Value (CLV) Modeling
### 4.4 Sentiment Analysis for Reviews
### 4.5 Image Enhancement Pipeline

*(Details available in feature-specific documentation)*

---

## Cost Estimates

### Phase 1 (Weeks 1-4)
- AWS Personalize: $100-150/month (1 TPS campaign)
- Elasticsearch (t3.small): $50/month
- Hangfire (Redis): Included in existing Redis
- SES (Email): $0.10 per 1,000 emails
- **Total**: ~$150-200/month

### Phase 2 (Weeks 5-10)
- AWS Comprehend: $0.0001 per character (first 50K free)
- AWS Lex: $0.00075 per text request (10K free)
- AWS Fraud Detector: $0.15 per prediction
- **Additional**: ~$100-150/month

### Phase 3 (Weeks 11-16)
- AWS Forecast: $0.60 per 1,000 forecasts
- AWS Rekognition Custom Labels: $4/hour inference
- ML.NET: Free (runs in existing services)
- **Additional**: ~$100-200/month

### Total Monthly Cost (All Phases Active)
- **Production**: $350-550/month
- **Development (LocalStack Pro)**: $0 (local emulation)

---

## Success Metrics Dashboard

### KPIs to Track

**Phase 1**:
- Recommendation CTR > 5%
- Autocomplete usage > 30%
- Cart recovery rate > 10%

**Phase 2**:
- Search result relevance score > 0.8
- Chatbot resolution rate > 60%
- Fraud detection accuracy > 95%

**Phase 3**:
- Forecast accuracy (MAPE) < 15%
- Visual search success rate > 70%
- Dynamic pricing revenue lift > 5%

**Business Impact**:
- Average order value increase: 20%+
- Conversion rate improvement: 15%+
- Customer support cost reduction: 30%+
- Inventory optimization: 25% less overstock

---

## Risk Mitigation

### Technical Risks
1. **AWS Service Costs**: Monitor with CloudWatch alarms, set billing alerts
2. **Model Accuracy**: A/B test all AI features before full rollout
3. **Performance**: Cache predictions in Redis, use async processing
4. **LocalStack Limitations**: Some AI services have limited LocalStack support - test in AWS sandbox

### Business Risks
1. **User Acceptance**: Gradual rollout with user feedback loops
2. **Privacy Concerns**: Clear opt-in/opt-out mechanisms for personalization
3. **Over-Reliance on AI**: Always provide manual override options

---

## Next Steps

1. Review this roadmap with the development team
2. Set up AWS accounts and LocalStack Pro
3. Begin Phase 0 (Prerequisites) - Event tracking infrastructure
4. Start Phase 1 - Week 1: Product Recommendations

**Ready to begin implementation!**
