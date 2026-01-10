# Search Service - Sequence Diagrams

This document contains Mermaid sequence diagrams illustrating the key flows in the Search Service.

## Table of Contents

1. [Event-Driven Synchronization Flow](#1-event-driven-synchronization-flow)
2. [Product Search Flow](#2-product-search-flow)
3. [Index Creation Flow](#3-index-creation-flow)
4. [Product Indexing Flow](#4-product-indexing-flow)
5. [Autocomplete Flow](#5-autocomplete-flow)

---

## 1. Event-Driven Synchronization Flow

This diagram shows how product changes in the Catalog Service are synchronized to the Search Service.

```mermaid
sequenceDiagram
    autonumber
    participant CS as Catalog Service
    participant SNS as AWS SNS<br/>(catalog-events-topic)
    participant SQS as AWS SQS<br/>(search-catalog-events-queue)
    participant CEC as CatalogEventConsumer<br/>(BackgroundService)
    participant CEH as CatalogEventHandler
    participant PPM as ProductPayloadMapper
    participant PIS as ProductIndexService
    participant IM as IndexManager
    participant OS as OpenSearch

    Note over CS,OS: Product Created/Updated/Deleted in Catalog

    CS->>SNS: Publish CatalogEvent<br/>(ProductCreated/Updated/Deleted)
    SNS->>SQS: Forward message<br/>(SNS wrapper)

    loop Continuous Polling
        CEC->>SQS: ReceiveMessageAsync<br/>(long-poll 20s, max 10 msgs)
        SQS-->>CEC: SQS Message(s)

        alt Has Messages
            CEC->>CEC: Parse SNS wrapper
            CEC->>CEC: Deserialize CatalogEvent
            CEC->>CEH: HandleAsync(CatalogEvent)

            alt ProductCreated
                CEH->>PPM: ToSearchDocument(payload)
                PPM-->>CEH: ProductSearchDocument
                CEH->>PIS: IndexProductAsync(document)
                PIS->>IM: EnsureProductIndexExistsAsync(tenantId)
                IM->>OS: Check/Create Index
                PIS->>OS: IndexAsync(document)
                OS-->>PIS: Success
                PIS-->>CEH: true
            else ProductUpdated
                CEH->>PPM: ToSearchDocument(payload)
                PPM-->>CEH: ProductSearchDocument
                CEH->>PIS: UpdateProductAsync(document)
                PIS->>OS: IndexAsync(document) [upsert]
                OS-->>PIS: Success
                PIS-->>CEH: true
            else ProductDeleted
                CEH->>PIS: DeleteProductAsync(productId, tenantId)
                PIS->>OS: DeleteAsync(productId)
                OS-->>PIS: Success
                PIS-->>CEH: true
            end

            CEH-->>CEC: true (success)
            CEC->>SQS: DeleteMessageAsync<br/>(acknowledge)
        else No Messages
            Note over CEC: Wait and retry
        end
    end
```

---

## 2. Product Search Flow

This diagram shows how a search request is processed from the API to OpenSearch.

```mermaid
sequenceDiagram
    autonumber
    participant Client as Client
    participant SC as SearchController
    participant TC as TenantContext
    participant M as MediatR
    participant SPH as SearchProductsQueryHandler
    participant OCF as OpenSearchClientFactory
    participant IM as IndexManager
    participant OS as OpenSearch

    Client->>SC: GET /api/search/products<br/>?query=cycling&brand=GearPro<br/>&minPrice=50&maxPrice=200

    SC->>TC: Get TenantId
    TC-->>SC: "default-tenant"

    SC->>SC: Build SearchProductsQuery<br/>(with all filters)
    SC->>M: Send(SearchProductsQuery)
    M->>SPH: Handle(query)

    SPH->>IM: GetIndexName(tenantId)
    IM-->>SPH: "default-tenant-products"

    SPH->>SPH: BuildQuery()<br/>- Must: IsActive=true<br/>- Multi-match: query text<br/>- Filter: brand, price range

    SPH->>SPH: BuildSort()<br/>(relevance/price/name/rating)

    SPH->>SPH: BuildAggregations()<br/>- brands (50)<br/>- categories (50)<br/>- departments (20)<br/>- price_ranges<br/>- ratings

    SPH->>OCF: GetClient()
    OCF-->>SPH: IElasticClient

    SPH->>OS: SearchAsync<ProductSearchDocument><br/>(index, query, sort, aggs, pagination)

    OS-->>SPH: SearchResponse<br/>(hits, aggregations, total)

    SPH->>SPH: MapToResponse()<br/>- Map documents to DTOs<br/>- Extract facet buckets

    SPH-->>M: SearchProductsResponse
    M-->>SC: SearchProductsResponse

    SC-->>Client: 200 OK<br/>{items, facets, totalCount, pagination}
```

---

## 3. Index Creation Flow

This diagram shows how a product index is created via the Admin API.

```mermaid
sequenceDiagram
    autonumber
    participant Client as Admin Client
    participant AC as AdminController
    participant IM as IndexManager
    participant OCF as OpenSearchClientFactory
    participant OS as OpenSearch

    Client->>AC: POST /api/admin/index/{tenantId}/products

    AC->>IM: CreateProductIndexAsync(tenantId)

    IM->>IM: GetIndexName(tenantId)<br/>"default-tenant-products"

    IM->>IM: IndexExistsAsync(tenantId)
    IM->>OCF: GetClient()
    OCF-->>IM: IElasticClient
    IM->>OS: Indices.ExistsAsync(indexName)
    OS-->>IM: ExistsResponse

    alt Index Already Exists
        IM-->>AC: true
        AC-->>Client: 200 OK<br/>{"message": "Index already exists"}
    else Index Does Not Exist
        IM->>IM: Check IsLocalStack config

        alt LocalStack Environment
            IM->>IM: CreateLocalStackIndexAsync()
            Note over IM: Settings:<br/>- 1 shard<br/>- 0 replicas<br/>- No custom analyzers
        else Production Environment
            IM->>IM: CreateProductionIndexAsync()
            Note over IM: Settings:<br/>- 2 shards<br/>- 1 replica<br/>- product_name_analyzer<br/>- autocomplete_analyzer
        end

        IM->>IM: ConfigureProductProperties()<br/>(shared mapping)

        IM->>OS: Indices.CreateAsync(indexName, settings, mappings)
        OS-->>IM: CreateIndexResponse

        alt Creation Success
            IM->>IM: Add to _ensuredIndexes cache
            IM-->>AC: true
            AC-->>Client: 200 OK<br/>{"message": "Index created successfully"}
        else Creation Failed
            IM-->>AC: false
            AC-->>Client: 500 Error<br/>{"error": "Failed to create index"}
        end
    end
```

---

## 4. Product Indexing Flow

This diagram shows the detailed flow when indexing a single product.

```mermaid
sequenceDiagram
    autonumber
    participant CEH as CatalogEventHandler
    participant PIS as ProductIndexService
    participant IM as IndexManager
    participant OCF as OpenSearchClientFactory
    participant OS as OpenSearch

    CEH->>PIS: IndexProductAsync(ProductSearchDocument)

    PIS->>PIS: Extract tenantId from document

    PIS->>IM: EnsureProductIndexExistsAsync(tenantId)

    IM->>IM: Check _ensuredIndexes cache

    alt Not in Cache
        IM->>IM: IndexExistsAsync(tenantId)
        IM->>OS: Indices.ExistsAsync
        OS-->>IM: ExistsResponse

        alt Index Does Not Exist
            IM->>IM: CreateProductIndexAsync(tenantId)
            IM->>OS: Indices.CreateAsync(settings, mappings)
            OS-->>IM: CreateIndexResponse
        end

        IM->>IM: Add to _ensuredIndexes cache
    end

    IM-->>PIS: (index ensured)

    PIS->>IM: GetIndexName(tenantId)
    IM-->>PIS: "default-tenant-products"

    PIS->>OCF: GetClient()
    OCF-->>PIS: IElasticClient

    PIS->>OS: IndexAsync(document, indexName)<br/>[creates or updates]

    alt Success
        OS-->>PIS: IndexResponse (valid)
        PIS-->>CEH: true
    else Failure
        OS-->>PIS: IndexResponse (invalid)
        PIS->>PIS: Log error with details
        PIS-->>CEH: false
    end
```

---

## 5. Autocomplete Flow

This diagram shows the autocomplete suggestion flow with brands, categories, and products.

```mermaid
sequenceDiagram
    autonumber
    participant Client as Client
    participant SC as SearchController
    participant TC as TenantContext
    participant M as MediatR
    participant AQH as AutocompleteQueryHandler
    participant IM as IndexManager
    participant OS as OpenSearch

    Client->>SC: GET /api/search/autocomplete<br/>?prefix=sg&limit=10

    SC->>TC: Get TenantId
    TC-->>SC: "default"

    SC->>SC: Build AutocompleteQuery

    alt Prefix < 2 characters
        SC-->>Client: 200 OK<br/>{"suggestions": []}
    end

    SC->>M: Send(AutocompleteQuery)
    M->>AQH: Handle(query)

    AQH->>IM: GetIndexName(tenantId)
    IM-->>AQH: "default-products"

    Note over AQH,OS: Step 1: Get Brand Suggestions

    AQH->>OS: SearchAsync<br/>- Size: 0<br/>- Match: brand.autocomplete<br/>- Aggs: brands (terms)
    OS-->>AQH: Brand aggregation buckets

    AQH->>AQH: Filter brands starting with prefix<br/>Add to suggestions (type: "brand")

    Note over AQH,OS: Step 2: Get Category Suggestions

    AQH->>OS: SearchAsync<br/>- Size: 0<br/>- Aggs: categories (terms)
    OS-->>AQH: Category aggregation buckets

    AQH->>AQH: Filter categories containing prefix<br/>Add to suggestions (type: "category")

    Note over AQH,OS: Step 3: Get Product Suggestions

    AQH->>OS: SearchAsync<br/>- Size: 10<br/>- MultiMatch: name.autocomplete, brand.autocomplete<br/>- Type: bool_prefix
    OS-->>AQH: Product hits

    AQH->>AQH: Map to suggestions (type: "product")

    Note over AQH: Step 4: Combine & Deduplicate

    AQH->>AQH: DistinctBy(text, type)<br/>Take(limit)

    AQH-->>M: AutocompleteResponse
    M-->>SC: AutocompleteResponse

    SC-->>Client: 200 OK<br/>{"suggestions": [<br/>  {"text": "SG", "type": "brand"},<br/>  {"text": "Shoes", "type": "category"},<br/>  {"text": "SG Test Batting Gloves", "type": "product"}<br/>]}
```

### Autocomplete Priority Order

| Priority | Type | Max Results | Match Strategy |
|----------|------|-------------|----------------|
| 1 | Brand | 3 | Prefix match (StartsWith) |
| 2 | Category | 3 | Contains match |
| 3 | Product | 10 | Edge n-gram on name & brand |

### Edge N-Gram Tokenization Example

```
Indexed: "Kookaburra" → ["k", "ko", "koo", "kook", "kooka", ...]
Query:   "kook"       → matches token "kook" ✓
```

---

## 6. Bulk Indexing Flow

This diagram shows the bulk indexing operation for multiple products.

```mermaid
sequenceDiagram
    autonumber
    participant Caller as Caller
    participant PIS as ProductIndexService
    participant IM as IndexManager
    participant OCF as OpenSearchClientFactory
    participant OS as OpenSearch

    Caller->>PIS: BulkIndexProductsAsync(products[])

    PIS->>PIS: Validate batch size <= 1000

    PIS->>PIS: Group products by TenantId

    loop For Each Tenant
        PIS->>IM: EnsureProductIndexExistsAsync(tenantId)
        IM-->>PIS: (index ensured)
    end

    PIS->>OCF: GetClient()
    OCF-->>PIS: IElasticClient

    PIS->>OS: BulkAsync(descriptor)<br/>- IndexMany for each tenant group

    OS-->>PIS: BulkResponse

    PIS->>PIS: Count successes/failures
    PIS->>PIS: Collect error messages

    alt All Succeeded
        PIS-->>Caller: BulkIndexResult<br/>{Success: true, SuccessCount: N}
    else Some Failed
        PIS-->>Caller: BulkIndexResult<br/>{Success: false, Errors: [...]}
    end
```

---

## 7. Delete Product Flow

This diagram shows the product deletion flow.

```mermaid
sequenceDiagram
    autonumber
    participant CEH as CatalogEventHandler
    participant PIS as ProductIndexService
    participant IM as IndexManager
    participant OCF as OpenSearchClientFactory
    participant OS as OpenSearch

    CEH->>PIS: DeleteProductAsync(productId, tenantId)

    PIS->>IM: GetIndexName(tenantId)
    IM-->>PIS: "default-tenant-products"

    PIS->>OCF: GetClient()
    OCF-->>PIS: IElasticClient

    PIS->>OS: DeleteAsync(productId, indexName)

    alt Document Exists
        OS-->>PIS: DeleteResponse (valid)
        PIS-->>CEH: true
    else Document Not Found
        OS-->>PIS: DeleteResponse (not found)
        Note over PIS: Treat as success<br/>(idempotent)
        PIS-->>CEH: true
    else Error
        OS-->>PIS: DeleteResponse (error)
        PIS->>PIS: Log error
        PIS-->>CEH: false
    end
```

---

## Message Flow Legend

| Symbol | Meaning |
|--------|---------|
| `->>`  | Synchronous request |
| `-->>` | Response |
| `alt`  | Alternative paths |
| `loop` | Repeated operation |
| `Note` | Additional context |

---

## Related Documentation

- [Autocomplete](./AUTOCOMPLETE.md) - Detailed autocomplete feature documentation
- [Class Responsibilities](./CLASS-RESPONSIBILITIES.md) - Detailed class documentation
- [Architecture](./ARCHITECTURE.md) - System architecture overview
- [Implementation Plan](./IMPLEMENTATION-PLAN.md) - Development guide
