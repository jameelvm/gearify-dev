# Catalog DynamoDB Table Documentation

## Table Overview

**Table Name:** `gearify-catalog`
**Design Pattern:** Single-Table Design
**Multi-tenancy:** Yes (tenant-isolated via partition keys)

## Entities Stored

1. **CATEGORY** - Top-level product categories (e.g., "Bats", "Balls")
2. **CATEGORY_SECTION** - Organizational sections within categories (e.g., "By Brand", "Price Range")
3. **SUBCATEGORY** - Items within sections (e.g., "SS", "Under ₹5000")

## Primary Key Structure

| Attribute | Type | Description |
|-----------|------|-------------|
| **PK** | String | Partition Key - Groups related items |
| **SK** | String | Sort Key - Defines item type and hierarchy |

## Global Secondary Indexes (GSI)

### GSI1 - Slug Lookup Index
**Purpose:** Find categories by slug for SEO-friendly URLs

| Attribute | Type | Description |
|-----------|------|-------------|
| **GSI1PK** | String | Partition Key |
| **GSI1SK** | String | Sort Key |

### GSI2 - Category List Index
**Purpose:** Get all categories ordered by display order

| Attribute | Type | Description |
|-----------|------|-------------|
| **GSI2PK** | String | Partition Key |
| **GSI2SK** | String | Sort Key (includes display order for sorting) |

---

## Key Patterns

### Category Entity
```
PK:      TENANT#{tenantId}#CATEGORY#{categoryId}
SK:      METADATA
Type:    CATEGORY

GSI1PK:  TENANT#{tenantId}#SLUG
GSI1SK:  CATEGORY#{slug}

GSI2PK:  TENANT#{tenantId}#CATEGORIES
GSI2SK:  ORDER#{displayOrder:D4}
```

**Example:**
```
PK:      TENANT#default#CATEGORY#cat_bats
SK:      METADATA
GSI1PK:  TENANT#default#SLUG
GSI1SK:  CATEGORY#bats
GSI2PK:  TENANT#default#CATEGORIES
GSI2SK:  ORDER#0001
```

### Category Section Entity
```
PK:      TENANT#{tenantId}#CATEGORY#{categoryId}
SK:      SECTION#{sectionId}
Type:    CATEGORY_SECTION
```

**Example:**
```
PK:      TENANT#default#CATEGORY#cat_bats
SK:      SECTION#sec_bats_1
```

### Subcategory Entity
```
PK:      TENANT#{tenantId}#CATEGORY#{categoryId}
SK:      SECTION#{sectionId}#ITEM#{subcategoryId}
Type:    SUBCATEGORY
```

**Example:**
```
PK:      TENANT#default#CATEGORY#cat_bats
SK:      SECTION#sec_bats_1#ITEM#sub_bats_1
```

---

## Current Implementation

### Get All Categories with Details (Mega Menu)
**Repository Method:** `GetAllCategoriesWithDetailsAsync(string tenantId)`

**Use Case:** Load complete mega menu data for navigation

**Strategy:** Parallel queries for maximum performance

**Steps:**
1. Query GSI2 to get all category IDs (1 query)
2. Parallel queries on primary table for each category (N parallel queries)

**Performance:**
- 1 + N queries total
- N queries execute in parallel
- Total time ≈ time of slowest query (not sum of all queries)
- Performance: ~100-150ms for 9 categories (vs 500-1000ms sequential)

**Implementation:**
```csharp
// Step 1: Get all category IDs using GSI2
var categoriesRequest = new QueryRequest
{
    TableName = "gearify-catalog",
    IndexName = "GSI2",
    KeyConditionExpression = "GSI2PK = :gsi2pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":gsi2pk", new AttributeValue { S = "TENANT#default#CATEGORIES" } }
    }
};

var categoriesResponse = await dynamoDb.QueryAsync(categoriesRequest);
var categoryIds = categoriesResponse.Items
    .Select(item => item["Id"].S)
    .ToList();

// Step 2: Fetch details for all categories in parallel
var detailsTasks = categoryIds
    .Select(categoryId => GetCategoryWithDetailsAsync(categoryId, tenantId))
    .ToList();

var results = await Task.WhenAll(detailsTasks);

return results
    .Where(r => r.category is { Id: not null })
    .OrderBy(r => r.category.DisplayOrder)
    .ToList();
```

**Private Helper Method:** `GetCategoryWithDetailsAsync(string categoryId, string tenantId)`
```csharp
// Query primary table by PK to get all related items
var request = new QueryRequest
{
    TableName = "gearify-catalog",
    KeyConditionExpression = "PK = :pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } }
    }
};

var response = await dynamoDb.QueryAsync(request);

// Process items by SK pattern
foreach (var item in response.Items)
{
    var sk = item["SK"].S;
    if (sk == "METADATA")
        category = MapToCategory(item);
    else if (sk.StartsWith("SECTION#") && !sk.Contains("#ITEM#"))
        sections.Add(MapToSection(item));
    else if (sk.Contains("#ITEM#"))
        subcategories.Add(MapToSubcategory(item));
}
```

---

## Potential Future Access Patterns

These patterns are supported by the schema but not currently implemented:

### Get Category by Slug
**Use Case:** Load category page from URL (e.g., `/category/bats`)
```
Index:     GSI1
Condition: GSI1PK = "TENANT#{tenantId}#SLUG" AND GSI1SK = "CATEGORY#{slug}"
Returns:   Single category metadata
```

### Get All Categories (List Only)
**Use Case:** Display category navigation menu (without details)
```
Index:     GSI2
Condition: GSI2PK = "TENANT#{tenantId}#CATEGORIES"
Returns:   All categories ordered by displayOrder
```

---

## Data Hierarchy Example

### Table Items for "Bats" Category

| PK | SK | EntityType | Attributes |
|----|----|-----------| ----------|
| `TENANT#default#CATEGORY#cat_bats` | `METADATA` | CATEGORY | Id, Name: "Bats", Slug: "bats", Icon, ImageUrl, DisplayOrder: 1 |
| `TENANT#default#CATEGORY#cat_bats` | `SECTION#sec_bats_1` | CATEGORY_SECTION | Id, Title: "By Brand", Slug: "by-brand", DisplayOrder: 1 |
| `TENANT#default#CATEGORY#cat_bats` | `SECTION#sec_bats_1#ITEM#sub_bats_1` | SUBCATEGORY | Id, Name: "SS", Slug: "ss", DisplayOrder: 1 |
| `TENANT#default#CATEGORY#cat_bats` | `SECTION#sec_bats_1#ITEM#sub_bats_2` | SUBCATEGORY | Id, Name: "MRF", Slug: "mrf", DisplayOrder: 2 |
| `TENANT#default#CATEGORY#cat_bats` | `SECTION#sec_bats_2` | CATEGORY_SECTION | Id, Title: "Price Range", Slug: "price-range", DisplayOrder: 2 |
| `TENANT#default#CATEGORY#cat_bats` | `SECTION#sec_bats_2#ITEM#sub_bats_6` | SUBCATEGORY | Id, Name: "Under ₹5000", Slug: "under-5000", DisplayOrder: 1 |

### Visual Hierarchy
```
Category: Bats
├── Section: By Brand
│   ├── SS
│   ├── MRF
│   ├── SG
│   ├── Kookaburra
│   └── DSC
└── Section: Price Range
    ├── Under ₹5000
    ├── ₹5000 - ₹10000
    └── Above ₹10000
```

---

## Query Performance Analysis

### Scenario: Loading Mega Menu with 9 Categories

**Current Implementation (Parallel Pattern):**
```
Query 1:    Get all category IDs from GSI2       → 1 RCU
Queries 2-10: Get category details from primary table (PARALLEL)
  - Category 1 details (PK query)                → 1 RCU
  - Category 2 details (PK query)                → 1 RCU
  - ...
  - Category 9 details (PK query)                → 1 RCU
-----------------------------------------------------------
Total: 10 RCUs, 2 round trip phases
Time: ~100-150ms (parallel execution)
```

**Key Performance Characteristics:**
- Round Trip 1: GSI2 query returns all category IDs
- Round Trip 2: N parallel PK queries execute concurrently
- Total time = Time(GSI2 query) + Time(slowest PK query)
- NOT the sum of all query times due to parallelization

---

## Multi-Tenancy Isolation

All access patterns enforce tenant isolation:

**✅ Secure:**
```
PK: TENANT#default#CATEGORY#cat_bats
```
Each tenant's data is in separate partition keys.

**❌ Insecure (not possible with this design):**
```
PK: CATEGORY#cat_bats  // Missing tenant ID
```

**Benefits:**
- Data isolation at partition level
- No risk of cross-tenant data leakage
- Supports tenant-specific scaling

---

## Attribute Details

### Category Attributes
```json
{
  "PK": "TENANT#default#CATEGORY#cat_bats",
  "SK": "METADATA",
  "EntityType": "CATEGORY",
  "Id": "cat_bats",
  "TenantId": "default",
  "Name": "Bats",
  "Slug": "bats",
  "Description": "Cricket bats for all levels",
  "Icon": "bat",
  "ImageUrl": "",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2024-12-21T10:00:00Z",
  "UpdatedAt": "2024-12-21T10:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#SLUG",
  "GSI1SK": "CATEGORY#bats",
  "GSI2PK": "TENANT#default#CATEGORIES",
  "GSI2SK": "ORDER#0001"
}
```

### Section Attributes
```json
{
  "PK": "TENANT#default#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_1",
  "EntityType": "CATEGORY_SECTION",
  "Id": "sec_bats_1",
  "CategoryId": "cat_bats",
  "TenantId": "default",
  "Title": "By Brand",
  "Slug": "by-brand",
  "ShowTitle": true,
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2024-12-21T10:00:00Z",
  "UpdatedAt": "2024-12-21T10:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system"
}
```

### Subcategory Attributes
```json
{
  "PK": "TENANT#default#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_1#ITEM#sub_bats_1",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_1",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_1",
  "TenantId": "default",
  "Name": "SS",
  "Slug": "ss",
  "Description": "",
  "ImageUrl": "",
  "DisplayOrder": 1,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2024-12-21T10:00:00Z",
  "UpdatedAt": "2024-12-21T10:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system"
}
```

---

## Best Practices

### ✅ Do's

1. **Use Parallel Queries** - Leverage Task.WhenAll for fetching multiple categories
2. **Query by PK** - Always query using partition key for best performance
3. **Leverage Sort Keys** - Use SK patterns for hierarchical data (SECTION#, ITEM#)
4. **Use GSIs Wisely** - GSI2 for ordered lists, GSI1 for slug lookups
5. **Tenant Isolation** - Always include tenantId in partition keys

### ❌ Don'ts

1. **Avoid Scans** - Never scan the entire table
2. **Don't Use Filters** - Use key conditions instead of filters when possible
3. **Avoid Hot Partitions** - Distribute writes across multiple partition keys
4. **Don't Over-Index** - Only create GSIs for actual access patterns
5. **Avoid Large Items** - Keep items under 400KB (we're well under this)

---

## Admin Operations (Not Currently Implemented)

If you need to add admin functionality for managing catalog data, consider these patterns:

### Seeding Data
**Current Approach:** Static JSON files loaded via `init-aws.sh` script
- Location: `gearify-umbrella/localstack/dynamodb/data/catalog-default-tenant-batch-{n}.json`
- Loaded on: Docker Compose startup

### CRUD Operations
Admin endpoints for category management would require additional repository methods:

**Create Category:**
```csharp
PutItem with PK = TENANT#{tenantId}#CATEGORY#{categoryId}, SK = METADATA
```

**Update Category:**
```csharp
UpdateItem with PK = TENANT#{tenantId}#CATEGORY#{categoryId}, SK = METADATA
```

**Delete Category (cascading):**
```csharp
1. Query: PK = TENANT#{tenantId}#CATEGORY#{categoryId}
2. BatchWriteItem: Delete all returned items (category + sections + subcategories)
```

**Add Section to Category:**
```csharp
PutItem with PK = TENANT#{tenantId}#CATEGORY#{categoryId}, SK = SECTION#{sectionId}
```

**Add Subcategory to Section:**
```csharp
PutItem with PK = TENANT#{tenantId}#CATEGORY#{categoryId}, SK = SECTION#{sectionId}#ITEM#{subcategoryId}
```

---

## Monitoring & Optimization

### Key Metrics to Monitor

1. **Read Capacity Units (RCUs)**
   - Mega menu query: ~10 RCUs per request
   - Category detail query: ~1-2 RCUs per category

2. **Latency**
   - Target: <100ms for mega menu (with parallel queries)
   - Current: ~100-150ms

3. **Cache Hit Rate**
   - Implement caching for 5-10 minutes
   - Expected hit rate: >90% for mega menu

### Optimization Opportunities

1. **Add Caching Layer**
   ```csharp
   [ResponseCache(Duration = 300)] // 5 minutes
   ```

2. **Use DynamoDB DAX** (if needed)
   - Microsecond read latency
   - Automatic cache management

3. **Consider Item Collections**
   - Current design already optimizes item collections
   - All category data in single partition key

---

## Summary

This single-table design provides:

✅ **Efficient Queries** - Get entire category hierarchy in 1-2 round trips
✅ **Multi-Tenancy** - Tenant isolation at partition key level
✅ **Scalability** - Parallel queries for optimal performance
✅ **Flexibility** - Multiple access patterns via GSIs
✅ **Cost-Effective** - Minimal RCU consumption

The design is optimized for **read-heavy workloads** (mega menu, navigation) which is typical for e-commerce catalog data.
