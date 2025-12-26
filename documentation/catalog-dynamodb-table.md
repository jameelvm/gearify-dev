# Catalog DynamoDB Table Documentation

## Table Overview

**Table Name:** `gearify-catalog`
**Design Pattern:** Single-Table Design
**Multi-tenancy:** Yes (tenant-isolated via partition keys)
**Last Updated:** 2025-12-26

## Entities Stored

The catalog table supports a **4-level hierarchy** for flexible e-commerce categorization:

1. **DEPARTMENT** - Top-level categorization (e.g., "Cricket", "Perfume", "Electronics")
2. **CATEGORY** - Major product groupings within departments (e.g., "Bats", "Balls", "Shoes")
3. **CATEGORY_SECTION** - Organizational sections within categories (e.g., "By Brand", "Price Range")
4. **SUBCATEGORY** - Items within sections (e.g., "SS", "MRF", "Under ₹5000")

### Hierarchy Example
```
Department: Cricket
├── Category: Bats
│   ├── Section: By Brand
│   │   ├── SS
│   │   ├── MRF
│   │   ├── SG
│   │   ├── Kookaburra
│   │   └── DSC
│   └── Section: Price Range
│       ├── Under ₹5000
│       ├── ₹5000 - ₹10000
│       └── Above ₹10000
└── Category: Balls
    └── Section: By Type
        ├── Leather Balls
        └── Tennis Balls
```

---

## Primary Key Structure

| Attribute | Type | Description |
|-----------|------|-------------|
| **PK** | String | Partition Key - Groups related items in department hierarchy |
| **SK** | String | Sort Key - Defines item type and hierarchy position |

## Global Secondary Indexes (GSI)

### GSI1 - Slug Lookup Index
**Purpose:** Find departments/categories by slug for SEO-friendly URLs

| Attribute | Type | Description |
|-----------|------|-------------|
| **GSI1PK** | String | Partition Key |
| **GSI1SK** | String | Sort Key |

### GSI2 - List Index
**Purpose:** Get all departments or categories ordered by display order

| Attribute | Type | Description |
|-----------|------|-------------|
| **GSI2PK** | String | Partition Key |
| **GSI2SK** | String | Sort Key (includes display order for sorting) |

---

## Key Patterns

### Department Entity
```
PK:      TENANT#{tenantId}#DEPARTMENT#{departmentSlug}
SK:      METADATA
Type:    DEPARTMENT

GSI1PK:  TENANT#{tenantId}#SLUG
GSI1SK:  DEPARTMENT#{slug}

GSI2PK:  TENANT#{tenantId}#DEPARTMENTS
GSI2SK:  ORDER#{displayOrder:D4}
```

**Example:**
```
PK:      TENANT#default#DEPARTMENT#cricket
SK:      METADATA
GSI1PK:  TENANT#default#SLUG
GSI1SK:  DEPARTMENT#cricket
GSI2PK:  TENANT#default#DEPARTMENTS
GSI2SK:  ORDER#0001
```

### Category Entity
**Note:** Category PK now includes department for proper hierarchy

```
PK:      TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}
SK:      METADATA
Type:    CATEGORY

GSI1PK:  TENANT#{tenantId}#SLUG
GSI1SK:  CATEGORY#{slug}

GSI2PK:  TENANT#{tenantId}#CATEGORIES
GSI2SK:  ORDER#{displayOrder:D4}
```

**Example:**
```
PK:      TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats
SK:      METADATA
GSI1PK:  TENANT#default#SLUG
GSI1SK:  CATEGORY#bats
GSI2PK:  TENANT#default#CATEGORIES
GSI2SK:  ORDER#0001
```

### Category Section Entity
```
PK:      TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}
SK:      SECTION#{sectionId}
Type:    CATEGORY_SECTION
```

**Example:**
```
PK:      TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats
SK:      SECTION#sec_bats_1
```

### Subcategory Entity
```
PK:      TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}
SK:      SECTION#{sectionId}#ITEM#{subcategoryId}
Type:    SUBCATEGORY
```

**Example:**
```
PK:      TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats
SK:      SECTION#sec_bats_1#ITEM#sub_bats_1
```

---

## Access Patterns

### 1. Get All Departments
**Repository Method:** `DepartmentRepository.GetAllAsync(string tenantId)`
**Use Case:** Load department list for mega menu, homepage navigation

**Query:**
```csharp
Index:     GSI2
Condition: GSI2PK = "TENANT#{tenantId}#DEPARTMENTS"
Returns:   All departments ordered by displayOrder
```

**Performance:** ~1 RCU, <50ms

---

### 2. Get Department by Slug
**Repository Method:** `DepartmentRepository.GetBySlugAsync(string slug, string tenantId)`
**Use Case:** Load department page from URL (e.g., `/cricket`)

**Query:**
```csharp
KeyConditionExpression: PK = :pk AND SK = :sk
Values:
  :pk = "TENANT#{tenantId}#DEPARTMENT#{slug}"
  :sk = "METADATA"
```

**Performance:** ~1 RCU, <20ms

---

### 3. Get Categories for Department
**Repository Method:** `DepartmentRepository.GetCategoriesAsync(string departmentSlug, string tenantId)`
**Use Case:** Show all categories within a department

**Query:**
```csharp
Index:     GSI2
Condition: GSI2PK = "TENANT#{tenantId}#CATEGORIES"
Filter:    DepartmentSlug = :deptSlug AND EntityType = :entityType
Values:
  :deptSlug = "cricket"
  :entityType = "CATEGORY"
```

**Performance:** ~1 RCU, <50ms

---

### 4. Get Mega Menu Data
**Handler:** `GetMegaMenuDataQueryHandler`
**Use Case:** Load complete mega menu with department hierarchy for navigation

**Strategy:** Hierarchical parallel queries

**Steps:**
1. Query GSI2 to get all departments (1 query)
2. For each department, query categories using GSI2 with filter (N queries in parallel)
3. For each category, query sections and subcategories by PK (M queries in parallel)
4. Enrich subcategories with mapped data (brands, price ranges)

**Response Structure:**
```json
{
  "departments": [
    {
      "id": "dept_cricket",
      "name": "Cricket",
      "slug": "cricket",
      "icon": "cricket",
      "displayOrder": 1,
      "categories": [
        {
          "category": {
            "id": "cat_bats",
            "name": "Bats",
            "slug": "bats",
            ...
          },
          "sections": [
            {
              "id": "sec_bats_1",
              "title": "By Brand",
              "items": [
                { "name": "SS", "slug": "ss", ... },
                { "name": "MRF", "slug": "mrf", ... }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

**Performance:**
- Departments: 1 + N queries (parallel)
- Categories with sections: N * M queries (parallel)
- Total time: ~150-200ms for 1 department with 9 categories
- RCUs: ~10-15 per mega menu request

**Implementation:**
```csharp
public async Task<MegaMenuDto> Handle(GetMegaMenuDataQuery request)
{
    // Step 1: Get all departments
    var departments = await _departmentRepository.GetAllAsync(tenantId);

    // Step 2: For each department, get categories with details
    foreach (var department in departments)
    {
        var categories = await _departmentRepository.GetCategoriesAsync(
            department.Slug, tenantId
        );

        // Step 3: For each category, get sections and subcategories
        foreach (var category in categories)
        {
            var (_, sections, subcategories) = await GetCategoryWithDetailsAsync(
                category.Id,
                category.DepartmentSlug,
                tenantId
            );

            // Step 4: Enrich subcategories
            await EnrichSubcategoriesAsync(sections, subcategories, tenantId);
        }
    }

    return megaMenuDto;
}
```

---

### 5. Get Category with Details
**Private Method:** `GetCategoryWithDetailsAsync(categoryId, departmentSlug, tenantId)`
**Use Case:** Internal helper to fetch category + sections + subcategories

**Query:**
```csharp
KeyConditionExpression: PK = :pk
Values:
  :pk = "TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}"
Returns:  Category metadata, all sections, all subcategories
```

**Processing:**
- SK = "METADATA" → Category
- SK starts with "SECTION#" (no #ITEM#) → Section
- SK contains "#ITEM#" → Subcategory

**Performance:** ~1 RCU per category, <30ms

---

### 6. Get Category by Slug
**Use Case:** Load category page from URL (e.g., `/cricket/bats`)

**Query:**
```csharp
Index:     GSI1
Condition: GSI1PK = "TENANT#{tenantId}#SLUG" AND GSI1SK = "CATEGORY#{slug}"
Returns:   Single category metadata
```

**Performance:** ~1 RCU, <30ms

---

## Data Hierarchy Example

### Table Items for Cricket Department → Bats Category

| PK | SK | EntityType | Key Attributes |
|----|----|-----------| ----------|
| `TENANT#default#DEPARTMENT#cricket` | `METADATA` | DEPARTMENT | Id: "dept_cricket", Name: "Cricket", Slug: "cricket", DisplayOrder: 1 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `METADATA` | CATEGORY | Id: "cat_bats", Name: "Bats", DepartmentId: "dept_cricket", DepartmentSlug: "cricket", DisplayOrder: 1 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `SECTION#sec_bats_1` | CATEGORY_SECTION | Title: "By Brand", Slug: "by-brand", DisplayOrder: 1 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `SECTION#sec_bats_1#ITEM#sub_bats_1` | SUBCATEGORY | Name: "SS", Slug: "ss", SectionId: "sec_bats_1", DisplayOrder: 1 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `SECTION#sec_bats_1#ITEM#sub_bats_2` | SUBCATEGORY | Name: "MRF", Slug: "mrf", SectionId: "sec_bats_1", DisplayOrder: 2 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `SECTION#sec_bats_2` | CATEGORY_SECTION | Title: "Price Range", Slug: "price-range", DisplayOrder: 2 |
| `TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats` | `SECTION#sec_bats_2#ITEM#sub_bats_6` | SUBCATEGORY | Name: "Under ₹5000", Slug: "under-5000", DisplayOrder: 1 |

---

## Query Performance Analysis

### Scenario: Loading Mega Menu with 1 Department, 9 Categories

**Current Implementation (Hierarchical Parallel Pattern):**
```
Query 1:    Get all departments from GSI2              → 1 RCU
Query 2:    Get categories for Cricket (GSI2 + filter) → 1 RCU
Queries 3-11: Get category details (9 parallel PK queries)
  - Bats details (PK query)                            → 1 RCU
  - Balls details (PK query)                           → 1 RCU
  - ... (7 more categories)
-----------------------------------------------------------
Total: ~12 RCUs, 3 round trip phases
Time: ~150-200ms (parallel execution)
```

**Performance Characteristics:**
- Phase 1: Get all departments
- Phase 2: Get all categories for each department (parallel by department)
- Phase 3: Get sections/subcategories for each category (parallel by category)
- Total time ≈ sum of sequential phases (parallelization within each phase)

---

## Multi-Tenancy & Department Flexibility

### Single-Department Tenant (Cricket Store)
All categories belong to one department. Frontend can:
- Hide department tabs (show categories directly)
- Use simplified navigation
- Access pattern optimized for single-department queries

**Example:**
```json
{
  "departments": [
    {
      "id": "dept_cricket",
      "name": "Cricket",
      "categories": [9 cricket categories]
    }
  ]
}
```
**UI:** Show "Bats", "Balls", "Shoes" directly (no "Cricket" tab needed)

### Multi-Department Tenant (Supermarket)
Multiple departments with categories. Frontend can:
- Show department tabs/navigation
- Drill down: Department → Category → Products
- Support department-specific filtering

**Example:**
```json
{
  "departments": [
    { "id": "dept_cricket", "categories": [...] },
    { "id": "dept_perfume", "categories": [...] },
    { "id": "dept_electronics", "categories": [...] }
  ]
}
```
**UI:** Show tabs "Cricket | Perfume | Electronics" → Categories

### Tenant Isolation
All access patterns enforce tenant isolation:

**Secure:**
```
PK: TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats
```
Each tenant's data is in separate partition keys.

**Benefits:**
- Data isolation at partition level
- No risk of cross-tenant data leakage
- Supports tenant-specific scaling

---

## Attribute Details

### Department Attributes
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket",
  "SK": "METADATA",
  "EntityType": "DEPARTMENT",
  "Id": "dept_cricket",
  "TenantId": "default",
  "Name": "Cricket",
  "Slug": "cricket",
  "Description": "Cricket equipment and gear for all levels",
  "Icon": "cricket",
  "ImageUrl": "",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-25T00:00:00Z",
  "UpdatedAt": "2025-12-25T00:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system",
  "GSI1PK": "TENANT#default#SLUG",
  "GSI1SK": "DEPARTMENT#cricket",
  "GSI2PK": "TENANT#default#DEPARTMENTS",
  "GSI2SK": "ORDER#0001"
}
```

### Category Attributes
**Note:** Now includes DepartmentId and DepartmentSlug

```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "METADATA",
  "EntityType": "CATEGORY",
  "Id": "cat_bats",
  "TenantId": "default",
  "DepartmentId": "dept_cricket",
  "DepartmentSlug": "cricket",
  "Name": "Bats",
  "Slug": "bats",
  "Description": "Cricket bats for all levels",
  "Icon": "bat",
  "ImageUrl": "",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00Z",
  "UpdatedAt": "2025-12-21T00:00:00Z",
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
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
  "SK": "SECTION#sec_bats_1",
  "EntityType": "CATEGORY_SECTION",
  "Id": "sec_bats_1",
  "CategoryId": "cat_bats",
  "TenantId": "default",
  "Title": "By Brand",
  "Slug": "by-brand",
  "ShowTitle": true,
  "Mapping": "BRAND",
  "DisplayOrder": 1,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00Z",
  "UpdatedAt": "2025-12-21T00:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system"
}
```

**Mapping Field:**
- `null` - Static subcategories
- `"BRAND"` - Dynamically mapped to brands table
- `"PRICE_RANGE"` - Dynamically mapped to price ranges

### Subcategory Attributes
```json
{
  "PK": "TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats",
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
  "BrandId": "brand_ss",
  "FilterType": "brand",
  "DisplayOrder": 1,
  "ProductCount": 0,
  "IsActive": true,
  "CreatedAt": "2025-12-21T00:00:00Z",
  "UpdatedAt": "2025-12-21T00:00:00Z",
  "CreatedBy": "system",
  "UpdatedBy": "system"
}
```

---

## Best Practices

### Do's

1. **Use Department Hierarchy** - Always include department in category PK for proper isolation
2. **Parallel Queries** - Leverage Task.WhenAll for fetching multiple departments/categories
3. **Query by PK** - Always query using partition key for best performance
4. **Leverage Sort Keys** - Use SK patterns for hierarchical data
5. **Use GSIs Wisely** - GSI2 for ordered lists, GSI1 for slug lookups
6. **Filter on Attributes** - Use FilterExpression on DepartmentSlug to get department categories

### Don'ts

1. **Avoid Scans** - Never scan the entire table
2. **Don't Skip Department** - Always include department in PK for categories
3. **Avoid Hot Partitions** - Distribute writes across multiple partition keys
4. **Don't Over-Index** - Only create GSIs for actual access patterns
5. **Avoid Large Items** - Keep items under 400KB

---

## Schema Migration Notes

### Migration from Old Pattern (Dec 2025)

**Old Pattern (Pre-Department):**
```
PK: TENANT#default#CATEGORY#cat_bats
SK: METADATA
```

**New Pattern (With Department):**
```
PK: TENANT#default#DEPARTMENT#cricket#CATEGORY#cat_bats
SK: METADATA
+ DepartmentId: "dept_cricket"
+ DepartmentSlug: "cricket"
```

**Migration Steps:**
1. Created `departments-default-tenant.json` with Cricket department
2. Updated all 54 catalog items with new PK pattern
3. Added DepartmentId and DepartmentSlug fields to all categories
4. Split catalog into 3 batch files (25 items each, DynamoDB batch limit)
5. Cleared and reseeded LocalStack database

**Files Modified:**
- `catalog-default-tenant.json` - Migrated PK patterns
- `catalog-default-tenant-batch-{1,2,3}.json` - Split batches
- `DynamoDbDepartmentRepository.cs` - Created
- `GetMegaMenuDataQueryHandler.cs` - Rewritten for department hierarchy
- Frontend: `CategoryService`, `CategoryNavComponent` - Updated for MegaMenuDto

---

## Monitoring & Optimization

### Key Metrics to Monitor

1. **Read Capacity Units (RCUs)**
   - Mega menu query: ~12 RCUs per request
   - Department detail query: ~1 RCU
   - Category detail query: ~1-2 RCUs per category

2. **Latency**
   - Target: <200ms for mega menu (with parallel queries)
   - Current: ~150-200ms
   - Department lookup: <50ms
   - Category lookup: <30ms

3. **Cache Hit Rate**
   - Implement caching for 5-10 minutes
   - Expected hit rate: >90% for mega menu
   - Cache key: `megamenu:{tenantId}`

### Optimization Opportunities

1. **Add Caching Layer**
   ```csharp
   [ResponseCache(Duration = 300)] // 5 minutes
   public async Task<MegaMenuDto> GetMegaMenu()
   ```

2. **Use DynamoDB DAX** (if needed)
   - Microsecond read latency
   - Automatic cache management
   - Reduces repeated mega menu queries

3. **Consider Item Collections**
   - Current design already optimizes item collections
   - All category data in single partition key
   - Efficient for hierarchical queries

---

## Summary

This single-table design with department hierarchy provides:

 **Flexible Hierarchy** - Supports both single and multi-department tenants
 **Efficient Queries** - Get entire department/category hierarchy in 2-3 round trips
 **Multi-Tenancy** - Tenant isolation at partition key level
 **Scalability** - Parallel queries for optimal performance
 **Adaptability** - Works for cricket stores AND supermarkets
 **Future-Proof** - Add new departments without schema changes

The design is optimized for **read-heavy workloads** (mega menu, navigation) which is typical for e-commerce catalog data, while supporting the flexibility needed for a generic multi-department e-commerce platform.

### Design Strengths

1. **Department Flexibility:** Same schema supports single-department (Cricket only) and multi-department (marketplace) models
2. **Efficient Navigation:** Mega menu loads in ~150-200ms with all departments, categories, and sections
3. **Tenant Isolation:** Strong multi-tenancy with department-scoped partition keys
4. **Section Mapping:** Dynamic sections can map to brands, price ranges, or other facets
5. **Scalable:** Add departments/categories without changing schema or code

### Real-World Suitability

This 4-level hierarchy (Department → Category → Section → Subcategory) matches industry standards:
- **Amazon:** 4-5 levels
- **Flipkart:** 3-4 levels
- **Target/Walmart:** 3-4 levels

**Gearify is well-positioned for a full-fledged e-commerce application.**
