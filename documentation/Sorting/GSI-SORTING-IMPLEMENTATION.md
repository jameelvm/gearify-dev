# GSI-Based Sorting Implementation

## Overview

Successfully implemented **database-level sorting** for products using DynamoDB Global Secondary Indexes (GSIs). This replaces the previous in-memory LINQ sorting approach with proper DynamoDB queries that scale efficiently.

---

## Understanding GSIs (Global Secondary Indexes)

### What are GSIs?
GSIs are like **additional tables** that allow you to query your data using different keys and sort orders. Each GSI has its own Partition Key (PK) and Sort Key (SK), which DynamoDB uses to organize and retrieve data efficiently.

### Why 6 GSIs?
Each GSI enables a specific sorting capability:

| GSI Name | What It Does | When It's Used |
|----------|--------------|----------------|
| **GSI1** | Lists all products for a tenant | Default listing (no sort specified) |
| **GSI2** | Sorts products by **price** | `sortBy=price-asc` or `sortBy=price-desc` |
| **GSI3** | Sorts products by **rating** | `sortBy=rating` (highest rated first) |
| **GSI4** | Sorts products by **creation date** | `sortBy=newest` (newest products first) |
| **GSI5** | Sorts products by **name** | `sortBy=name` (alphabetical A→Z) |
| **GSI6** | Lists **featured products only** | `sortBy=featured` (sparse index) |

### GSI Structure Reference

Here's exactly what each GSI stores:

```
Main Table:
PK: TENANT#default                    SK: PRODUCT#prod-123

GSI1 (All Products - Default Listing):
GSI1PK: TENANT#default#PRODUCTS       GSI1SK: PRODUCT#prod-123

GSI2 (Price Sort):
GSI2PK: TENANT#default                GSI2SK: PRICE#0000129999#PRODUCT#prod-123
                                              ↑ Padded to 10 digits (price in cents)

GSI3 (Rating Sort):
GSI3PK: TENANT#default                GSI3SK: RATING#00450#PRODUCT#prod-123
                                              ↑ Padded to 5 digits (rating * 100)

GSI4 (Newest First Sort):
GSI4PK: TENANT#default                GSI4SK: CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
                                              ↑ ISO 8601 timestamp

GSI5 (Name A-Z Sort):
GSI5PK: TENANT#default                GSI5SK: NAME#kookaburra bat#PRODUCT#prod-123
                                              ↑ Lowercase name for case-insensitive sort

GSI6 (Featured Products - Sparse):
GSI6PK: TENANT#default#FEATURED       GSI6SK: CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
        ↑ Only created if IsFeatured=true
```

### For Developers: How to Create/Update Products

**Good News:** You don't need to manually compute GSI keys!

The `GsiKeyHelper` class and `DynamoDbProductRepository.CreateAsync()` **automatically compute all GSI keys** for you.

**When creating a product:**
```csharp
// Just create your product with normal fields
var product = new Product
{
    Id = "prod-123",
    TenantId = "default",
    Name = "Kookaburra Bat",
    Price = 1299.99m,
    RatingAverage = 4.5m,
    IsFeatured = true,
    CreatedAt = DateTime.UtcNow,
    // ... other fields
};

// Call CreateAsync - it handles ALL GSI keys automatically
await _productRepository.CreateAsync(product);

// Behind the scenes, CreateAsync:
// 1. Calls GsiKeyHelper.ComputePriceSortKey(product) → "PRICE#0000129999#PRODUCT#prod-123"
// 2. Calls GsiKeyHelper.ComputeRatingSortKey(product) → "RATING#00450#PRODUCT#prod-123"
// 3. Calls GsiKeyHelper.ComputeCreatedAtSortKey(product) → "CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123"
// 4. Calls GsiKeyHelper.ComputeNameSortKey(product) → "NAME#kookaburra bat#PRODUCT#prod-123"
// 5. Calls GsiKeyHelper.ComputeFeaturedSortKeys(product) → Only if IsFeatured=true
// 6. Stores all these computed keys in DynamoDB
```

**You never manually set GSI keys!** The repository handles everything.

---

## What Changed

### 1. **DynamoDB Table Schema** (`localstack/dynamodb/tables/products.json`)
Added 5 new GSIs to the products table:

| GSI | Purpose | PK | SK | Sort Direction |
|-----|---------|----|----|----------------|
| **GSI2** | Price Sorting | `TENANT#{tenantId}` | `PRICE#{cents}#PRODUCT#{id}` | Asc/Desc |
| **GSI3** | Rating Sorting | `TENANT#{tenantId}` | `RATING#{hundredths}#PRODUCT#{id}` | Desc (top rated first) |
| **GSI4** | Newest Sorting | `TENANT#{tenantId}` | `CREATEDAT#{iso8601}#PRODUCT#{id}` | Desc (newest first) |
| **GSI5** | Name Sorting (A-Z) | `TENANT#{tenantId}` | `NAME#{lowercase}#PRODUCT#{id}` | Asc (A→Z) |
| **GSI6** | Featured Products | `TENANT#{tenantId}#FEATURED` | `CREATEDAT#{iso8601}#PRODUCT#{id}` | Desc (sparse index) |

**GSI1** was also updated:
- **Old**: `TENANT#{tenantId}#CATEGORY#{category}`
- **New**: `TENANT#{tenantId}#PRODUCTS` (used for unsorted/default queries)

---

### 2. **GSI Key Helper** (`gearify-catalog-svc/Infrastructure/Helpers/GsiKeyHelper.cs`)
Created helper class to compute GSI sort keys:

```csharp
public static class GsiKeyHelper
{
    // Price: $1299.99 → "PRICE#0000129999#PRODUCT#prod-201"
    public static string ComputePriceSortKey(Product product);

    // Rating: 4.5 stars → "RATING#00450#PRODUCT#prod-201"
    public static string ComputeRatingSortKey(Product product);

    // Created: ISO8601 → "CREATEDAT#2025-01-17T10:00:00.000Z#PRODUCT#prod-201"
    public static string ComputeCreatedAtSortKey(Product product);

    // Name: "SS Ton Reserve" → "NAME#ss ton reserve#PRODUCT#prod-201"
    public static string ComputeNameSortKey(Product product);

    // Featured: Sparse index (only if IsFeatured=true)
    public static (string? GSI6PK, string? GSI6SK) ComputeFeaturedSortKeys(Product product);
}
```

---

### 3. **Product Repository** (`DynamoDbProductRepository.cs`)
Updated `CreateAsync()` method to compute and store all GSI keys:

```csharp
// GSI2: Price sorting
{ "GSI2PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
{ "GSI2SK", new AttributeValue { S = GsiKeyHelper.ComputePriceSortKey(product) } },

// GSI3: Rating sorting
{ "GSI3PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
{ "GSI3SK", new AttributeValue { S = GsiKeyHelper.ComputeRatingSortKey(product) } },

// ... GSI4, GSI5, GSI6
```

Also added missing boolean fields: `IsDeal`, `IsClearance`, `IsNewArrival`, `IsBestSeller`, `IsFeatured`, `DealStartDate`, `DealEndDate`, `CustomCollections`.

---

### 4. **Query Handler** (`GetProductsBySlugQueryHandler.cs`)
Completely rewrote sorting logic:

**Before** (in-memory LINQ):
```csharp
allProducts = request.SortBy.ToLower() switch
{
    "price-asc" => allProducts.OrderBy(p => p.Price).ToList(),
    "price-desc" => allProducts.OrderByDescending(p => p.Price).ToList(),
    // ... more in-memory sorts
};
```

**After** (database-level GSI queries):
```csharp
private (string IndexName, bool SortAscending) GetIndexAndSortDirection(string? sortBy)
{
    return sortBy.ToLower() switch
    {
        "featured" => ("GSI6", false),      // GSI6: Featured products, newest first
        "price-asc" => ("GSI2", true),      // GSI2: Price low to high
        "price-desc" => ("GSI2", false),    // GSI2: Price high to low
        "rating" => ("GSI3", false),        // GSI3: Top rated first
        "newest" => ("GSI4", false),        // GSI4: Newest first
        "name" => ("GSI5", true),           // GSI5: Name A-Z
        _ => ("GSI1", true)                 // Default: GSI1 unsorted
    };
}
```

Now queries the appropriate GSI directly with `ScanIndexForward` for sort direction.

---

### 5. **Seed Data** (75 products updated)
All product seed files updated with GSI2-GSI6 keys:
- `products-default-tenant-batch.json` (25 products)
- `products-default-tenant-batch-2.json` (21 products)
- `products-test-tenant-batch.json` (5 products)
- `products-acme-corp-batch.json` (4 products)
- `products-additional-batch.json` (20 products)

Example:
```json
{
  "PK": {"S": "TENANT#default"},
  "SK": {"S": "PRODUCT#prod-201"},
  "GSI2PK": {"S": "TENANT#default"},
  "GSI2SK": {"S": "PRICE#0000129999#PRODUCT#prod-201"},
  "GSI3PK": {"S": "TENANT#default"},
  "GSI3SK": {"S": "RATING#00450#PRODUCT#prod-201"},
  // ... GSI4, GSI5, GSI6
}
```

---

### 6. **LocalStack Init Script** (`init-aws.sh`)
Updated products table creation with all 6 GSIs:

```bash
awslocal dynamodb create-table \
  --table-name gearify-products \
  --attribute-definitions \
    AttributeName=GSI2PK,AttributeType=S \
    AttributeName=GSI2SK,AttributeType=S \
    AttributeName=GSI3PK,AttributeType=S \
    # ... GSI4, GSI5, GSI6
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI2\",...},{\"IndexName\":\"GSI3\",...}...]"
```

Also added sort options seeding.

---

## Benefits of GSI-Based Sorting

### ✅ **Before (In-Memory Sorting)**
- ❌ Loads ALL matching products into memory
- ❌ Sorts them with LINQ
- ❌ Then paginates manually
- ❌ **Doesn't scale** - performance degrades with large catalogs
- ❌ **Breaks cursor pagination** - cursor becomes meaningless

### ✅ **After (Database-Level Sorting)**
- ✅ Queries DynamoDB with sort order at database level
- ✅ Returns pre-sorted results
- ✅ True cursor-based pagination works correctly
- ✅ **Scales to millions of products** with consistent performance
- ✅ **Low latency** - no in-memory sorting overhead

---

## How to Apply

### Step 1: Recreate DynamoDB Table
Run the recreate script to drop and recreate the products table with new GSIs:

```bash
cd C:/Gearify/gearify-umbrella/localstack
bash recreate-products-table.sh
```

This will:
1. Delete the old `gearify-products` table
2. Create a new one with GSI2-GSI6
3. Reload all 75 products with updated GSI keys
4. Verify the data loaded correctly

**Expected output:**
```
============================================================
Recreating Products Table with Sort GSIs
============================================================

1. Deleting existing products table...
   [OK] Table deleted

2. Creating products table with GSI2-GSI6 for sorting...
   [OK] Table created with 6 GSIs

3. Reloading product seed data...
   - Loading default-tenant products (batch 1/2)
      [OK]
   ...

4. Verifying table...
   Total products loaded: 75

5. Verifying GSIs...
GSI1    GSI2    GSI3    GSI4    GSI5    GSI6

[OK] Products table recreated successfully!
```

---

### Step 2: Rebuild Catalog Service
Rebuild the catalog service to include the new helper and repository changes:

```bash
cd C:/Gearify/gearify-catalog-svc
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

### Step 3: Restart Services
Restart the catalog service to pick up the changes:

```bash
cd C:/Gearify/gearify-umbrella
docker-compose restart catalog-svc
```

Or if running locally:
```bash
dotnet run --project C:/Gearify/gearify-catalog-svc/Gearify.CatalogService.csproj
```

---

## Testing

### Test 1: Price Sorting (Low to High)
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=price-asc&pageSize=5"
```

**Expected**: Products ordered by price ascending (cheapest first)

---

### Test 2: Price Sorting (High to Low)
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=price-desc&pageSize=5"
```

**Expected**: Products ordered by price descending (most expensive first)

---

### Test 3: Top Rated
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=rating&pageSize=5"
```

**Expected**: Products ordered by rating descending (highest rated first)

---

### Test 4: Newest First
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=newest&pageSize=5"
```

**Expected**: Products ordered by CreatedAt descending (newest first)

---

### Test 5: Name A-Z
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=name&pageSize=5"
```

**Expected**: Products ordered alphabetically by name

---

### Test 6: Featured Items
```bash
curl "http://localhost:5001/api/catalog/products?sortBy=featured&pageSize=5"
```

**Expected**: Only products with `IsFeatured=true`, ordered by newest first (uses sparse GSI6)

---

### Test 7: Pagination with Sorting
```bash
# Get first page
curl "http://localhost:5001/api/catalog/products?sortBy=price-asc&pageSize=5"

# Extract nextCursor from response, then:
curl "http://localhost:5001/api/catalog/products?sortBy=price-asc&pageSize=5&cursor={nextCursor}"
```

**Expected**: Pagination maintains sort order across pages

---

## Frontend Integration

The frontend already has the sort options configured in:
- `filter.component.ts` - loads sort options from `/api/catalog/sort-options`
- `filter.component.html` - renders sort dropdown

Sort option values from frontend map to GSIs as follows:

| Frontend `sortBy` Value | GSI Used | Sort Direction |
|-------------------------|----------|----------------|
| `featured` | GSI6 | Desc (newest featured first) |
| `price-asc` | GSI2 | Asc (low → high) |
| `price-desc` | GSI2 | Desc (high → low) |
| `rating` | GSI3 | Desc (top rated first) |
| `newest` | GSI4 | Desc (newest first) |
| `name` | GSI5 | Asc (A → Z) |

---

## Files Modified

### Backend (C# .NET)
1. ✅ `gearify-catalog-svc/Infrastructure/Helpers/GsiKeyHelper.cs` (NEW)
2. ✅ `gearify-catalog-svc/Infrastructure/Repositories/DynamoDbProductRepository.cs`
3. ✅ `gearify-catalog-svc/Application/Queries/GetProductsBySlugQueryHandler.cs`

### Infrastructure (DynamoDB/LocalStack)
4. ✅ `gearify-umbrella/localstack/dynamodb/tables/products.json`
5. ✅ `gearify-umbrella/localstack/init-aws.sh`
6. ✅ `gearify-umbrella/localstack/recreate-products-table.sh` (NEW)

### Seed Data (75 products)
7. ✅ `localstack/dynamodb/data/products-default-tenant-batch.json`
8. ✅ `localstack/dynamodb/data/products-default-tenant-batch-2.json`
9. ✅ `localstack/dynamodb/data/products-test-tenant-batch.json`
10. ✅ `localstack/dynamodb/data/products-acme-corp-batch.json`
11. ✅ `localstack/dynamodb/data/products-additional-batch.json`

---

## Cleanup Scripts (Optional)

The Python automation scripts are located in `gearify-umbrella/localstack/`:
- `update-gsi-sorting.py` - Updates repository and seed data (already run)
- `update-query-handler.py` - Updates query handler (already run)
- `update-init-script.py` - Updates init script (already run)

These were used for the initial implementation and can be deleted if desired.

---

## Future Enhancements

1. **Add filters to GSI queries** - Currently filters are applied post-query. Could optimize by creating composite GSI keys like `TENANT#{tenantId}#DEPT#{dept}` for GSI2-GSI6.

2. **OpenSearch integration** - For complex multi-field sorting and full-text search, consider migrating to OpenSearch while keeping DynamoDB for transactional data.

3. **Caching** - Add Redis caching for frequently accessed sorted product lists.

---

## Questions?

If you encounter any issues:
1. Check catalog-svc logs for errors
2. Verify GSIs exist: `awslocal dynamodb describe-table --table-name gearify-products --region us-east-1`
3. Check product count: `awslocal dynamodb scan --table-name gearify-products --select COUNT --region us-east-1`
4. Test sort API directly with curl commands above

---

**Status**: ✅ Implementation Complete - Ready for Testing


---

## Quick Reference

**Too much detail?** See [GSI-QUICK-REFERENCE.md](GSI-QUICK-REFERENCE.md) for a simplified guide with:
- Simple explanations of each GSI
- Visual examples
- Common questions and answers
- Zero jargon developer guide

---

