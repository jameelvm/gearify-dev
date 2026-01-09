# SearchProductsQueryHandler Documentation

## Overview

The `SearchProductsQueryHandler` is the core component that handles product search requests in the Gearify Search Service. It follows the **CQRS (Command Query Responsibility Segregation)** pattern using **MediatR** library.

---

## Architecture Flow

```
┌──────────────┐     ┌──────────┐     ┌─────────────────────────┐     ┌────────────┐
│   Client     │────▶│ Controller│────▶│ SearchProductsQueryHandler │────▶│ OpenSearch │
│  (Browser)   │     │          │     │                         │     │            │
└──────────────┘     └──────────┘     └─────────────────────────┘     └────────────┘
                          │                       │
                          │   IMediator.Send()    │
                          │──────────────────────▶│
```

### Request Flow

1. **Client** sends HTTP GET request to `/api/search/products?q=nike&brand=nike`
2. **SearchController** creates `SearchProductsQuery` object and sends via `IMediator`
3. **MediatR** automatically routes to `SearchProductsQueryHandler`
4. **Handler** builds OpenSearch query and executes search
5. **Response** flows back with products and facets

---

## File Location

```
gearify-search-svc/
└── Application/
    └── Queries/
        ├── SearchProductsQuery.cs        # Request DTO
        └── SearchProductsQueryHandler.cs # Handler (this file)
```

---

## Class Structure

```csharp
public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResponse>
{
    // Dependencies
    private readonly IElasticClient _client;        // OpenSearch client
    private readonly IIndexManager _indexManager;   // Index name resolver
    private readonly ILogger _logger;               // Logging

    // Main entry point
    public async Task<SearchProductsResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)

    // Helper methods
    private QueryContainer BuildQuery(...)          // Builds search filters
    private SortDescriptor BuildSort(...)           // Builds sorting
    private AggregationContainerDescriptor BuildAggregations(...)  // Builds facets
    private SearchProductsResponse MapToResponse(...)              // Maps results
    private SearchFacets MapFacets(...)             // Maps facet results
}
```

---

## Method-by-Method Explanation

### 1. Handle() - Main Entry Point

```csharp
public async Task<SearchProductsResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
```

**Purpose:** Entry point called by MediatR when `IMediator.Send(query)` is invoked.

**What it does:**
1. Resolves tenant ID (defaults to "default-tenant")
2. Gets the correct index name for the tenant
3. Builds and executes the OpenSearch query
4. Maps results to response DTO

**Example:**
```csharp
// Input
request = {
    TenantId: "store-123",
    SearchTerm: "nike shoes",
    MinPrice: 50,
    MaxPrice: 100,
    Page: 1,
    PageSize: 20
}

// Output
response = {
    Items: [...],
    TotalCount: 45,
    Page: 1,
    PageSize: 20,
    TotalPages: 3,
    Facets: { Brands: [...], Categories: [...] }
}
```

---

### 2. BuildQuery() - Creating Search Filters

```csharp
private QueryContainer BuildQuery(QueryContainerDescriptor<ProductSearchDocument> q, SearchProductsQuery request)
```

**Purpose:** Builds the OpenSearch query with all filters. Think of it as building a SQL WHERE clause.

**How filters work:**

| Filter | Code | OpenSearch Query |
|--------|------|------------------|
| Active only | `q.Term(f => f.IsActive, true)` | `{"term": {"isActive": true}}` |
| Text search | `q.MultiMatch(...)` | `{"multi_match": {"query": "nike"}}` |
| Category | `q.Term(f => f.CategorySlug, "running")` | `{"term": {"categorySlug": "running"}}` |
| Price range | `q.Range(f => f.Price)` | `{"range": {"price": {"gte": 50, "lte": 100}}}` |

**Filter Logic:**
```
All filters are combined with AND logic (Bool Must query)

Example: "nike shoes" + brand:nike + price:50-100

OpenSearch Query:
{
  "bool": {
    "must": [
      { "term": { "isActive": true } },
      { "multi_match": { "query": "nike shoes", "fields": ["name^3", "brand^2", "description"] } },
      { "term": { "brandSlug": "nike" } },
      { "range": { "price": { "gte": 50, "lte": 100 } } }
    ]
  }
}
```

**Full-Text Search Fields with Boosting:**

```csharp
.Fields(f => f
    .Field(p => p.Name, boost: 3)           // Name matches are 3x more important
    .Field("name.autocomplete", boost: 2)   // Partial matches are 2x important
    .Field(p => p.Description)              // Description has default weight (1x)
    .Field(p => p.Brand, boost: 2)          // Brand matches are 2x important
    .Field(p => p.Category)                 // Category has default weight
    .Field(p => p.Tags)                     // Tags have default weight
)
```

**Example - How boosting affects results:**
```
Search: "nike"

Product A: Name = "Nike Air Max"     → Score: 3.0 (matched name)
Product B: Brand = "Nike"            → Score: 2.0 (matched brand)
Product C: Tags = ["nike", "sports"] → Score: 1.0 (matched tags)

Result order: A, B, C (highest score first)
```

---

### 3. BuildSort() - Ordering Results

```csharp
private SortDescriptor<ProductSearchDocument> BuildSort(
    SortDescriptor<ProductSearchDocument> sort,
    string? sortBy,
    string? sortDirection)
```

**Purpose:** Determines the order of search results.

**Sort Options:**

| sortBy Value | What Happens | Use Case |
|--------------|--------------|----------|
| `"relevance"` (default) | Sort by search score (_score) | Best matches first |
| `"price"` | Sort by price field | Cheapest/most expensive first |
| `"name"` | Sort by name.keyword | Alphabetical order |
| `"rating"` | Sort by ratingAverage | Highest rated first |
| `"newest"` | Sort by createdAt | Recently added first |
| `"popularity"` | Sort by ratingCount | Most reviewed first |

**Sort Direction:**
- `"asc"` → Ascending (A-Z, low to high)
- `"desc"` → Descending (Z-A, high to low)

**Example:**
```
GET /api/search/products?q=shoes&sortBy=price&sortDirection=asc

Result: Cheapest shoes first
[
  { name: "Budget Runner", price: 29.99 },
  { name: "Mid-Range Shoe", price: 79.99 },
  { name: "Premium Sneaker", price: 199.99 }
]
```

---

### 4. BuildAggregations() - Creating Facets

```csharp
private AggregationContainerDescriptor<ProductSearchDocument> BuildAggregations(
    AggregationContainerDescriptor<ProductSearchDocument> a)
```

**Purpose:** Creates facets (filter counts) for the search UI sidebar.

**What are Facets?**

Facets are the filter options with counts you see on e-commerce sites:

```
┌─────────────────────────┐
│ Brand                   │
│ ├── Nike (45)           │
│ ├── Adidas (32)         │
│ └── Puma (18)           │
│                         │
│ Price                   │
│ ├── Under $25 (12)      │
│ ├── $25-$50 (28)        │
│ ├── $50-$100 (45)       │
│ └── Over $200 (8)       │
│                         │
│ Rating                  │
│ ├── 4★ & up (67)        │
│ └── 3★ & up (89)        │
└─────────────────────────┘
```

**Aggregation Types:**

| Type | Purpose | Example |
|------|---------|---------|
| **Terms** | Count unique values | Brands: Nike (45), Adidas (32) |
| **Range** | Count values in ranges | Price: $0-50 (28), $50-100 (45) |

**Code Breakdown:**

```csharp
// Terms aggregation for brands
.Terms("brands", t => t
    .Field("brand.keyword")  // Use keyword field for exact matching
    .Size(50)                // Return top 50 brands
)

// Range aggregation for price
.Range("price_ranges", r => r
    .Field(f => f.Price)
    .Ranges(
        range => range.Key("under_25").To(25),           // $0 - $25
        range => range.Key("25_to_50").From(25).To(50),  // $25 - $50
        range => range.Key("50_to_100").From(50).To(100) // $50 - $100
        // ...
    )
)
```

---

### 5. MapToResponse() - Converting Results

```csharp
private SearchProductsResponse MapToResponse(
    ISearchResponse<ProductSearchDocument> response,
    SearchProductsQuery request)
```

**Purpose:** Converts raw OpenSearch response to clean API response.

**Transformation:**

```
OpenSearch Response                    API Response
─────────────────────                  ─────────────────────
{                                      {
  "hits": {                              "items": [
    "total": { "value": 45 },              {
    "hits": [                                "id": "abc123",
      {                                      "name": "Nike Air",
        "_source": {                         "price": 89.99,
          "Id": "abc123",                    "brand": "Nike"
          "Name": "Nike Air",              }
          "Price": 89.99,                ],
          "Brand": "Nike",               "totalCount": 45,
          "ImageUrls": ["url1"]          "page": 1,
        }                                "pageSize": 20,
      }                                  "totalPages": 3,
    ]                                    "facets": { ... }
  },                                   }
  "aggregations": { ... }
}
```

**Key Transformations:**

| OpenSearch Field | API Field | Notes |
|------------------|-----------|-------|
| `_source.Id` | `id` | Direct mapping |
| `_source.ImageUrls[0]` | `imageUrl` | Takes first image |
| `hits.total.value` | `totalCount` | Total matching products |
| Calculated | `totalPages` | `Math.Ceiling(total / pageSize)` |

---

### 6. MapFacets() - Converting Aggregation Results

```csharp
private SearchFacets MapFacets(AggregateDictionary aggregations)
```

**Purpose:** Converts OpenSearch aggregation results to facet DTOs.

**Example Transformation:**

```
OpenSearch Aggregations                API Facets
─────────────────────────              ─────────────────────
{                                      {
  "brands": {                            "brands": [
    "buckets": [                           { "key": "Nike", "count": 45 },
      { "key": "Nike", "doc_count": 45 },  { "key": "Adidas", "count": 32 }
      { "key": "Adidas", "doc_count": 32 } ],
    ]                                    "priceRanges": [
  },                                       { "key": "under_25", "count": 12 },
  "price_ranges": {                        { "key": "25_to_50", "count": 28 }
    "buckets": [                         ]
      { "key": "under_25", "doc_count": 12 },
      { "key": "25_to_50", "doc_count": 28 }
    ]
  }
}
```

---

## Complete Example

### Request

```http
GET /api/search/products?q=running+shoes&brand=nike&minPrice=50&maxPrice=150&sortBy=rating&page=1&pageSize=10
Headers:
  X-Tenant-Id: store-123
```

### SearchProductsQuery Object

```csharp
{
    TenantId = "store-123",
    SearchTerm = "running shoes",
    Brand = "nike",
    MinPrice = 50,
    MaxPrice = 150,
    SortBy = "rating",
    SortDirection = "desc",
    Page = 1,
    PageSize = 10
}
```

### Generated OpenSearch Query

```json
{
  "from": 0,
  "size": 10,
  "query": {
    "bool": {
      "must": [
        { "term": { "isActive": true } },
        {
          "multi_match": {
            "query": "running shoes",
            "fields": ["name^3", "name.autocomplete^2", "description", "brand^2", "category", "tags"],
            "type": "best_fields",
            "fuzziness": "AUTO"
          }
        },
        { "term": { "brandSlug": "nike" } },
        { "range": { "price": { "gte": 50, "lte": 150 } } }
      ]
    }
  },
  "sort": [
    { "ratingAverage": { "order": "desc" } }
  ],
  "aggs": {
    "brands": { "terms": { "field": "brand.keyword", "size": 50 } },
    "categories": { "terms": { "field": "category.keyword", "size": 50 } },
    "price_ranges": {
      "range": {
        "field": "price",
        "ranges": [
          { "key": "under_25", "to": 25 },
          { "key": "25_to_50", "from": 25, "to": 50 },
          { "key": "50_to_100", "from": 50, "to": 100 },
          { "key": "100_to_200", "from": 100, "to": 200 },
          { "key": "over_200", "from": 200 }
        ]
      }
    }
  }
}
```

### API Response

```json
{
  "items": [
    {
      "id": "prod-001",
      "sku": "NIKE-RUN-001",
      "name": "Nike Air Zoom Pegasus 40",
      "description": "Responsive cushioning for everyday runs",
      "brand": "Nike",
      "brandSlug": "nike",
      "category": "Running Shoes",
      "categorySlug": "running-shoes",
      "price": 129.99,
      "compareAtPrice": 149.99,
      "discountPercentage": 13.33,
      "currency": "USD",
      "imageUrl": "https://example.com/pegasus.jpg",
      "thumbnailUrl": "https://example.com/pegasus-thumb.jpg",
      "ratingAverage": 4.8,
      "ratingCount": 1250,
      "isDeal": true,
      "isClearance": false,
      "isNewArrival": false,
      "isBestSeller": true
    },
    {
      "id": "prod-002",
      "name": "Nike Free Run 5.0",
      "price": 99.99,
      "ratingAverage": 4.6
      // ... more fields
    }
  ],
  "totalCount": 23,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3,
  "facets": {
    "brands": [
      { "key": "Nike", "count": 23 }
    ],
    "categories": [
      { "key": "Running Shoes", "count": 15 },
      { "key": "Training Shoes", "count": 8 }
    ],
    "departments": [
      { "key": "Footwear", "count": 23 }
    ],
    "priceRanges": [
      { "key": "50_to_100", "count": 12 },
      { "key": "100_to_200", "count": 11 }
    ],
    "ratings": [
      { "key": "4_and_up", "count": 20 },
      { "key": "3_and_up", "count": 23 }
    ]
  }
}
```

---

## Filter Reference

### Available Query Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `q` | string | Full-text search term | `q=nike shoes` |
| `category` | string | Category slug filter | `category=running-shoes` |
| `brand` | string | Brand slug filter | `brand=nike` |
| `department` | string | Department slug filter | `department=footwear` |
| `minPrice` | decimal | Minimum price | `minPrice=50` |
| `maxPrice` | decimal | Maximum price | `maxPrice=200` |
| `minRating` | decimal | Minimum rating (1-5) | `minRating=4` |
| `tags` | string | Comma-separated tags | `tags=running,athletic` |
| `dealsOnly` | bool | Only products on deal | `dealsOnly=true` |
| `clearanceOnly` | bool | Only clearance items | `clearanceOnly=true` |
| `newArrivalsOnly` | bool | Only new arrivals | `newArrivalsOnly=true` |
| `bestSellersOnly` | bool | Only best sellers | `bestSellersOnly=true` |
| `sortBy` | string | Sort field | `sortBy=price` |
| `sortDirection` | string | Sort order (asc/desc) | `sortDirection=asc` |
| `page` | int | Page number (1-based) | `page=2` |
| `pageSize` | int | Results per page (max 100) | `pageSize=20` |

---

## Error Handling

If OpenSearch query fails, the handler returns an empty response:

```csharp
if (!searchResponse.IsValid)
{
    _logger.LogError("Search failed: {Error}", searchResponse.DebugInformation);
    return new SearchProductsResponse
    {
        Items = new List<ProductSearchItem>(),
        TotalCount = 0,
        Page = request.Page,
        PageSize = request.PageSize
    };
}
```

This prevents 500 errors and allows the UI to show "No results found" gracefully.

---

## Multi-Tenancy

Each tenant has a separate index:

```
store-123-products  → Products for Store 123
store-456-products  → Products for Store 456
default-tenant-products → Default store
```

The handler resolves the index name using:
```csharp
var tenantId = request.TenantId ?? "default-tenant";
var indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products);
// Result: "store-123-products"
```

---

## Performance Considerations

1. **Pagination:** Uses `from` and `size` to avoid loading all results
2. **Field Selection:** Only returns needed fields in response
3. **Aggregation Size:** Limited to 50 brands/categories to prevent memory issues
4. **Fuzzy Search:** Uses `AUTO` fuzziness for typo tolerance without performance hit

---

## Related Files

| File | Purpose |
|------|---------|
| `SearchProductsQuery.cs` | Request DTO with all filter parameters |
| `SearchProductsResponse.cs` | Response DTO with items and facets |
| `ProductSearchDocument.cs` | OpenSearch document model |
| `SearchController.cs` | API endpoint that sends query via MediatR |
| `IndexManager.cs` | Manages index names and creation |

---

**Document Version:** 1.0
**Last Updated:** 2026-01-08
**Author:** Claude Code
