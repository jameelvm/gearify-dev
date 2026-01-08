# Search Service Implementation Plan

## Development Phases

This document outlines a module-wise implementation plan for the Gearify Search Service. Each phase can be developed, tested, and deployed independently.

---

## Phase 1: Foundation & Infrastructure (Week 1)

### Module 1.1: Project Setup
**Goal**: Create the basic project structure and configuration

**Tasks**:
1. Create solution structure
   ```
   C:/Gearify/gearify-search-svc/
   ├── Gearify.SearchService.sln
   ├── API/
   │   ├── Gearify.SearchService.API.csproj
   │   ├── Program.cs
   │   ├── Startup.cs
   │   └── appsettings.json
   ├── Application/
   │   ├── Gearify.SearchService.Application.csproj
   │   ├── Queries/
   │   └── DTOs/
   ├── Domain/
   │   ├── Gearify.SearchService.Domain.csproj
   │   └── Entities/
   └── Infrastructure/
       ├── Gearify.SearchService.Infrastructure.csproj
       ├── OpenSearch/
       └── Messaging/
   ```

2. Install NuGet packages
   ```bash
   # API Project
   dotnet add package Swashbuckle.AspNetCore
   dotnet add package MediatR

   # Infrastructure Project
   dotnet add package NEST (Elasticsearch .NET client)
   dotnet add package StackExchange.Redis
   dotnet add package AWSSDK.SQS
   dotnet add package AWSSDK.SNS
   ```

3. Configure appsettings.json
   ```json
   {
     "OpenSearch": {
       "Endpoint": "http://localhost:4566",
       "Username": "",
       "Password": "",
       "IndexPrefix": "gearify-products"
     },
     "Redis": {
       "ConnectionString": "localhost:6379",
       "InstanceName": "gearify-search:"
     },
     "AWS": {
       "Region": "us-east-1",
       "ServiceURL": "http://localhost:4566",
       "SQS": {
         "QueueUrl": "http://localhost:4566/000000000000/search-catalog-events-queue"
       }
     }
   }
   ```

**Deliverables**:
- Solution compiles successfully
- Configuration files ready
- Basic API project running on http://localhost:5004

**Testing**: `dotnet run` in API project

---

### Module 1.2: OpenSearch Client Setup
**Goal**: Configure OpenSearch connection and index management

**Tasks**:
1. Create `OpenSearchSettings.cs` in Infrastructure/Configuration
   ```csharp
   public class OpenSearchSettings
   {
       public string Endpoint { get; set; }
       public string Username { get; set; }
       public string Password { get; set; }
       public string IndexPrefix { get; set; }
   }
   ```

2. Create `OpenSearchClientFactory.cs` in Infrastructure/OpenSearch
   ```csharp
   public class OpenSearchClientFactory
   {
       public IElasticClient CreateClient(OpenSearchSettings settings)
       {
           var node = new Uri(settings.Endpoint);
           var connectionSettings = new ConnectionSettings(node)
               .DefaultIndex("products")
               .EnableDebugMode()
               .PrettyJson();

           return new ElasticClient(connectionSettings);
       }
   }
   ```

3. Create `IndexManager.cs` in Infrastructure/OpenSearch
   ```csharp
   public interface IIndexManager
   {
       Task<bool> CreateIndexAsync(string tenantId);
       Task<bool> DeleteIndexAsync(string tenantId);
       Task<bool> IndexExistsAsync(string tenantId);
   }

   public class IndexManager : IIndexManager
   {
       private readonly IElasticClient _client;
       private readonly OpenSearchSettings _settings;

       // Implementation with index creation logic
       // Use mappings from ARCHITECTURE.md
   }
   ```

**Deliverables**:
- OpenSearch client successfully connects to LocalStack
- Index creation works with proper mappings
- Unit tests for IndexManager

**Testing**:
```bash
# Start LocalStack with OpenSearch
cd C:/Gearify/gearify-umbrella
docker-compose up -d

# Verify OpenSearch is running
curl http://localhost:4566/_cluster/health

# Run index creation test
dotnet test --filter "ClassName=IndexManagerTests"
```

---

### Module 1.3: Domain Models
**Goal**: Define core domain entities and DTOs

**Tasks**:
1. Create `ProductSearchDocument.cs` in Domain/Entities
   ```csharp
   public class ProductSearchDocument
   {
       public string Id { get; set; }
       public string TenantId { get; set; }
       public string Sku { get; set; }
       public string Name { get; set; }
       public string Description { get; set; }
       public string Brand { get; set; }
       public string BrandSlug { get; set; }
       public string Department { get; set; }
       public string DepartmentSlug { get; set; }
       public string Category { get; set; }
       public string CategorySlug { get; set; }
       public string Subcategory { get; set; }
       public string SubcategorySlug { get; set; }
       public decimal Price { get; set; }
       public decimal CompareAtPrice { get; set; }
       public decimal? DiscountPercentage { get; set; }
       public string Currency { get; set; }
       public decimal? RatingAverage { get; set; }
       public int? RatingCount { get; set; }
       public string ThumbnailUrl { get; set; }
       public List<string> ImageUrls { get; set; }
       public List<string> Tags { get; set; }
       public bool IsActive { get; set; }
       public bool IsDeal { get; set; }
       public bool IsClearance { get; set; }
       public bool IsNewArrival { get; set; }
       public bool IsBestSeller { get; set; }
       public bool IsFeatured { get; set; }
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
   }
   ```

2. Create DTOs in Application/DTOs
   - `SearchProductsQuery.cs`
   - `SearchProductsResponse.cs`
   - `ProductSearchResult.cs`
   - `SearchFacets.cs`

**Deliverables**:
- All domain models defined
- DTOs match API contract from ARCHITECTURE.md

---

## Phase 2: Core Search Functionality (Week 2)

### Module 2.1: Product Indexing
**Goal**: Implement product indexing operations

**Tasks**:
1. Create `IProductIndexService.cs` in Application/Services
   ```csharp
   public interface IProductIndexService
   {
       Task<bool> IndexProductAsync(ProductSearchDocument product);
       Task<bool> BulkIndexProductsAsync(List<ProductSearchDocument> products);
       Task<bool> DeleteProductAsync(string productId, string tenantId);
       Task<bool> UpdateProductAsync(ProductSearchDocument product);
   }
   ```

2. Create `ProductIndexService.cs` in Infrastructure/OpenSearch
   ```csharp
   public class ProductIndexService : IProductIndexService
   {
       private readonly IElasticClient _client;
       private readonly OpenSearchSettings _settings;

       public async Task<bool> IndexProductAsync(ProductSearchDocument product)
       {
           var indexName = $"{_settings.IndexPrefix}-{product.TenantId}";
           var response = await _client.IndexAsync(product, idx => idx
               .Index(indexName)
               .Id(product.Id)
           );

           return response.IsValid;
       }

       // Implement other methods...
   }
   ```

**Deliverables**:
- Single product indexing works
- Bulk indexing works (batch size: 1000)
- Product deletion works
- Integration tests passing

**Testing**:
```bash
# Integration test
dotnet test --filter "ClassName=ProductIndexServiceTests"

# Manual verification
curl -X GET "http://localhost:4566/gearify-products-default/_search?pretty"
```

---

### Module 2.2: Search Query Handler
**Goal**: Implement basic product search with filters

**Tasks**:
1. Create `SearchProductsQuery.cs` in Application/Queries
   ```csharp
   public class SearchProductsQuery : IRequest<SearchProductsResponse>
   {
       public string Query { get; set; }
       public string TenantId { get; set; }
       public string Category { get; set; }
       public string Brand { get; set; }
       public decimal? MinPrice { get; set; }
       public decimal? MaxPrice { get; set; }
       public decimal? MinRating { get; set; }
       public List<string> Tags { get; set; }
       public bool? OnlyDeals { get; set; }
       public bool? OnlyClearance { get; set; }
       public string SortBy { get; set; } = "relevance";
       public int Page { get; set; } = 1;
       public int PageSize { get; set; } = 20;
   }
   ```

2. Create `SearchProductsQueryHandler.cs` in Application/Queries
   ```csharp
   public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResponse>
   {
       private readonly IElasticClient _client;
       private readonly IDistributedCache _cache;

       public async Task<SearchProductsResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
       {
           // Check cache first
           var cacheKey = GenerateCacheKey(request);
           var cachedResult = await _cache.GetStringAsync(cacheKey);
           if (cachedResult != null)
           {
               return JsonSerializer.Deserialize<SearchProductsResponse>(cachedResult);
           }

           // Build OpenSearch query
           var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
               .Index($"gearify-products-{request.TenantId}")
               .From((request.Page - 1) * request.PageSize)
               .Size(request.PageSize)
               .Query(q => BuildQuery(q, request))
               .Aggregations(a => BuildAggregations(a))
               .Sort(sort => BuildSort(sort, request.SortBy))
           );

           // Map response
           var response = MapSearchResponse(searchResponse);

           // Cache result
           await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response),
               new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

           return response;
       }

       private QueryContainer BuildQuery(QueryContainerDescriptor<ProductSearchDocument> q, SearchProductsQuery request)
       {
           var must = new List<QueryContainer>();

           // Tenant filter (required)
           must.Add(q.Term(t => t.Field(f => f.TenantId).Value(request.TenantId)));

           // Active products only
           must.Add(q.Term(t => t.Field(f => f.IsActive).Value(true)));

           // Text search (multi-match on name, description, brand)
           if (!string.IsNullOrWhiteSpace(request.Query))
           {
               must.Add(q.MultiMatch(mm => mm
                   .Fields(f => f
                       .Field(p => p.Name, boost: 2.0)
                       .Field(p => p.Brand, boost: 1.5)
                       .Field(p => p.Description)
                   )
                   .Query(request.Query)
                   .Fuzziness(Fuzziness.Auto)
               ));
           }

           // Category filter
           if (!string.IsNullOrWhiteSpace(request.Category))
           {
               must.Add(q.Term(t => t.Field(f => f.CategorySlug).Value(request.Category)));
           }

           // Brand filter
           if (!string.IsNullOrWhiteSpace(request.Brand))
           {
               must.Add(q.Term(t => t.Field(f => f.BrandSlug).Value(request.Brand)));
           }

           // Price range filter
           if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
           {
               must.Add(q.Range(r =>
               {
                   var range = r.Field(f => f.Price);
                   if (request.MinPrice.HasValue)
                       range = range.GreaterThanOrEquals((double)request.MinPrice.Value);
                   if (request.MaxPrice.HasValue)
                       range = range.LessThanOrEquals((double)request.MaxPrice.Value);
                   return range;
               }));
           }

           // Rating filter
           if (request.MinRating.HasValue)
           {
               must.Add(q.Range(r => r
                   .Field(f => f.RatingAverage)
                   .GreaterThanOrEquals((double)request.MinRating.Value)
               ));
           }

           // Tags filter
           if (request.Tags != null && request.Tags.Any())
           {
               must.Add(q.Terms(t => t.Field(f => f.Tags).Terms(request.Tags)));
           }

           // Deals filter
           if (request.OnlyDeals.HasValue && request.OnlyDeals.Value)
           {
               must.Add(q.Term(t => t.Field(f => f.IsDeal).Value(true)));
           }

           // Clearance filter
           if (request.OnlyClearance.HasValue && request.OnlyClearance.Value)
           {
               must.Add(q.Term(t => t.Field(f => f.IsClearance).Value(true)));
           }

           return q.Bool(b => b.Must(must));
       }
   }
   ```

3. Create `SearchController.cs` in API/Controllers
   ```csharp
   [ApiController]
   [Route("api/search")]
   public class SearchController : ControllerBase
   {
       private readonly IMediator _mediator;

       [HttpGet("products")]
       public async Task<ActionResult<SearchProductsResponse>> SearchProducts(
           [FromQuery] string q,
           [FromQuery] string tenantId,
           [FromQuery] string category,
           [FromQuery] string brand,
           [FromQuery] decimal? minPrice,
           [FromQuery] decimal? maxPrice,
           [FromQuery] decimal? minRating,
           [FromQuery] string[] tags,
           [FromQuery] bool? onlyDeals,
           [FromQuery] bool? onlyClearance,
           [FromQuery] string sortBy = "relevance",
           [FromQuery] int page = 1,
           [FromQuery] int pageSize = 20)
       {
           var query = new SearchProductsQuery
           {
               Query = q,
               TenantId = tenantId,
               Category = category,
               Brand = brand,
               MinPrice = minPrice,
               MaxPrice = maxPrice,
               MinRating = minRating,
               Tags = tags?.ToList(),
               OnlyDeals = onlyDeals,
               OnlyClearance = onlyClearance,
               SortBy = sortBy,
               Page = page,
               PageSize = pageSize
           };

           var result = await _mediator.Send(query);
           return Ok(result);
       }
   }
   ```

**Deliverables**:
- Basic text search works
- All filters work correctly
- Sorting works (price, rating, newest, relevance)
- Pagination works
- API endpoint returns proper JSON

**Testing**:
```bash
# Test search endpoint
curl "http://localhost:5004/api/search/products?q=nike&tenantId=default&page=1&pageSize=10"

# Test with filters
curl "http://localhost:5004/api/search/products?q=shoes&tenantId=default&category=running-shoes&minPrice=50&maxPrice=200&minRating=4.0"

# Test sorting
curl "http://localhost:5004/api/search/products?q=nike&tenantId=default&sortBy=price-asc"
```

---

### Module 2.3: Faceted Search (Aggregations)
**Goal**: Add facets/aggregations for filtering UI

**Tasks**:
1. Update `SearchProductsQueryHandler` to include aggregations
   ```csharp
   private AggregationContainerDescriptor<ProductSearchDocument> BuildAggregations(
       AggregationContainerDescriptor<ProductSearchDocument> a)
   {
       return a
           .Terms("brands", t => t
               .Field(f => f.BrandSlug)
               .Size(50)
           )
           .Terms("categories", t => t
               .Field(f => f.CategorySlug)
               .Size(50)
           )
           .Range("priceRanges", r => r
               .Field(f => f.Price)
               .Ranges(
                   ranges => ranges.To(50),
                   ranges => ranges.From(50).To(100),
                   ranges => ranges.From(100).To(200),
                   ranges => ranges.From(200)
               )
           );
   }
   ```

2. Map aggregation results to `SearchFacets` DTO

**Deliverables**:
- Facets returned in search response
- Brand facets with counts
- Category facets with counts
- Price range facets with counts

**Testing**:
```bash
# Verify facets in response
curl "http://localhost:5004/api/search/products?q=&tenantId=default" | jq '.facets'
```

---

## Phase 3: Event-Driven Sync (Week 3)

### Module 3.1: Event Models & DTOs
**Goal**: Define event schemas for catalog synchronization

**Tasks**:
1. Create event models in Domain/Events
   - `ProductCreatedEvent.cs`
   - `ProductUpdatedEvent.cs`
   - `ProductDeletedEvent.cs`
   - `CatalogEvent.cs` (base class)

2. Add JSON serialization attributes

**Deliverables**:
- Event models match schema in ARCHITECTURE.md
- JSON serialization/deserialization works

---

### Module 3.2: SQS Consumer Setup
**Goal**: Implement background worker to consume catalog events from SQS

**Tasks**:
1. Create `CatalogEventConsumer.cs` in Infrastructure/Messaging
   ```csharp
   public class CatalogEventConsumer : BackgroundService
   {
       private readonly IAmazonSQS _sqsClient;
       private readonly IServiceProvider _serviceProvider;
       private readonly string _queueUrl;

       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
           while (!stoppingToken.IsCancellationRequested)
           {
               var request = new ReceiveMessageRequest
               {
                   QueueUrl = _queueUrl,
                   MaxNumberOfMessages = 10,
                   WaitTimeSeconds = 20 // Long polling
               };

               var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

               foreach (var message in response.Messages)
               {
                   await ProcessMessageAsync(message, stoppingToken);
               }
           }
       }

       private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
       {
           try
           {
               // Deserialize SNS message wrapper
               var snsMessage = JsonSerializer.Deserialize<SnsMessage>(message.Body);
               var eventData = JsonSerializer.Deserialize<CatalogEvent>(snsMessage.Message);

               // Create scope and get handler
               using var scope = _serviceProvider.CreateScope();
               var handler = scope.ServiceProvider.GetRequiredService<ICatalogEventHandler>();

               // Process event
               await handler.HandleAsync(eventData, cancellationToken);

               // Delete message from queue
               await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
           }
           catch (Exception ex)
           {
               // Log error (message will return to queue after visibility timeout)
               Console.WriteLine($"Error processing message: {ex.Message}");
           }
       }
   }
   ```

2. Create `ICatalogEventHandler.cs` in Application/Events
   ```csharp
   public interface ICatalogEventHandler
   {
       Task HandleAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken);
   }

   public class CatalogEventHandler : ICatalogEventHandler
   {
       private readonly IProductIndexService _indexService;

       public async Task HandleAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken)
       {
           switch (catalogEvent.EventType)
           {
               case "ProductCreated":
               case "ProductUpdated":
                   var product = MapToSearchDocument(catalogEvent.Payload);
                   await _indexService.IndexProductAsync(product);
                   break;

               case "ProductDeleted":
                   await _indexService.DeleteProductAsync(
                       catalogEvent.Payload.Id,
                       catalogEvent.Payload.TenantId);
                   break;
           }
       }
   }
   ```

3. Register background service in `Startup.cs`
   ```csharp
   services.AddHostedService<CatalogEventConsumer>();
   ```

**Deliverables**:
- Background worker runs and polls SQS
- Events are processed and index is updated
- Failed messages go to DLQ after retries

**Testing**:
```bash
# Publish test event to SNS
aws --endpoint-url=http://localhost:4566 sns publish \
  --topic-arn arn:aws:sns:us-east-1:000000000000:catalog-events-topic \
  --message '{"eventType":"ProductCreated","tenantId":"default","payload":{...}}'

# Verify message consumed
docker logs gearify-search-svc

# Verify product indexed
curl "http://localhost:4566/gearify-products-default/_doc/{productId}?pretty"
```

---

### Module 3.3: Catalog Service Integration
**Goal**: Update Catalog Service to publish events to SNS

**Tasks**:
1. Install AWS SNS SDK in Catalog Service
   ```bash
   cd C:/Gearify/gearify-catalog-svc
   dotnet add package AWSSDK.SimpleNotificationService
   ```

2. Create `IEventPublisher.cs` in Catalog Service
   ```csharp
   public interface IEventPublisher
   {
       Task PublishProductCreatedAsync(Product product);
       Task PublishProductUpdatedAsync(Product product);
       Task PublishProductDeletedAsync(string productId, string tenantId);
   }

   public class SnsEventPublisher : IEventPublisher
   {
       private readonly IAmazonSimpleNotificationService _snsClient;
       private readonly string _topicArn;

       public async Task PublishProductCreatedAsync(Product product)
       {
           var catalogEvent = new
           {
               eventType = "ProductCreated",
               eventId = Guid.NewGuid().ToString(),
               timestamp = DateTime.UtcNow,
               tenantId = product.TenantId,
               payload = product
           };

           await _snsClient.PublishAsync(_topicArn, JsonSerializer.Serialize(catalogEvent));
       }
   }
   ```

3. Update `ProductsController` to publish events
   ```csharp
   [HttpPost]
   public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductCommand command)
   {
       var product = await _mediator.Send(command);

       // Publish event to SNS
       await _eventPublisher.PublishProductCreatedAsync(product);

       return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
   }
   ```

**Deliverables**:
- Catalog Service publishes events to SNS on CRUD operations
- Events flow: Catalog → SNS → SQS → Search Service
- End-to-end integration test passes

**Testing**:
```bash
# Create product via Catalog Service API
curl -X POST http://localhost:5001/api/products \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d '{"name":"Test Product","brand":"Nike",...}'

# Wait 2 seconds for event processing

# Search for product via Search Service API
curl "http://localhost:5004/api/search/products?q=Test+Product&tenantId=default"
```

---

## Phase 4: Advanced Features (Week 4)

### Module 4.1: Autocomplete
**Goal**: Implement autocomplete/typeahead search

**Tasks**:
1. Create `AutocompleteQuery.cs` and handler
2. Use edge n-gram analyzer on `name.autocomplete` field
3. Return top 10 suggestions

**Deliverables**:
- GET /api/search/autocomplete endpoint
- Returns suggestions as user types

**Testing**:
```bash
curl "http://localhost:5004/api/search/autocomplete?q=nik&tenantId=default"
# Response: ["nike", "nike air max", "nike running shoes"]
```

---

### Module 4.2: Did You Mean (Suggestions)
**Goal**: Implement "did you mean" for misspelled queries

**Tasks**:
1. Create `SuggestionsQuery.cs` and handler
2. Use OpenSearch suggesters API
3. Return spelling corrections

**Deliverables**:
- GET /api/search/suggestions endpoint
- Returns corrected query if misspelled

**Testing**:
```bash
curl "http://localhost:5004/api/search/suggestions?q=niek+shose&tenantId=default"
# Response: {"original":"niek shose","suggestions":[{"text":"nike shoes","score":0.95}]}
```

---

### Module 4.3: Redis Caching
**Goal**: Add Redis caching layer for search results

**Tasks**:
1. Install StackExchange.Redis
2. Implement cache-aside pattern in query handlers
3. Cache key format: `search:{tenantId}:{queryHash}`
4. TTL: 5 minutes
5. Invalidate on product updates

**Deliverables**:
- Search queries check cache first
- Cache hit rate > 70%
- Cache invalidation on updates

**Testing**:
```bash
# First query (cache miss)
time curl "http://localhost:5004/api/search/products?q=nike&tenantId=default"
# ~100ms

# Second query (cache hit)
time curl "http://localhost:5004/api/search/products?q=nike&tenantId=default"
# ~10ms
```

---

### Module 4.4: Initial Bulk Sync
**Goal**: Create admin endpoint to bulk sync all products from Catalog Service

**Tasks**:
1. Create `BulkSyncController.cs` with admin authentication
2. Call Catalog Service API to get all products
3. Bulk index products (batch size: 1000)
4. Track sync progress

**Deliverables**:
- POST /api/admin/sync endpoint
- Syncs all products for a tenant
- Returns sync status

**Testing**:
```bash
# Trigger bulk sync
curl -X POST "http://localhost:5004/api/admin/sync?tenantId=default" \
  -H "Authorization: Bearer {admin-token}"

# Verify products indexed
curl "http://localhost:4566/gearify-products-default/_count?pretty"
```

---

## Phase 5: Testing & Documentation (Week 5)

### Module 5.1: Unit Tests
**Goal**: Achieve > 80% code coverage

**Tasks**:
1. Unit tests for query handlers
2. Unit tests for index service
3. Unit tests for event handlers
4. Mock OpenSearch and Redis

**Deliverables**:
- All unit tests passing
- Code coverage > 80%

---

### Module 5.2: Integration Tests
**Goal**: Test end-to-end flows with LocalStack

**Tasks**:
1. Create docker-compose for test environment
2. Integration tests for search API
3. Integration tests for event processing
4. Integration tests for bulk sync

**Deliverables**:
- All integration tests passing
- Can run tests in CI/CD

---

### Module 5.3: API Documentation
**Goal**: Generate Swagger/OpenAPI docs

**Tasks**:
1. Configure Swashbuckle in Startup.cs
2. Add XML comments to controllers
3. Add example requests/responses
4. Test Swagger UI

**Deliverables**:
- Swagger UI at http://localhost:5004/swagger
- All endpoints documented

---

### Module 5.4: Performance Testing
**Goal**: Validate performance targets from ARCHITECTURE.md

**Tasks**:
1. Use k6 or JMeter for load testing
2. Test search query latency (target: < 100ms P95)
3. Test autocomplete latency (target: < 50ms P95)
4. Test throughput (target: 500 qps)

**Deliverables**:
- Performance test results documented
- Targets met or optimization plan created

---

## Phase 6: Deployment (Week 6)

### Module 6.1: LocalStack Setup
**Goal**: Add Search Service to docker-compose

**Tasks**:
1. Update `C:/Gearify/gearify-umbrella/docker-compose.yml`
   ```yaml
   search-svc:
     build:
       context: ../gearify-search-svc
       dockerfile: Dockerfile
     ports:
       - "5004:80"
     environment:
       - OpenSearch__Endpoint=http://localstack:4566
       - Redis__ConnectionString=redis:6379
       - AWS__ServiceURL=http://localstack:4566
     depends_on:
       - localstack
       - redis
   ```

2. Create SQS queue and SNS topic in LocalStack init script
3. Subscribe SQS queue to SNS topic

**Deliverables**:
- Search Service runs in docker-compose
- All services communicate correctly

---

### Module 6.2: Monitoring & Logging
**Goal**: Add structured logging and health checks

**Tasks**:
1. Configure Serilog for structured logging
2. Add health check endpoint: GET /health
3. Add metrics endpoint: GET /metrics
4. Log search queries with latency

**Deliverables**:
- Health checks pass
- Logs are structured JSON
- Key metrics tracked

---

## Summary: Development Timeline

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Phase 1: Foundation | Week 1 | Project setup, OpenSearch client, domain models |
| Phase 2: Core Search | Week 2 | Search API, filtering, facets |
| Phase 3: Event Sync | Week 3 | SQS consumer, SNS publisher, real-time sync |
| Phase 4: Advanced | Week 4 | Autocomplete, suggestions, caching, bulk sync |
| Phase 5: Testing | Week 5 | Unit tests, integration tests, performance tests |
| Phase 6: Deployment | Week 6 | Docker, monitoring, production readiness |

**Total**: 6 weeks (1 developer)

---

## Getting Started: First Steps

### Step 1: Create Project Structure
```bash
cd C:/Gearify
dotnet new sln -n Gearify.SearchService -o gearify-search-svc
cd gearify-search-svc

dotnet new webapi -n Gearify.SearchService.API -o API
dotnet new classlib -n Gearify.SearchService.Application -o Application
dotnet new classlib -n Gearify.SearchService.Domain -o Domain
dotnet new classlib -n Gearify.SearchService.Infrastructure -o Infrastructure

dotnet sln add API/Gearify.SearchService.API.csproj
dotnet sln add Application/Gearify.SearchService.Application.csproj
dotnet sln add Domain/Gearify.SearchService.Domain.csproj
dotnet sln add Infrastructure/Gearify.SearchService.Infrastructure.csproj
```

### Step 2: Add Project References
```bash
cd API
dotnet add reference ../Application/Gearify.SearchService.Application.csproj

cd ../Application
dotnet add reference ../Domain/Gearify.SearchService.Domain.csproj
dotnet add reference ../Infrastructure/Gearify.SearchService.Infrastructure.csproj

cd ../Infrastructure
dotnet add reference ../Domain/Gearify.SearchService.Domain.csproj
```

### Step 3: Install NuGet Packages
```bash
cd ../API
dotnet add package Swashbuckle.AspNetCore
dotnet add package MediatR
dotnet add package Serilog.AspNetCore

cd ../Application
dotnet add package MediatR

cd ../Infrastructure
dotnet add package NEST
dotnet add package StackExchange.Redis
dotnet add package AWSSDK.SQS
dotnet add package AWSSDK.SimpleNotificationService
```

### Step 4: Configure appsettings.json
Follow Module 1.1 configuration

### Step 5: Start LocalStack
```bash
cd C:/Gearify/gearify-umbrella
docker-compose up -d
```

### Step 6: Verify Setup
```bash
cd C:/Gearify/gearify-search-svc/API
dotnet run
```

Visit: http://localhost:5004/swagger

---

## Development Best Practices

1. **Follow Clean Architecture**: Domain → Application → Infrastructure → API
2. **Use MediatR**: All queries/commands through MediatR handlers
3. **Dependency Injection**: Register services in Startup.cs
4. **Configuration**: Use IOptions<T> pattern for settings
5. **Logging**: Use structured logging (Serilog)
6. **Error Handling**: Global exception handler middleware
7. **Testing**: Write tests alongside implementation (TDD)
8. **Git Commits**: Commit after each module completion

---

**Document Version**: 1.0
**Last Updated**: 2026-01-07
**Author**: Claude Code
**Status**: Ready for Implementation
