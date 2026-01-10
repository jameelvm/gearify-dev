# Search Service - Class Responsibilities

This document provides detailed documentation of each class in the Search Service, organized by architectural layer.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [API Layer](#api-layer)
3. [Application Layer](#application-layer)
4. [Domain Layer](#domain-layer)
5. [Infrastructure Layer](#infrastructure-layer)
6. [Dependency Graph](#dependency-graph)

---

## Architecture Overview

The Search Service follows **Clean Architecture** with four distinct layers:

```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer                             │
│  Controllers, HTTP endpoints, request/response handling      │
├─────────────────────────────────────────────────────────────┤
│                    Application Layer                         │
│  Query handlers, event handlers, mappers, DTOs               │
├─────────────────────────────────────────────────────────────┤
│                      Domain Layer                            │
│  Entities, events, business logic                            │
├─────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                       │
│  OpenSearch, SQS, configuration, external services           │
└─────────────────────────────────────────────────────────────┘
```

---

## API Layer

### SearchController

**File**: `API/Controllers/SearchController.cs`

**Responsibility**: HTTP endpoint for product search operations.

| Property | Value |
|----------|-------|
| Route | `/api/search` |
| Dependencies | `IMediator`, `ITenantContext`, `ILogger<SearchController>` |

**Endpoints**:

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/products` | Full-text search with filters and facets |
| GET | `/autocomplete` | Autocomplete suggestions (brands, categories, products) |

**Search Parameters (`/products`)**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `query` | string | Search term |
| `category` | string | Category slug filter |
| `brand` | string | Brand slug filter |
| `department` | string | Department slug filter |
| `minPrice` | decimal? | Minimum price filter |
| `maxPrice` | decimal? | Maximum price filter |
| `minRating` | decimal? | Minimum rating filter |
| `tags` | string[] | Tag filters |
| `dealsOnly` | bool | Only show deals |
| `clearanceOnly` | bool | Only show clearance items |
| `newArrivalsOnly` | bool | Only show new arrivals |
| `bestSellersOnly` | bool | Only show best sellers |
| `sortBy` | string | Sort field (relevance/price/name/rating/newest/popularity) |
| `sortOrder` | string | asc/desc |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (max: 100) |

**Autocomplete Parameters (`/autocomplete`)**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `prefix` | string | Search prefix (minimum 2 characters) |
| `limit` | int | Max suggestions to return (default: 10, max: 20) |

**Autocomplete Response**:

```json
{
  "suggestions": [
    {"text": "SG", "type": "brand", "id": null, "slug": "sg"},
    {"text": "Gloves", "type": "category", "id": null, "slug": "gloves"},
    {"text": "SG Batting Gloves", "type": "product", "id": "abc-123", "slug": null}
  ]
}
```

---

### AdminController

**File**: `API/Controllers/AdminController.cs`

**Responsibility**: Administrative operations for index management and testing.

| Property | Value |
|----------|-------|
| Route | `/api/admin` |
| Dependencies | `IIndexManager`, `IProductIndexService`, `ILogger<AdminController>` |

**Endpoints**:

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/index/{tenantId}/products` | Create product index |
| GET | `/index/{tenantId}/products/exists` | Check if index exists |
| DELETE | `/index/{tenantId}/products` | Delete product index |
| POST | `/products/test` | Index a test product |
| DELETE | `/products/{productId}` | Delete a product from index |

---

## Application Layer

### Queries

#### SearchProductsQuery

**File**: `Application/Queries/SearchProductsQuery.cs`

**Responsibility**: MediatR request object containing search criteria.

**Type**: `record` implementing `IRequest<SearchProductsResponse>`

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `TenantId` | string | Tenant identifier |
| `SearchTerm` | string? | Full-text search query |
| `Category` | string? | Category filter |
| `Brand` | string? | Brand filter |
| `Department` | string? | Department filter |
| `MinPrice` | decimal? | Minimum price |
| `MaxPrice` | decimal? | Maximum price |
| `MinRating` | decimal? | Minimum rating |
| `Tags` | List\<string\>? | Tag filters |
| `DealsOnly` | bool | Deals filter |
| `ClearanceOnly` | bool | Clearance filter |
| `NewArrivalsOnly` | bool | New arrivals filter |
| `BestSellersOnly` | bool | Best sellers filter |
| `Page` | int | Page number |
| `PageSize` | int | Items per page |
| `SortBy` | string? | Sort field |
| `SortDirection` | string? | Sort direction |

---

#### SearchProductsQueryHandler

**File**: `Application/Queries/SearchProductsQueryHandler.cs`

**Responsibility**: Executes search queries against OpenSearch.

**Type**: `class` implementing `IRequestHandler<SearchProductsQuery, SearchProductsResponse>`

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IOpenSearchClientFactory` | Get OpenSearch client |
| `IIndexManager` | Get index name |
| `ILogger<SearchProductsQueryHandler>` | Logging |

**Key Methods**:

| Method | Description |
|--------|-------------|
| `Handle()` | Main handler - builds and executes query |
| `BuildQuery()` | Constructs OpenSearch query with all filters |
| `BuildSort()` | Creates sort descriptor |
| `BuildAggregations()` | Adds facet aggregations |
| `MapToResponse()` | Converts OpenSearch response to DTO |

**Query Building Details**:

- **Must Conditions**: `IsActive=true`, multi-match search, all filters
- **Multi-Match Fields**: Name (boost 3), name.autocomplete (boost 2), Description, Brand (boost 2), Category, Tags
- **Fuzziness**: Auto
- **Aggregations**: brands (50), categories (50), departments (20), price_ranges, ratings

---

#### AutocompleteQuery

**File**: `Application/Queries/AutocompleteQuery.cs`

**Responsibility**: MediatR request object for autocomplete suggestions.

**Type**: `class` implementing `IRequest<AutocompleteResponse>`

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `TenantId` | string? | Tenant identifier |
| `Prefix` | string | Search prefix (min 2 chars) |
| `Limit` | int | Max suggestions (default: 10, max: 20) |

---

#### AutocompleteQueryHandler

**File**: `Application/Queries/AutocompleteQueryHandler.cs`

**Responsibility**: Executes autocomplete queries returning brands, categories, and products.

**Type**: `class` implementing `IRequestHandler<AutocompleteQuery, AutocompleteResponse>`

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IOpenSearchClientFactory` | Get OpenSearch client |
| `IIndexManager` | Get index name |
| `ILogger<AutocompleteQueryHandler>` | Logging |

**Key Methods**:

| Method | Description |
|--------|-------------|
| `Handle()` | Main handler - orchestrates suggestion fetching |
| `GetBrandSuggestionsAsync()` | Fetches matching brands via aggregations |
| `GetCategorySuggestionsAsync()` | Fetches matching categories via aggregations |
| `GetProductSuggestionsAsync()` | Fetches matching products via multi-match |

**Suggestion Priority**:

| Priority | Type | Max | Match Strategy |
|----------|------|-----|----------------|
| 1 | Brand | 3 | StartsWith (prefix match) |
| 2 | Category | 3 | Contains (partial match) |
| 3 | Product | 10 | Edge n-gram on name.autocomplete |

**Query Details**:

- **Brand Query**: Uses aggregation on `brand.keyword`, filters StartsWith in memory
- **Category Query**: Uses aggregation on `category.keyword`, filters Contains in memory
- **Product Query**: MultiMatch on `name.autocomplete` (boost 3) and `brand.autocomplete` (boost 2) with `bool_prefix` type

---

### Events

#### ICatalogEventHandler

**File**: `Application/Events/ICatalogEventHandler.cs`

**Responsibility**: Interface for processing catalog events.

**Contract**:
```csharp
Task<bool> HandleAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken);
```

---

#### CatalogEventHandler

**File**: `Application/Events/CatalogEventHandler.cs`

**Responsibility**: Implements event-driven synchronization with catalog service.

**Type**: `class` implementing `ICatalogEventHandler`

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IProductIndexService` | Index operations |
| `ILogger<CatalogEventHandler>` | Logging |

**Event Handling Strategy**:

| Event Type | Handler Method | Action |
|------------|----------------|--------|
| `ProductCreated` | `HandleProductCreatedAsync()` | Index new product |
| `ProductUpdated` | `HandleProductUpdatedAsync()` | Update existing product |
| `ProductDeleted` | `HandleProductDeletedAsync()` | Remove from index |
| Unknown | `HandleUnknownEvent()` | Log and acknowledge |

**Return Value**: `true` = success (delete from queue), `false` = retry later

---

### Services

#### IProductIndexService

**File**: `Application/Services/IProductIndexService.cs`

**Responsibility**: Service contract for indexing operations.

**Methods**:

| Method | Return | Description |
|--------|--------|-------------|
| `IndexProductAsync(ProductSearchDocument)` | `Task<bool>` | Index single product |
| `UpdateProductAsync(ProductSearchDocument)` | `Task<bool>` | Update product (upsert) |
| `DeleteProductAsync(string, string)` | `Task<bool>` | Delete product by ID |
| `BulkIndexProductsAsync(IEnumerable<ProductSearchDocument>)` | `Task<BulkIndexResult>` | Batch index |

---

### Mappers

#### ProductPayloadMapper

**File**: `Application/Mappers/ProductPayloadMapper.cs`

**Responsibility**: Converts catalog event payloads to search documents.

**Type**: `static class`

**Method**:
```csharp
static ProductSearchDocument ToSearchDocument(ProductPayload payload)
```

**Mapping**: All 35 fields from `ProductPayload` → `ProductSearchDocument`

---

### DTOs

#### SearchProductsResponse

**File**: `Application/DTOs/SearchProductsResponse.cs`

**Responsibility**: Response object for search results.

**Structure**:

```
SearchProductsResponse
├── Items: List<ProductSearchItem>
├── Facets: SearchFacets
├── TotalCount: long
├── Page: int
├── PageSize: int
└── TotalPages: int

ProductSearchItem
├── Id, Sku, Name, Description
├── Brand, BrandSlug
├── Department, DepartmentSlug
├── Category, CategorySlug
├── Price, CompareAtPrice, DiscountPercentage, Currency
├── ImageUrl, ThumbnailUrl
├── RatingAverage, RatingCount
└── IsDeal, IsClearance, IsNewArrival, IsBestSeller

SearchFacets
├── Brands: List<FacetItem>
├── Categories: List<FacetItem>
├── Departments: List<FacetItem>
├── PriceRanges: List<FacetItem>
└── Ratings: List<FacetItem>

FacetItem
├── Key: string
└── Count: long
```

---

#### AutocompleteResponse

**File**: `Application/DTOs/AutocompleteResponse.cs`

**Responsibility**: Response object for autocomplete suggestions.

**Structure**:

```
AutocompleteResponse
└── Suggestions: List<AutocompleteSuggestion>

AutocompleteSuggestion
├── Text: string      // Display text (e.g., "SG", "Gloves", "SG Batting Gloves")
├── Type: string      // "brand", "category", or "product"
├── Id: string?       // Product ID (only for type="product")
└── Slug: string?     // URL slug (only for type="brand" or "category")
```

**Example Response**:

```json
{
  "suggestions": [
    {"text": "SG", "type": "brand", "id": null, "slug": "sg"},
    {"text": "Gloves", "type": "category", "id": null, "slug": "gloves"},
    {"text": "SG Batting Gloves", "type": "product", "id": "18e5c43e-...", "slug": null}
  ]
}
```

---

## Domain Layer

### Entities

#### ProductSearchDocument

**File**: `Domain/Entities/ProductSearchDocument.cs`

**Responsibility**: Search index document entity for OpenSearch.

**Properties** (35 fields):

| Category | Properties |
|----------|------------|
| **Identity** | `Id`, `TenantId` |
| **Product Info** | `Sku`, `Name`, `Description` |
| **Classification** | `Brand`, `BrandSlug`, `Department`, `DepartmentSlug`, `Category`, `CategorySlug`, `Subcategory`, `SubcategorySlug` |
| **Pricing** | `Price`, `CompareAtPrice`, `DiscountPercentage`, `Currency` |
| **Ratings** | `RatingAverage`, `RatingCount` |
| **Media** | `ThumbnailUrl`, `ImageUrls[]` |
| **Metadata** | `Tags[]` |
| **Flags** | `IsActive`, `IsDeal`, `IsClearance`, `IsNewArrival`, `IsBestSeller`, `IsFeatured` |
| **Timestamps** | `CreatedAt`, `UpdatedAt` |

---

### Events

#### CatalogEvent

**File**: `Domain/Events/CatalogEvent.cs`

**Responsibility**: Event envelope from Catalog Service.

**Properties**:

| Property | Type | JSON Name |
|----------|------|-----------|
| `EventId` | string | `eventId` |
| `EventType` | string | `eventType` |
| `TenantId` | string | `tenantId` |
| `Timestamp` | DateTime | `timestamp` |
| `Payload` | ProductPayload | `payload` |

---

#### ProductPayload

**File**: `Domain/Events/CatalogEvent.cs` (nested)

**Responsibility**: Product data contained in catalog events.

**Properties**: Same as `ProductSearchDocument` with JSON property name attributes.

---

#### CatalogEventTypes

**File**: `Domain/Events/CatalogEventTypes.cs`

**Responsibility**: Constants for event type identification.

```csharp
public const string ProductCreated = "ProductCreated";
public const string ProductUpdated = "ProductUpdated";
public const string ProductDeleted = "ProductDeleted";
```

---

#### SnsMessageWrapper

**File**: `Domain/Events/SnsMessageWrapper.cs`

**Responsibility**: SNS envelope when messages arrive through SNS→SQS subscription.

**Properties**: `Type`, `MessageId`, `TopicArn`, `Subject`, `Message`, `Timestamp`, `Signature`, etc.

---

## Infrastructure Layer

### OpenSearch

#### IOpenSearchClientFactory

**File**: `Infrastructure/OpenSearch/OpenSearchClientFactory.cs`

**Responsibility**: Factory interface for creating OpenSearch client.

**Method**: `IElasticClient CreateClient()`

---

#### OpenSearchClientFactory

**File**: `Infrastructure/OpenSearch/OpenSearchClientFactory.cs`

**Responsibility**: Creates and caches OpenSearch client instance.

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IOptions<OpenSearchSettings>` | Configuration |
| `ILogger<OpenSearchClientFactory>` | Logging |

**Features**:
- Singleton instance caching
- Connection settings configuration
- Basic auth support (optional)
- Debug mode logging
- 30-second request timeout

---

#### IIndexManager

**File**: `Infrastructure/OpenSearch/IIndexManager.cs`

**Responsibility**: Contract for index lifecycle management.

**Methods**:

| Method | Description |
|--------|-------------|
| `CreateProductIndexAsync(tenantId)` | Create index with mappings |
| `DeleteIndexAsync(tenantId, indexType)` | Delete index |
| `IndexExistsAsync(tenantId, indexType)` | Check existence |
| `EnsureProductIndexExistsAsync(tenantId)` | Idempotent ensure |
| `GetIndexName(tenantId, indexType)` | Generate index name |

---

#### IndexManager

**File**: `Infrastructure/OpenSearch/IndexManager.cs`

**Responsibility**: Manages OpenSearch indices with tenant isolation.

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IOpenSearchClientFactory` | Get client |
| `ILogger<IndexManager>` | Logging |
| `IConfiguration` | Environment check |

**Index Naming**: `{tenantId}-{indexType}` (e.g., `default-tenant-products`)

**Key Methods**:

| Method | Description |
|--------|-------------|
| `CreateProductIndexAsync()` | Delegates to LocalStack or Production |
| `CreateLocalStackIndexAsync()` | 1 shard, 0 replicas, no analyzers |
| `CreateProductionIndexAsync()` | 2 shards, 1 replica, custom analyzers |
| `ConfigureProductProperties()` | Shared field mappings |
| `HandleIndexCreationResponse()` | Common response handling |

**Field Mappings**:

| Field Type | OpenSearch Type | Fields |
|------------|-----------------|--------|
| Keyword | `keyword` | Id, TenantId, Sku, *Slug fields, Currency, URLs, Tags |
| Text | `text` | Name (+ keyword subfield), Description, Brand (+ keyword subfield) |
| Numeric | `double`/`integer` | Price, CompareAtPrice, DiscountPercentage, RatingAverage, RatingCount |
| Boolean | `boolean` | IsActive, IsDeal, IsClearance, IsNewArrival, IsBestSeller, IsFeatured |
| Date | `date` | CreatedAt, UpdatedAt |

---

#### ProductIndexService

**File**: `Infrastructure/OpenSearch/ProductIndexService.cs`

**Responsibility**: Implements indexing operations.

**Type**: `class` implementing `IProductIndexService`

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IOpenSearchClientFactory` | Get client |
| `IIndexManager` | Index management |
| `IOptions<OpenSearchSettings>` | Configuration |
| `ILogger<ProductIndexService>` | Logging |

**Key Features**:
- Auto-creates index if needed
- Batch size limit: 1000 documents
- Groups products by tenant for multi-tenant indexing
- Idempotent delete (not found = success)

---

### Messaging

#### CatalogEventConsumer

**File**: `Infrastructure/Messaging/CatalogEventConsumer.cs`

**Responsibility**: Background service that polls SQS for catalog events.

**Type**: `class` extending `BackgroundService`

**Dependencies**:

| Dependency | Purpose |
|------------|---------|
| `IAmazonSQS` | SQS operations |
| `IServiceProvider` | Scoped service resolution |
| `IOptions<MessagingSettings>` | Configuration |
| `ILogger<CatalogEventConsumer>` | Logging |

**Polling Configuration**:

| Setting | Value |
|---------|-------|
| Long-poll wait time | 20 seconds |
| Max messages per poll | 10 |
| Visibility timeout | 30 seconds |

**Message Processing Flow**:
1. Receive messages from SQS
2. Parse SNS wrapper (or direct JSON)
3. Create scoped `ICatalogEventHandler`
4. Call handler
5. On success: delete message
6. On failure: leave for retry

---

### Configuration

#### OpenSearchSettings

**File**: `Infrastructure/Configuration/OpenSearchSettings.cs`

```csharp
public class OpenSearchSettings
{
    public string Endpoint { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
```

---

#### IndexNames

**File**: `Infrastructure/Configuration/OpenSearchSettings.cs`

```csharp
public static class IndexNames
{
    public const string Products = "products";
    public const string Categories = "categories";
    public const string Brands = "brands";

    public static string GetIndexName(string tenantId, string indexType)
        => $"{tenantId}-{indexType}".ToLowerInvariant();
}
```

---

#### MessagingSettings

**File**: `Infrastructure/Configuration/MessagingSettings.cs`

```csharp
public class MessagingSettings
{
    public SqsSettings SQS { get; set; }
}

public class SqsSettings
{
    public string CatalogEventsQueueUrl { get; set; }
    public int MaxNumberOfMessages { get; set; } = 10;
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSeconds { get; set; } = 30;
}
```

---

## Dependency Graph

```
┌──────────────────────────────────────────────────────────────────┐
│                         API Layer                                 │
│  ┌─────────────────┐     ┌─────────────────┐                     │
│  │ SearchController │     │ AdminController  │                     │
│  └────────┬────────┘     └────────┬────────┘                     │
│           │                       │                               │
├───────────┼───────────────────────┼───────────────────────────────┤
│           │   Application Layer   │                               │
│           ▼                       ▼                               │
│  ┌─────────────────┐     ┌─────────────────┐                     │
│  │ SearchProducts  │     │ IProductIndex   │                     │
│  │ QueryHandler    │     │ Service         │◄────────────────┐   │
│  └────────┬────────┘     └─────────────────┘                 │   │
│           │                       ▲                           │   │
│           │              ┌────────┴────────┐                 │   │
│           │              │ CatalogEvent    │                 │   │
│           │              │ Handler         │                 │   │
│           │              └────────┬────────┘                 │   │
│           │                       │                           │   │
│           │              ┌────────┴────────┐                 │   │
│           │              │ ProductPayload  │                 │   │
│           │              │ Mapper          │                 │   │
│           │              └─────────────────┘                 │   │
│           │                                                   │   │
├───────────┼───────────────────────────────────────────────────┼───┤
│           │         Domain Layer                              │   │
│           │  ┌─────────────────┐  ┌─────────────────┐        │   │
│           │  │ ProductSearch   │  │ CatalogEvent    │        │   │
│           │  │ Document        │  │ (+ Payload)     │        │   │
│           │  └─────────────────┘  └─────────────────┘        │   │
│           │                                                   │   │
├───────────┼───────────────────────────────────────────────────┼───┤
│           │      Infrastructure Layer                         │   │
│           ▼                                                   │   │
│  ┌─────────────────┐     ┌─────────────────┐                 │   │
│  │ OpenSearchClient│     │ IndexManager    │                 │   │
│  │ Factory         │◄────┤                 │                 │   │
│  └────────┬────────┘     └────────┬────────┘                 │   │
│           │                       │                           │   │
│           ▼                       ▼                           │   │
│  ┌─────────────────┐     ┌─────────────────┐                 │   │
│  │ IElasticClient  │     │ ProductIndex    │─────────────────┘   │
│  │ (NEST)          │◄────┤ Service         │                     │
│  └────────┬────────┘     └─────────────────┘                     │
│           │                       ▲                               │
│           ▼                       │                               │
│  ┌─────────────────┐     ┌────────┴────────┐                     │
│  │   OpenSearch    │     │ CatalogEvent    │                     │
│  │   (External)    │     │ Consumer        │◄──── AWS SQS        │
│  └─────────────────┘     └─────────────────┘                     │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## Interface Summary

| Interface | Implementation | Layer |
|-----------|----------------|-------|
| `IOpenSearchClientFactory` | `OpenSearchClientFactory` | Infrastructure |
| `IIndexManager` | `IndexManager` | Infrastructure |
| `IProductIndexService` | `ProductIndexService` | Infrastructure |
| `ICatalogEventHandler` | `CatalogEventHandler` | Application |
| `IRequestHandler<SearchProductsQuery, ...>` | `SearchProductsQueryHandler` | Application |

---

## Related Documentation

- [Sequence Diagrams](./SEQUENCE-DIAGRAMS.md) - Visual flow diagrams
- [Architecture](./ARCHITECTURE.md) - System architecture overview
- [Implementation Plan](./IMPLEMENTATION-PLAN.md) - Development guide
