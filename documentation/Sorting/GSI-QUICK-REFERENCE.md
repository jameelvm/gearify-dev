# GSI Quick Reference Guide

## For Developers: Creating Products

### The Simple Way (Recommended)

**You don't need to worry about GSI keys!** Just create your product with normal fields and call `CreateAsync()`:

```csharp
var product = new Product
{
    Id = "prod-123",
    TenantId = "default",
    Name = "Kookaburra Kahuna Pro Bat",
    Price = 1299.99m,
    RatingAverage = 4.7m,
    IsFeatured = true,
    CreatedAt = DateTime.UtcNow,
    // ... other fields
};

// This automatically computes ALL GSI keys for you!
await _productRepository.CreateAsync(product);
```

The repository handles everything behind the scenes. No manual GSI key computation needed!

---

## Understanding the 6 GSIs

### Simple Explanation

Each GSI is like a different "view" of your products, sorted a different way:

| GSI Name | What It Does | API Usage |
|----------|--------------|-----------|
| **GSI1** | Lists ALL products | `GET /api/catalog/products` (no sortBy) |
| **GSI2** | Sorts by PRICE | `GET /api/catalog/products?sortBy=price-asc` |
| **GSI3** | Sorts by RATING | `GET /api/catalog/products?sortBy=rating` |
| **GSI4** | Sorts by DATE (newest first) | `GET /api/catalog/products?sortBy=newest` |
| **GSI5** | Sorts by NAME (A-Z) | `GET /api/catalog/products?sortBy=name` |
| **GSI6** | Shows FEATURED products only | `GET /api/catalog/products?sortBy=featured` |

---

## Technical Details: GSI Structure

### Main Table
```
PK: TENANT#default
SK: PRODUCT#prod-123
```

### GSI1: All Products (Default Listing)
```
GSI1PK: TENANT#default#PRODUCTS
GSI1SK: PRODUCT#prod-123
```
- **Purpose:** List all products without sorting
- **Used when:** No sortBy parameter or sortBy=default

### GSI2: Price Sorting
```
GSI2PK: TENANT#default
GSI2SK: PRICE#0000129999#PRODUCT#prod-123
                ↑ 10-digit zero-padded cents
```
- **Purpose:** Sort products by price (low→high or high→low)
- **Used when:** sortBy=price-asc or sortBy=price-desc
- **Example:** $1299.99 becomes `PRICE#0000129999#PRODUCT#prod-123`
- **Why zero-pad?** DynamoDB sorts lexicographically, so "129999" > "0780"

### GSI3: Rating Sorting
```
GSI3PK: TENANT#default
GSI3SK: RATING#00450#PRODUCT#prod-123
                ↑ 5-digit zero-padded hundredths
```
- **Purpose:** Sort products by rating (highest first)
- **Used when:** sortBy=rating
- **Example:** 4.5 stars becomes `RATING#00450#PRODUCT#prod-123`
- **Null ratings:** Get `00000` (appear first when ascending)

### GSI4: Newest First Sorting
```
GSI4PK: TENANT#default
GSI4SK: CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
                  ↑ ISO 8601 timestamp
```
- **Purpose:** Sort products by creation date (newest first)
- **Used when:** sortBy=newest
- **Format:** ISO 8601 timestamps sort correctly lexicographically

### GSI5: Name Sorting (A-Z)
```
GSI5PK: TENANT#default
GSI5SK: NAME#kookaburra kahuna pro bat#PRODUCT#prod-123
             ↑ Lowercase normalized name
```
- **Purpose:** Sort products alphabetically
- **Used when:** sortBy=name
- **Why lowercase?** Ensures case-insensitive sorting ("Apple" and "apple" sort together)

### GSI6: Featured Products (Sparse Index)
```
GSI6PK: TENANT#default#FEATURED
GSI6SK: CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
```
- **Purpose:** List only featured products (newest first)
- **Used when:** sortBy=featured
- **Special:** This is a "sparse" index - only products with `IsFeatured=true` have GSI6 keys
- **Why sparse?** Saves storage - unfeatured products don't need these keys

---

## Code Reference

### Where GSI Keys are Computed

**File:** `gearify-catalog-svc/Infrastructure/Helpers/GsiKeyHelper.cs`

```csharp
// GSI2 - Price Sort
GsiKeyHelper.ComputePriceSortKey(product)
// → "PRICE#0000129999#PRODUCT#prod-123"

// GSI3 - Rating Sort
GsiKeyHelper.ComputeRatingSortKey(product)
// → "RATING#00450#PRODUCT#prod-123"

// GSI4 - Date Sort
GsiKeyHelper.ComputeCreatedAtSortKey(product)
// → "CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123"

// GSI5 - Name Sort
GsiKeyHelper.ComputeNameSortKey(product)
// → "NAME#kookaburra kahuna pro bat#PRODUCT#prod-123"

// GSI6 - Featured (Sparse)
GsiKeyHelper.ComputeFeaturedSortKeys(product)
// → ("TENANT#default#FEATURED", "CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123")
// OR (null, null) if not featured
```

### Where GSI Keys are Stored

**File:** `gearify-catalog-svc/Infrastructure/Repositories/DynamoDbProductRepository.cs`

See `CreateAsync()` method lines 111-158. It:
1. Calls GsiKeyHelper methods to compute each sort key
2. Stores all keys in DynamoDB automatically

### Where GSI Queries Happen

**File:** `gearify-catalog-svc/Application/Queries/GetProductsBySlugQueryHandler.cs`

See `GetIndexAndSortDirection()` method (lines 145-163) which maps sortBy values to GSI indexes.

---

## Common Questions

### Q: Do I need to manually set GSI keys when creating a product?
**A:** No! Just call `await _productRepository.CreateAsync(product)` - it handles everything.

### Q: What if I update a product's price/name/rating?
**A:** Call `await _productRepository.UpdateAsync(product)` - it recomputes all GSI keys automatically.

### Q: Why are there duplicate-looking keys (GSI2PK, GSI2SK, GSI3PK, GSI3SK...)?
**A:** Each GSI needs its own Partition Key (PK) and Sort Key (SK). They're not duplicates - each serves a different sorting purpose.

### Q: Can I query multiple GSIs at once?
**A:** No. DynamoDB queries one GSI at a time. Use filters if you need multiple criteria.

### Q: What happens if I forget to set IsFeatured=true but want featured products?
**A:** The product won't appear in `sortBy=featured` queries because GSI6 keys are only created when `IsFeatured=true`.

---

## Visual Example: One Product, All GSIs

Product:
```json
{
  "id": "prod-123",
  "tenantId": "default",
  "name": "Kookaburra Kahuna Pro Bat",
  "price": 1299.99,
  "ratingAverage": 4.7,
  "isFeatured": true,
  "createdAt": "2025-12-29T00:00:00.000Z"
}
```

Stored in DynamoDB as:
```
Main:   PK=TENANT#default           SK=PRODUCT#prod-123
GSI1:   GSI1PK=TENANT#default#PRODUCTS   GSI1SK=PRODUCT#prod-123
GSI2:   GSI2PK=TENANT#default       GSI2SK=PRICE#0000129999#PRODUCT#prod-123
GSI3:   GSI3PK=TENANT#default       GSI3SK=RATING#00470#PRODUCT#prod-123
GSI4:   GSI4PK=TENANT#default       GSI4SK=CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
GSI5:   GSI5PK=TENANT#default       GSI5SK=NAME#kookaburra kahuna pro bat#PRODUCT#prod-123
GSI6:   GSI6PK=TENANT#default#FEATURED   GSI6SK=CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#prod-123
```

All computed automatically by `CreateAsync()`!

---

**Pro Tip:** If you're ever confused about which GSI does what, just look at the SK (Sort Key) format - it tells you what it's sorting by!
