# Gearify Search Service Architecture

## Overview

The Search Service is a dedicated microservice providing fast, full-text product search capabilities using AWS OpenSearch. It maintains a synchronized search index of products from the Catalog Service and exposes search APIs with features like autocomplete, fuzzy matching, faceted filtering, and relevance scoring.

## Technology Stack

- **Search Engine**: AWS OpenSearch (Elasticsearch-compatible)
- **Runtime**: .NET 8.0
- **Message Queue**: AWS SQS (for event consumption)
- **Event Bus**: AWS SNS (for catalog events)
- **Cache**: Redis (for search result caching)
- **Client Library**: NEST (Elasticsearch .NET client)
- **Local Development**: LocalStack with OpenSearch plugin

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Gateway                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP/REST
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Search Service API                          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   Controllers                                             │   │
│  │   • SearchController (GET /api/search/products)          │   │
│  │   • AutocompleteController (GET /api/search/autocomplete)│   │
│  │   • SuggestionsController (GET /api/search/suggestions)  │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   Application Layer                                       │   │
│  │   • SearchQueryHandler                                    │   │
│  │   • AutocompleteQueryHandler                             │   │
│  │   • FacetedSearchQueryHandler                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   Infrastructure                                          │   │
│  │   • OpenSearchClient (NEST)                              │   │
│  │   • RedisCache                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ NEST Client
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     AWS OpenSearch Cluster                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   Indexes                                                 │   │
│  │   • gearify-products-{tenant-id}                         │   │
│  │     - Full-text search fields                            │   │
│  │     - Facet fields (brand, category, price ranges)       │   │
│  │     - Sorting fields (price, rating, createdAt)          │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                 Background Index Sync Worker                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   SQS Consumer                                            │   │
│  │   • ProductCreatedEventHandler                           │   │
│  │   • ProductUpdatedEventHandler                           │   │
│  │   • ProductDeletedEventHandler                           │   │
│  │   • MediaProcessedEventHandler                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ SQS Messages
                              │
┌─────────────────────────────────────────────────────────────────┐
│                          AWS SQS Queue                           │
│                    search-catalog-events-queue                   │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ SNS Subscription
                              │
┌─────────────────────────────────────────────────────────────────┐
│                          AWS SNS Topic                           │
│                      catalog-events-topic                        │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ Publish Events
                              │
┌─────────────────────────────────────────────────────────────────┐
│                       Catalog Service                            │
│  • Product CRUD operations                                       │
│  • Publishes events on create/update/delete                     │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow

### 1. Product Indexing Flow (Event-Driven)

```
1. User creates/updates product in Catalog Service
   │
   ▼
2. Catalog Service publishes event to SNS
   • ProductCreated / ProductUpdated / ProductDeleted
   │
   ▼
3. SNS forwards event to SQS queue
   • search-catalog-events-queue
   │
   ▼
4. Search Service Background Worker polls SQS
   • Receives event message
   │
   ▼
5. Event Handler processes message
   • Deserialize product data
   • Transform to search document
   │
   ▼
6. Index document in OpenSearch
   • Upsert to gearify-products-{tenantId} index
   • Update search index in real-time
   │
   ▼
7. Delete message from SQS queue
   • Acknowledge successful processing
```

### 2. Search Query Flow

```
1. User searches for "nike running shoes"
   │
   ▼
2. API Gateway routes to Search Service
   • GET /api/search/products?q=nike+running+shoes&tenantId=default
   │
   ▼
3. Search Service checks Redis cache
   • Cache key: search:{tenantId}:{queryHash}
   • TTL: 5 minutes
   │
   ├─ Cache Hit → Return cached results (< 10ms)
   │
   └─ Cache Miss
      │
      ▼
   4. Query OpenSearch
      • Match query on name, description, brand
      • Fuzzy matching enabled
      • Filters applied (category, price range, rating)
      • Boost fields: brand (2x), name (1.5x)
      │
      ▼
   5. OpenSearch returns results
      • Ranked by relevance score
      • Facets aggregated (brands, categories, price ranges)
      │
      ▼
   6. Cache results in Redis
      • Store for 5 minutes
      │
      ▼
   7. Return results to client
      • Products + facets + total count
```

## OpenSearch Index Schema

### Index Name Pattern
```
gearify-products-{tenantId}
```
Example: `gearify-products-default`, `gearify-products-acme-corp`

### Index Settings
```json
{
  "settings": {
    "number_of_shards": 2,
    "number_of_replicas": 1,
    "analysis": {
      "analyzer": {
        "product_name_analyzer": {
          "type": "custom",
          "tokenizer": "standard",
          "filter": ["lowercase", "asciifolding", "edge_ngram_filter"]
        },
        "autocomplete_analyzer": {
          "type": "custom",
          "tokenizer": "standard",
          "filter": ["lowercase", "asciifolding", "autocomplete_filter"]
        }
      },
      "filter": {
        "edge_ngram_filter": {
          "type": "edge_ngram",
          "min_gram": 2,
          "max_gram": 20
        },
        "autocomplete_filter": {
          "type": "edge_ngram",
          "min_gram": 2,
          "max_gram": 10
        }
      }
    }
  }
}
```

### Index Mapping
```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "tenantId": { "type": "keyword" },
      "sku": { "type": "keyword" },
      "name": {
        "type": "text",
        "analyzer": "product_name_analyzer",
        "fields": {
          "keyword": { "type": "keyword" },
          "autocomplete": {
            "type": "text",
            "analyzer": "autocomplete_analyzer"
          }
        }
      },
      "description": {
        "type": "text",
        "analyzer": "standard"
      },
      "brand": {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword" }
        }
      },
      "brandSlug": { "type": "keyword" },
      "department": { "type": "keyword" },
      "departmentSlug": { "type": "keyword" },
      "category": { "type": "keyword" },
      "categorySlug": { "type": "keyword" },
      "subcategory": { "type": "keyword" },
      "subcategorySlug": { "type": "keyword" },
      "price": { "type": "double" },
      "compareAtPrice": { "type": "double" },
      "discountPercentage": { "type": "double" },
      "currency": { "type": "keyword" },
      "ratingAverage": { "type": "double" },
      "ratingCount": { "type": "integer" },
      "thumbnailUrl": { "type": "keyword" },
      "imageUrls": { "type": "keyword" },
      "tags": { "type": "keyword" },
      "isActive": { "type": "boolean" },
      "isDeal": { "type": "boolean" },
      "isClearance": { "type": "boolean" },
      "isNewArrival": { "type": "boolean" },
      "isBestSeller": { "type": "boolean" },
      "isFeatured": { "type": "boolean" },
      "createdAt": { "type": "date" },
      "updatedAt": { "type": "date" }
    }
  }
}
```

## Event Schema

### ProductCreatedEvent / ProductUpdatedEvent
```json
{
  "eventType": "ProductCreated",
  "eventId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2026-01-07T10:30:00.000Z",
  "tenantId": "default",
  "payload": {
    "id": "022f46ce-1882-4966-b1fa-7636c6c62351",
    "tenantId": "default",
    "sku": "NIKE-RUN-001",
    "name": "Nike Air Zoom Pegasus 40",
    "description": "Responsive running shoe with enhanced cushioning",
    "brand": "Nike",
    "brandSlug": "nike",
    "department": "Sports",
    "departmentSlug": "sports",
    "category": "Running Shoes",
    "categorySlug": "running-shoes",
    "subcategory": "Men's Running",
    "subcategorySlug": "mens-running",
    "price": 129.99,
    "compareAtPrice": 159.99,
    "discountPercentage": 18.75,
    "currency": "USD",
    "ratingAverage": 4.5,
    "ratingCount": 127,
    "thumbnailUrl": "https://media.gearify.com/products/nike-run-001/thumb.jpg",
    "imageUrls": ["https://media.gearify.com/products/nike-run-001/1.jpg"],
    "tags": ["running", "sports", "footwear"],
    "isActive": true,
    "isDeal": true,
    "isClearance": false,
    "isNewArrival": false,
    "isBestSeller": true,
    "isFeatured": false,
    "createdAt": "2026-01-07T10:30:00.000Z",
    "updatedAt": "2026-01-07T10:30:00.000Z"
  }
}
```

### ProductDeletedEvent
```json
{
  "eventType": "ProductDeleted",
  "eventId": "660f9511-f3ac-52e5-b827-557766551111",
  "timestamp": "2026-01-07T10:35:00.000Z",
  "tenantId": "default",
  "payload": {
    "id": "022f46ce-1882-4966-b1fa-7636c6c62351",
    "tenantId": "default"
  }
}
```

## API Endpoints

### 1. Product Search
```
GET /api/search/products

Query Parameters:
  - q (string, required): Search query
  - tenantId (string, required): Tenant identifier
  - category (string, optional): Filter by category slug
  - brand (string, optional): Filter by brand slug
  - minPrice (decimal, optional): Minimum price filter
  - maxPrice (decimal, optional): Maximum price filter
  - minRating (decimal, optional): Minimum rating filter (e.g., 4.0)
  - tags (string[], optional): Filter by tags
  - onlyDeals (bool, optional): Show only deals
  - onlyClearance (bool, optional): Show only clearance items
  - sortBy (string, optional): price-asc|price-desc|rating|newest|relevance
  - page (int, optional, default: 1): Page number
  - pageSize (int, optional, default: 20): Results per page

Response:
{
  "results": [
    {
      "id": "022f46ce-1882-4966-b1fa-7636c6c62351",
      "name": "Nike Air Zoom Pegasus 40",
      "brand": "Nike",
      "price": 129.99,
      "ratingAverage": 4.5,
      "thumbnailUrl": "...",
      "_score": 12.5
    }
  ],
  "facets": {
    "brands": [
      { "key": "Nike", "count": 45 },
      { "key": "Adidas", "count": 32 }
    ],
    "categories": [
      { "key": "Running Shoes", "count": 78 }
    ],
    "priceRanges": [
      { "key": "0-50", "count": 12 },
      { "key": "50-100", "count": 45 },
      { "key": "100-200", "count": 67 }
    ]
  },
  "total": 124,
  "page": 1,
  "pageSize": 20,
  "totalPages": 7
}
```

### 2. Autocomplete
```
GET /api/search/autocomplete

Query Parameters:
  - q (string, required): Partial search query
  - tenantId (string, required): Tenant identifier
  - limit (int, optional, default: 10): Max suggestions

Response:
{
  "suggestions": [
    "Nike running shoes",
    "Nike air max",
    "Nike sneakers"
  ]
}
```

### 3. Suggestions (Did You Mean)
```
GET /api/search/suggestions

Query Parameters:
  - q (string, required): Search query
  - tenantId (string, required): Tenant identifier

Response:
{
  "original": "niek shoes",
  "suggestions": [
    { "text": "nike shoes", "score": 0.95 }
  ]
}
```

## Multi-Tenant Isolation

### Index Separation
- Each tenant gets a dedicated index: `gearify-products-{tenantId}`
- Prevents data leakage between tenants
- Allows tenant-specific search tuning

### Query Filtering
All queries include tenant filter:
```json
{
  "query": {
    "bool": {
      "must": [
        { "term": { "tenantId": "default" } },
        { "match": { "name": "running shoes" } }
      ]
    }
  }
}
```

## Caching Strategy

### Cache Layers

1. **Redis Cache (L1)**
   - TTL: 5 minutes for search results
   - TTL: 30 minutes for facet aggregations
   - Key pattern: `search:{tenantId}:{queryHash}`
   - Invalidation: On product update events

2. **OpenSearch Query Cache (L2)**
   - Managed by OpenSearch
   - Caches filter clauses automatically

### Cache Invalidation
```
Event: ProductUpdated (product X)
Action: Delete Redis keys matching pattern search:{tenantId}:*
Result: Next search query gets fresh data from OpenSearch
```

## Synchronization Strategy

### Initial Bulk Sync
```
1. Create OpenSearch index for tenant
2. Query all products from Catalog Service API
3. Bulk index products (batch size: 1000)
4. Verify sync completion
```

### Real-Time Sync (Event-Driven)
```
1. Catalog Service publishes event to SNS
2. SNS → SQS → Search Service Background Worker
3. Worker processes event and updates OpenSearch index
4. Eventual consistency: < 2 seconds
```

### Sync Health Monitoring
- Track SQS queue depth (alert if > 1000)
- Monitor event processing lag (alert if > 10 seconds)
- Daily reconciliation job to detect drift

## Performance Targets

| Metric                    | Target      | Measurement |
|---------------------------|-------------|-------------|
| Search Query Latency      | < 100ms     | P95         |
| Autocomplete Latency      | < 50ms      | P95         |
| Index Update Latency      | < 2s        | P95         |
| Search Throughput         | 500 qps     | Sustained   |
| Index Sync Success Rate   | > 99.9%     | Daily       |
| Cache Hit Rate            | > 70%       | Hourly      |

## Deployment Architecture

### Production (AWS)
```
- OpenSearch Domain: 2 x t3.medium.search instances
- Redis: ElastiCache (1 x cache.t3.micro)
- Search Service API: ECS Fargate (2 tasks)
- Background Worker: ECS Fargate (1 task)
- SQS Queue: Standard queue with DLQ
```

### Local Development (LocalStack)
```
- OpenSearch: LocalStack OpenSearch service
- Redis: Docker container (redis:7-alpine)
- Search Service API: dotnet run
- Background Worker: dotnet run
- SQS: LocalStack SQS service
```

## Cost Estimation (AWS - Monthly)

| Service                   | Configuration          | Cost (USD) |
|---------------------------|------------------------|------------|
| OpenSearch (2 x t3.medium)| 24/7                  | ~$140      |
| ElastiCache (t3.micro)    | 24/7                  | ~$15       |
| ECS Fargate (3 tasks)     | 1vCPU, 2GB each       | ~$45       |
| SQS                       | 1M requests           | ~$0.40     |
| SNS                       | 1M notifications      | ~$0.50     |
| Data Transfer             | 100GB                 | ~$9        |
| **Total**                 |                       | **~$210**  |

Notes:
- Assumes moderate traffic (10K searches/day)
- OpenSearch costs dominate (~67%)
- Can optimize with reserved instances (-30%)

## Security Considerations

1. **Authentication**: All APIs require valid JWT token
2. **Authorization**: Tenant isolation enforced at query level
3. **Encryption**: TLS 1.2+ for data in transit
4. **At-Rest Encryption**: OpenSearch domain encryption enabled
5. **Network**: VPC isolation for OpenSearch cluster
6. **IAM**: Least-privilege roles for service access

## Monitoring & Observability

### Key Metrics
- Search query latency (P50, P95, P99)
- Search error rate
- Index sync lag
- SQS queue depth
- Cache hit rate
- OpenSearch cluster health

### Logging
- Structured JSON logs (Serilog)
- Search query logs (query, latency, result count)
- Event processing logs
- Error logs with correlation IDs

### Alerts
- Search error rate > 1%
- Index sync lag > 30 seconds
- SQS DLQ message count > 0
- OpenSearch cluster status = RED

## Future Enhancements

1. **Personalized Search**: Use user behavior for ranking
2. **Semantic Search**: Vector embeddings with k-NN
3. **Search Analytics**: Track popular queries, zero-result queries
4. **A/B Testing**: Experiment with ranking algorithms
5. **Voice Search**: Speech-to-text integration
6. **Visual Search**: Image-based product search
7. **Geo-Search**: Location-based product availability

---

**Document Version**: 1.0
**Last Updated**: 2026-01-07
**Author**: Claude Code
**Status**: Design Phase
