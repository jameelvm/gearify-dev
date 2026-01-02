# Brands DynamoDB Table Documentation

## Table Overview

**Table Name:** `gearify-brands`
**Design Pattern:** Single-Table Design
**Multi-tenancy:** Yes (tenant-isolated via partition keys)

## Purpose

Stores brand/manufacturer master data as independent entities. Brands are referenced by:
- Products (for brand filtering and display)
- Catalog subcategories (for navigation filters)

## Primary Key Structure

| Attribute | Type | Description |
|-----------|------|-------------|
| **PK** | String | Partition Key - `TENANT#{tenantId}#BRAND#{brandId}` |
| **SK** | String | Sort Key - Always `METADATA` |

## Global Secondary Index (GSI1)

**Purpose:** List all brands ordered by name

| Attribute | Type | Description |
|-----------|------|-------------|
| **GSI1PK** | String | Partition Key - `TENANT#{tenantId}#BRANDS` |
| **GSI1SK** | String | Sort Key - `BRAND#{slug}` |

---

## Key Patterns

### Brand Entity
```
PK:      TENANT#{tenantId}#BRAND#{brandId}
SK:      METADATA
Type:    BRAND

GSI1PK:  TENANT#{tenantId}#BRANDS
GSI1SK:  BRAND#{slug}
```

**Example:**
```
PK:      TENANT#default#BRAND#a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d
SK:      METADATA
GSI1PK:  TENANT#default#BRANDS
GSI1SK:  BRAND#ss
```

**Note:** Brand IDs use GUIDs for guaranteed uniqueness and enterprise scalability.

---

## Current Implementation

### Get All Brands
**Repository Method:** `GetAllBrandsAsync(string tenantId)`

**Use Case:** Display all brands for filtering or selection

**Query:**
```csharp
var request = new QueryRequest
{
    TableName = "gearify-brands",
    IndexName = "GSI1",
    KeyConditionExpression = "GSI1PK = :gsi1pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#BRANDS" } }
    }
};
```

**Returns:** All brands ordered by DisplayOrder, then Name

**Performance:** Single query, ~1 RCU for 100 brands

---

### Get Brand by ID
**Repository Method:** `GetBrandByIdAsync(string brandId, string tenantId)`

**Use Case:** Fetch brand details for product display

**Query:**
```csharp
var request = new GetItemRequest
{
    TableName = "gearify-brands",
    Key = new Dictionary<string, AttributeValue>
    {
        { "PK", new AttributeValue { S = $"TENANT#{tenantId}#BRAND#{brandId}" } },
        { "SK", new AttributeValue { S = "METADATA" } }
    }
};
```

**Returns:** Single brand or null

**Performance:** Single GetItem, ~1 RCU

---

### Get Brand by Slug
**Repository Method:** `GetBrandBySlugAsync(string slug, string tenantId)`

**Use Case:** Load brand page from URL (e.g., `/brands/ss`)

**Query:**
```csharp
var request = new QueryRequest
{
    TableName = "gearify-brands",
    IndexName = "GSI1",
    KeyConditionExpression = "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#BRANDS" } },
        { ":gsi1sk", new AttributeValue { S = $"BRAND#{slug}" } }
    }
};
```

**Returns:** Single brand or null

**Performance:** Single query, ~1 RCU

---

## Attribute Details

### Brand Attributes
```json
{
  "PK": "TENANT#default#BRAND#a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "TenantId": "default",
  "Name": "SS",
  "Slug": "ss",
  "Description": "Premium cricket equipment manufacturer from India",
  "Logo": "https://cdn.example.com/brands/ss-logo.png",
  "Country": "India",
  "Website": "https://www.ss.com",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#ss"
}
```

**Enterprise Design Note:**
- **Id**: Uses GUID (Globally Unique Identifier) for automatic uniqueness
- **Slug**: Human-readable identifier for SEO-friendly URLs
- **PK**: Combines tenant isolation with GUID for partition key

---

## Integration with Other Tables

### Catalog Table References
Catalog subcategories reference brands via `BrandId` (GUID):

```json
{
  "PK": "TENANT#default#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_1#ITEM#sub_bats_1",
  "EntityType": "SUBCATEGORY",
  "FilterType": "BRAND",
  "BrandId": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "Name": "SS"
}
```

### Products Table References
Products reference brands via `BrandId` (GUID) and use GSI1 for filtering:

```json
{
  "PK": "TENANT#default#PRODUCT#prod_123",
  "SK": "METADATA",
  "BrandId": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "GSI1PK": "TENANT#default#BRAND#a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "GSI1SK": "PRODUCT#prod_123"
}
```

**Query products by brand:**
```csharp
// Query products table GSI1
QueryRequest: GSI1PK = "TENANT#default#BRAND#a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d"
Returns: All products from SS brand
```

**Why GUIDs for References:**
- **Scalability**: Auto-generated, no manual ID management
- **Uniqueness**: Guaranteed globally unique across all tenants
- **Enterprise Standard**: Industry best practice for distributed systems

---

## Category Section Mapping - Brand Enrichment

### Overview
The mega menu system supports dynamic brand enrichment through the `Mapping` field in CategorySection entities. When a CategorySection has `Mapping: "BRAND"`, the `EnrichSubcategoriesAsync` method automatically enriches subcategories with brand data (logo, description, etc.) using the BrandMapper.

**How It Works:**
1. CategorySection is created with `Mapping: "BRAND"`
2. Subcategories are created with `BrandId` references
3. At runtime, `GetMegaMenuDataQueryHandler.EnrichSubcategoriesAsync()` detects the Mapping field
4. BrandMapper fetches brand details from the brands table
5. Subcategories are enriched with brand logo, name, description

**Implementation Reference:**
See `GetMegaMenuDataQueryHandler.cs` lines 143-163 for enrichment logic.

---

### Seed Data Example: "Bats → By Brand" Section

This example shows how to create a category section that displays cricket bat brands in the mega menu.

#### Step 1: Create the CategorySection with Mapping

```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands",
  "EntityType": "CATEGORY_SECTION",
  "Id": "sec_bats_brands",
  "CategoryId": "cat_bats",
  "TenantId": "default",
  "Title": "By Brand",
  "Slug": "by-brand",
  "ShowTitle": true,
  "Mapping": "BRAND",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

**Key Field:** `"Mapping": "BRAND"` - This triggers brand enrichment

---

#### Step 2: Create Subcategories with BrandId References

**SS Brand Subcategory:**
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands#ITEM#sub_bats_ss",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_ss",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_brands",
  "TenantId": "default",
  "Name": "SS",
  "Slug": "ss",
  "Description": "SS cricket bats - Premium quality from India",
  "ImageUrl": null,
  "BrandId": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "FilterType": "BRAND",
  "DisplayOrder": 1,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

**MRF Brand Subcategory:**
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands#ITEM#sub_bats_mrf",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_mrf",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_brands",
  "TenantId": "default",
  "Name": "MRF",
  "Slug": "mrf",
  "Description": "MRF cricket bats - Trusted by champions",
  "ImageUrl": null,
  "BrandId": "b2c3d4e5-f6a7-5b6c-9d0e-2f3a4b5c6d7e",
  "FilterType": "BRAND",
  "DisplayOrder": 2,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

**SG Brand Subcategory:**
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands#ITEM#sub_bats_sg",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_sg",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_brands",
  "TenantId": "default",
  "Name": "SG",
  "Slug": "sg",
  "Description": "SG cricket bats - Superior craftsmanship",
  "ImageUrl": null,
  "BrandId": "c3d4e5f6-a7b8-6c7d-0e1f-3a4b5c6d7e8f",
  "FilterType": "BRAND",
  "DisplayOrder": 3,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

**Kookaburra Brand Subcategory:**
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands#ITEM#sub_bats_kookaburra",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_kookaburra",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_brands",
  "TenantId": "default",
  "Name": "Kookaburra",
  "Slug": "kookaburra",
  "Description": "Kookaburra cricket bats - Australian excellence",
  "ImageUrl": null,
  "BrandId": "d4e5f6a7-b8c9-7d8e-1f2a-4b5c6d7e8f9a",
  "FilterType": "BRAND",
  "DisplayOrder": 4,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

**DSC Brand Subcategory:**
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_brands#ITEM#sub_bats_dsc",
  "EntityType": "SUBCATEGORY",
  "Id": "sub_bats_dsc",
  "CategoryId": "cat_bats",
  "SectionId": "sec_bats_brands",
  "TenantId": "default",
  "Name": "DSC",
  "Slug": "dsc",
  "Description": "DSC cricket bats - Power and performance",
  "ImageUrl": null,
  "BrandId": "e5f6a7b8-c9d0-8e9f-2a3b-5c6d7e8f9a0b",
  "FilterType": "BRAND",
  "DisplayOrder": 5,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z"
}
```

---

#### Step 3: Create Corresponding Brand Entities

These brands should exist in the `gearify-brands` table:

**SS Brand:**
```json
{
  "PK": "TENANT#default#BRAND#a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "TenantId": "default",
  "Name": "SS",
  "Slug": "ss",
  "Description": "Premium cricket equipment manufacturer from India",
  "Logo": "https://cdn.example.com/brands/ss-logo.png",
  "Country": "India",
  "Website": "https://www.ss.com",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#ss"
}
```

**MRF Brand:**
```json
{
  "PK": "TENANT#default#BRAND#b2c3d4e5-f6a7-5b6c-9d0e-2f3a4b5c6d7e",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "b2c3d4e5-f6a7-5b6c-9d0e-2f3a4b5c6d7e",
  "TenantId": "default",
  "Name": "MRF",
  "Slug": "mrf",
  "Description": "Trusted by champions worldwide",
  "Logo": "https://cdn.example.com/brands/mrf-logo.png",
  "Country": "India",
  "Website": "https://www.mrf.com",
  "DisplayOrder": 2,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#mrf"
}
```

**SG Brand:**
```json
{
  "PK": "TENANT#default#BRAND#c3d4e5f6-a7b8-6c7d-0e1f-3a4b5c6d7e8f",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "c3d4e5f6-a7b8-6c7d-0e1f-3a4b5c6d7e8f",
  "TenantId": "default",
  "Name": "SG",
  "Slug": "sg",
  "Description": "Superior craftsmanship since 1931",
  "Logo": "https://cdn.example.com/brands/sg-logo.png",
  "Country": "India",
  "Website": "https://www.sg.com",
  "DisplayOrder": 3,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#sg"
}
```

**Kookaburra Brand:**
```json
{
  "PK": "TENANT#default#BRAND#d4e5f6a7-b8c9-7d8e-1f2a-4b5c6d7e8f9a",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "d4e5f6a7-b8c9-7d8e-1f2a-4b5c6d7e8f9a",
  "TenantId": "default",
  "Name": "Kookaburra",
  "Slug": "kookaburra",
  "Description": "Australian excellence in cricket equipment",
  "Logo": "https://cdn.example.com/brands/kookaburra-logo.png",
  "Country": "Australia",
  "Website": "https://www.kookaburra.com",
  "DisplayOrder": 4,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#kookaburra"
}
```

**DSC Brand:**
```json
{
  "PK": "TENANT#default#BRAND#e5f6a7b8-c9d0-8e9f-2a3b-5c6d7e8f9a0b",
  "SK": "METADATA",
  "EntityType": "BRAND",
  "Id": "e5f6a7b8-c9d0-8e9f-2a3b-5c6d7e8f9a0b",
  "TenantId": "default",
  "Name": "DSC",
  "Slug": "dsc",
  "Description": "Power and performance combined",
  "Logo": "https://cdn.example.com/brands/dsc-logo.png",
  "Country": "India",
  "Website": "https://www.dsc.com",
  "DisplayOrder": 5,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00.000Z",
  "UpdatedAt": "2025-12-21T00:00:00.000Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#BRANDS",
  "GSI1SK": "BRAND#dsc"
}
```

---

### How Runtime Enrichment Works

**Before Enrichment (from DynamoDB):**
```json
{
  "Id": "sub_bats_ss",
  "Name": "SS",
  "BrandId": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "ImageUrl": null
}
```

**After Enrichment (by BrandMapper):**
```json
{
  "Id": "sub_bats_ss",
  "Name": "SS",
  "BrandId": "a1b2c3d4-e5f6-4a5b-8c9d-1e2f3a4b5c6d",
  "ImageUrl": "https://cdn.example.com/brands/ss-logo.png",
  "BrandDetails": {
    "Name": "SS",
    "Logo": "https://cdn.example.com/brands/ss-logo.png",
    "Description": "Premium cricket equipment manufacturer from India",
    "Country": "India"
  }
}
```

**Benefits:**
- Brand logos automatically populate in mega menu
- Consistent brand information across the application
- Single source of truth for brand data
- Easy to update brand information (update once in brands table, reflects everywhere)

---

## Multi-Tenancy Isolation

All access patterns enforce tenant isolation:

**✅ Secure:**
```
PK: TENANT#default#BRAND#brand_ss
```
Each tenant's brands are in separate partition keys.

**Benefits:**
- Data isolation at partition level
- No risk of cross-tenant data leakage
- Supports tenant-specific brand catalogs

---

## Best Practices

### ✅ Do's

1. **Cache brand data** - Brands change rarely, cache for 30+ minutes
2. **Denormalize brand name** - Store brand name in products for display (avoids joins)
3. **Use BrandId references** - Never store brand data inline, always reference
4. **Keep logo URLs** - Store CDN URLs for brand logos
5. **Maintain slug uniqueness** - Ensure slugs are unique per tenant

### ❌ Don'ts

1. **Don't scan** - Always query by PK or GSI1
2. **Don't duplicate brands** - Single source of truth
3. **Don't skip tenant isolation** - Always include tenantId in keys
4. **Don't store products in brands table** - Use references only

---

## Query Performance

### Scenario: Product Page with Brand Filter

**Total Operations:**
```
1. Get brand details (if needed)
   Query: PK = "TENANT#default#BRAND#brand_ss"
   Cost: 1 RCU
   Time: 5ms

2. Get products by brand (from products table)
   Query products GSI1: GSI1PK = "TENANT#default#BRAND#brand_ss"
   Cost: Depends on product count
   Time: 10-20ms

Total: ~15-25ms
```

---

## Admin Operations (Future)

If you need to add admin functionality:

### Create Brand
```csharp
PutItem with:
  PK = "TENANT#{tenantId}#BRAND#{brandId}"
  SK = "METADATA"
  GSI1PK = "TENANT#{tenantId}#BRANDS"
  GSI1SK = "BRAND#{slug}"
```

### Update Brand
```csharp
UpdateItem with:
  PK = "TENANT#{tenantId}#BRAND#{brandId}"
  SK = "METADATA"
```

### Delete Brand
```csharp
1. Check if brand is referenced by products (query products GSI1)
2. If no references, DeleteItem
3. If references exist, mark IsActive = false (soft delete)
```

---

## Summary

**Key Benefits:**
- ✅ **Normalized data** - Single source of truth for brands
- ✅ **Fast queries** - All queries use keys (no scans)
- ✅ **Multi-tenant** - Complete isolation per tenant
- ✅ **Flexible** - Easy to add brand-specific features
- ✅ **Cacheable** - Brands change rarely, perfect for caching

**Design Philosophy:**
Brands are independent entities that products and catalog reference by ID. This enables:
- Consistent brand information across products
- Efficient brand filtering on product pages
- Easy brand management (update once, reflects everywhere)
- Rich brand data (logos, descriptions, country, etc.)
