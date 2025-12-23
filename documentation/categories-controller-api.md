# Categories Controller API Documentation

## Overview

The Categories Controller provides endpoints for managing and retrieving product categories in the Gearify e-commerce platform. It implements a hierarchical category structure with sections and subcategories, designed for building mega-menu navigation.

**Base URL**: `/api/catalog/categories`

**Authentication**: Required (X-Tenant-ID header)

**Architecture Pattern**: CQRS with MediatR, DTOs with Static Factory Methods

---

## Endpoints

### 1. Get All Categories

**Endpoint**: `GET /api/catalog/categories`

**Description**: Retrieves a flat list of all top-level categories for the current tenant.

**Use Case**:
- Building category dropdown menus
- Category listing pages
- Quick category lookups

**Request**:
```http
GET /api/catalog/categories HTTP/1.1
Host: localhost:5001
X-Tenant-ID: default
```

**Response** (`200 OK`):
```json
[
  {
    "id": "cat_bats",
    "name": "Bats",
    "slug": "bats",
    "description": "Cricket bats for all levels",
    "icon": "bat",
    "imageUrl": "",
    "displayOrder": 1,
    "isActive": true
  },
  {
    "id": "cat_balls",
    "name": "Balls",
    "slug": "balls",
    "description": "Cricket balls for all formats",
    "icon": "ball",
    "imageUrl": "",
    "displayOrder": 2,
    "isActive": true
  },
  {
    "id": "cat_protective_gear",
    "name": "Protective Gear",
    "slug": "protective-gear",
    "description": "Safety equipment for cricket",
    "icon": "helmet",
    "imageUrl": "",
    "displayOrder": 3,
    "isActive": true
  }
]
```

**Error Responses**:
- `400 Bad Request`: Missing X-Tenant-ID header
- `500 Internal Server Error`: Database or server error

---

### 2. Get Category with Details

**Endpoint**: `GET /api/catalog/categories/{categoryId}/details`

**Description**: Retrieves a specific category with all its sections and subcategories. This provides the complete hierarchical structure for a single category.

**Use Case**:
- Building category-specific mega menus
- Category detail pages
- Navigation sidebars

**Request**:
```http
GET /api/catalog/categories/cat_bats/details HTTP/1.1
Host: localhost:5001
X-Tenant-ID: default
```

**Response** (`200 OK`):
```json
{
  "category": {
    "id": "cat_bats",
    "name": "Bats",
    "slug": "bats",
    "description": "Cricket bats for all levels",
    "icon": "bat",
    "imageUrl": "",
    "displayOrder": 1,
    "isActive": true
  },
  "sections": [
    {
      "id": "sec_bats_1",
      "title": "By Brand",
      "slug": "by-brand",
      "showTitle": true,
      "displayOrder": 1,
      "items": [
        {
          "id": "sub_bats_1",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "SS",
          "slug": "ss",
          "description": "",
          "imageUrl": "",
          "displayOrder": 1,
          "productCount": 0,
          "isActive": true
        },
        {
          "id": "sub_bats_2",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "MRF",
          "slug": "mrf",
          "description": "",
          "imageUrl": "",
          "displayOrder": 2,
          "productCount": 0,
          "isActive": true
        }
      ]
    },
    {
      "id": "sec_bats_2",
      "title": "By Type",
      "slug": "by-type",
      "showTitle": true,
      "displayOrder": 2,
      "items": [
        {
          "id": "sub_bats_6",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_2",
          "name": "English Willow",
          "slug": "english-willow",
          "description": "",
          "imageUrl": "",
          "displayOrder": 1,
          "productCount": 0,
          "isActive": true
        },
        {
          "id": "sub_bats_7",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_2",
          "name": "Kashmir Willow",
          "slug": "kashmir-willow",
          "description": "",
          "imageUrl": "",
          "displayOrder": 2,
          "productCount": 0,
          "isActive": true
        }
      ]
    }
  ]
}
```

**Error Responses**:
- `400 Bad Request`: Missing X-Tenant-ID header
- `404 Not Found`: Category with specified ID not found
```json
{
  "error": "Category not found"
}
```
- `500 Internal Server Error`: Database or server error

---

### 3. Get Mega Menu Data

**Endpoint**: `GET /api/catalog/categories/mega-menu`

**Description**: Retrieves all categories with their complete hierarchical structure (sections and subcategories). This is the primary endpoint for building full mega-menu navigation.

**Use Case**:
- Main navigation mega menu
- Complete category tree visualization
- Category management dashboards

**Request**:
```http
GET /api/catalog/categories/mega-menu HTTP/1.1
Host: localhost:5001
X-Tenant-ID: default
```

**Response** (`200 OK`):
```json
[
  {
    "category": {
      "id": "cat_bats",
      "name": "Bats",
      "slug": "bats",
      "description": "Cricket bats for all levels",
      "icon": "bat",
      "imageUrl": "",
      "displayOrder": 1,
      "isActive": true
    },
    "sections": [
      {
        "id": "sec_bats_1",
        "title": "By Brand",
        "slug": "by-brand",
        "showTitle": true,
        "displayOrder": 1,
        "items": [
          {
            "id": "sub_bats_1",
            "categoryId": "cat_bats",
            "sectionId": "sec_bats_1",
            "name": "SS",
            "slug": "ss",
            "description": "",
            "imageUrl": "",
            "displayOrder": 1,
            "productCount": 0,
            "isActive": true
          },
          {
            "id": "sub_bats_2",
            "categoryId": "cat_bats",
            "sectionId": "sec_bats_1",
            "name": "MRF",
            "slug": "mrf",
            "description": "",
            "imageUrl": "",
            "displayOrder": 2,
            "productCount": 0,
            "isActive": true
          }
        ]
      }
    ]
  },
  {
    "category": {
      "id": "cat_balls",
      "name": "Balls",
      "slug": "balls",
      "description": "Cricket balls for all formats",
      "icon": "ball",
      "imageUrl": "",
      "displayOrder": 2,
      "isActive": true
    },
    "sections": [
      {
        "id": "sec_balls_1",
        "title": "By Type",
        "slug": "by-type",
        "showTitle": true,
        "displayOrder": 1,
        "items": [
          {
            "id": "sub_balls_1",
            "categoryId": "cat_balls",
            "sectionId": "sec_balls_1",
            "name": "Leather Balls",
            "slug": "leather-balls",
            "description": "",
            "imageUrl": "",
            "displayOrder": 1,
            "productCount": 0,
            "isActive": true
          }
        ]
      }
    ]
  }
]
```

**Error Responses**:
- `400 Bad Request`: Missing X-Tenant-ID header
- `500 Internal Server Error`: Database or server error

---

## Data Structure

### Hierarchical Category Model

```
Category (Top Level)
├── Section 1
│   ├── Subcategory 1.1
│   ├── Subcategory 1.2
│   └── Subcategory 1.3
├── Section 2
│   ├── Subcategory 2.1
│   └── Subcategory 2.2
└── Section 3
    └── Subcategory 3.1
```

**Example**: Cricket Equipment Store

```
Bats (Category)
├── By Brand (Section)
│   ├── SS (Subcategory)
│   ├── MRF (Subcategory)
│   └── Kookaburra (Subcategory)
├── By Type (Section)
│   ├── English Willow (Subcategory)
│   └── Kashmir Willow (Subcategory)
└── By Player Level (Section)
    ├── Professional (Subcategory)
    ├── Intermediate (Subcategory)
    └── Beginner (Subcategory)
```

---

## DTO Mapping

### Overview

The controller uses **Static Factory Methods** in DTOs to convert domain entities into API response DTOs. This pattern keeps mapping logic centralized and makes handlers thin.

### Mapping Architecture

```
Domain Entities (Database) → Static Factory Methods → DTOs (API Response)
```

---

### 1. Category → CategoryDto

**Domain Entity** (from DynamoDB):
```csharp
public class Category
{
    public string PK { get; set; }              // "TENANT#default#CATEGORY#cat_bats"
    public string SK { get; set; }              // "METADATA"
    public string EntityType { get; set; }      // "CATEGORY"
    public string Id { get; set; }              // "cat_bats"
    public string TenantId { get; set; }        // "default"
    public string Name { get; set; }            // "Bats"
    public string Slug { get; set; }            // "bats"
    public string Description { get; set; }     // "Cricket bats for all levels"
    public string Icon { get; set; }            // "bat"
    public string ImageUrl { get; set; }        // ""
    public int DisplayOrder { get; set; }       // 1
    public bool IsActive { get; set; }          // true
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string UpdatedBy { get; set; }
}
```

**Mapping Method**:
```csharp
public static CategoryDto FromEntity(Category category)
{
    return new CategoryDto(
        category.Id,           // Extract only business data
        category.Name,
        category.Slug,
        category.Description,
        category.Icon,
        category.ImageUrl,
        category.DisplayOrder,
        category.IsActive
    );
    // Note: PK, SK, TenantId, timestamps NOT exposed to API
}
```

**Resulting DTO**:
```json
{
  "id": "cat_bats",
  "name": "Bats",
  "slug": "bats",
  "description": "Cricket bats for all levels",
  "icon": "bat",
  "imageUrl": "",
  "displayOrder": 1,
  "isActive": true
}
```

**What's Hidden**:
- ❌ DynamoDB keys (PK, SK)
- ❌ EntityType (internal classification)
- ❌ TenantId (security - prevents leaking tenant info)
- ❌ Timestamps (not needed for display)
- ❌ Audit fields (CreatedBy, UpdatedBy)

---

### 2. Subcategory → SubcategoryDto

**Domain Entity** (from DynamoDB):
```csharp
public class Subcategory
{
    public string PK { get; set; }              // "TENANT#default#CATEGORY#cat_bats"
    public string SK { get; set; }              // "SECTION#sec_bats_1#ITEM#sub_bats_1"
    public string EntityType { get; set; }      // "SUBCATEGORY"
    public string Id { get; set; }              // "sub_bats_1"
    public string CategoryId { get; set; }      // "cat_bats"
    public string SectionId { get; set; }       // "sec_bats_1"
    public string TenantId { get; set; }        // "default"
    public string Name { get; set; }            // "SS"
    public string Slug { get; set; }            // "ss"
    public string Description { get; set; }     // ""
    public string ImageUrl { get; set; }        // ""
    public int DisplayOrder { get; set; }       // 1
    public int ProductCount { get; set; }       // 0
    public bool IsActive { get; set; }          // true
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Mapping Method**:
```csharp
public static SubcategoryDto FromEntity(Subcategory subcategory)
{
    return new SubcategoryDto(
        subcategory.Id,
        subcategory.CategoryId,    // Keep for client-side navigation
        subcategory.SectionId,     // Keep for grouping logic
        subcategory.Name,
        subcategory.Slug,
        subcategory.Description,
        subcategory.ImageUrl,
        subcategory.DisplayOrder,
        subcategory.ProductCount,  // Useful for "23 items" display
        subcategory.IsActive
    );
}
```

**Resulting DTO**:
```json
{
  "id": "sub_bats_1",
  "categoryId": "cat_bats",
  "sectionId": "sec_bats_1",
  "name": "SS",
  "slug": "ss",
  "description": "",
  "imageUrl": "",
  "displayOrder": 1,
  "productCount": 0,
  "isActive": true
}
```

---

### 3. Complex Mapping: CategoryWithDetailsDto

**Purpose**: Combines Category + Sections + Subcategories into hierarchical structure

**Input Data** (from repository):
```csharp
// Tuple returned from repository
(
    Category category,
    List<CategorySection> sections,
    List<Subcategory> subcategories
)
```

**Example Input Collections**:

**Category**:
```csharp
{
    Id = "cat_bats",
    Name = "Bats",
    Slug = "bats",
    // ... other fields
}
```

**Sections**:
```csharp
[
    { Id = "sec_bats_1", Title = "By Brand", DisplayOrder = 1 },
    { Id = "sec_bats_2", Title = "By Type", DisplayOrder = 2 }
]
```

**Subcategories** (flat list):
```csharp
[
    { Id = "sub_bats_1", SectionId = "sec_bats_1", Name = "SS", DisplayOrder = 1 },
    { Id = "sub_bats_2", SectionId = "sec_bats_1", Name = "MRF", DisplayOrder = 2 },
    { Id = "sub_bats_6", SectionId = "sec_bats_2", Name = "English Willow", DisplayOrder = 1 },
    { Id = "sub_bats_7", SectionId = "sec_bats_2", Name = "Kashmir Willow", DisplayOrder = 2 }
]
```

**Mapping Method** (Complex):
```csharp
public static CategoryWithDetailsDto FromEntities(
    Category category,
    List<CategorySection> sections,
    List<Subcategory> subcategories)
{
    // Step 1: Group subcategories by section and map to DTOs
    var sectionsWithItems = sections
        .Select(section => SectionWithItemsDto.FromEntity(section, subcategories))
        .OrderBy(s => s.DisplayOrder)
        .ToList();

    // Step 2: Combine into final structure
    return new CategoryWithDetailsDto(
        CategoryDto.FromEntity(category),
        sectionsWithItems
    );
}
```

**SectionWithItemsDto.FromEntity** (nested mapping):
```csharp
public static SectionWithItemsDto FromEntity(
    CategorySection section,
    List<Subcategory> allSubcategories)
{
    // Filter subcategories that belong to this section
    var items = allSubcategories
        .Where(sub => sub.SectionId == section.Id)  // ← Grouping logic
        .Select(SubcategoryDto.FromEntity)
        .OrderBy(sub => sub.DisplayOrder)           // ← Sorting logic
        .ToList();

    return new SectionWithItemsDto(
        section.Id,
        section.Title,
        section.Slug,
        section.ShowTitle,
        section.DisplayOrder,
        items
    );
}
```

**Transformation Flow**:

```
Flat Collections:
  Category: { id: "cat_bats", name: "Bats" }
  Sections: [
    { id: "sec_bats_1", title: "By Brand" },
    { id: "sec_bats_2", title: "By Type" }
  ]
  Subcategories: [
    { id: "sub_bats_1", sectionId: "sec_bats_1", name: "SS" },
    { id: "sub_bats_2", sectionId: "sec_bats_1", name: "MRF" },
    { id: "sub_bats_6", sectionId: "sec_bats_2", name: "English Willow" }
  ]

      ↓ FromEntities() - Groups and nests

Hierarchical DTO:
  {
    "category": { "id": "cat_bats", "name": "Bats" },
    "sections": [
      {
        "id": "sec_bats_1",
        "title": "By Brand",
        "items": [
          { "id": "sub_bats_1", "name": "SS" },
          { "id": "sub_bats_2", "name": "MRF" }
        ]
      },
      {
        "id": "sec_bats_2",
        "title": "By Type",
        "items": [
          { "id": "sub_bats_6", "name": "English Willow" }
        ]
      }
    ]
  }
```

---

## Handler Logic

### GetAllCategoriesQueryHandler

**Flow**:
```
1. Extract TenantId from context
2. Call repository.GetAllCategoriesAsync(tenantId)
3. Use CategoryDto.FromEntities() to map
4. Return List<CategoryDto>
```

**Code**:
```csharp
public async Task<List<CategoryDto>> Handle(...)
{
    var tenantId = _tenantContext.TenantId;
    var categories = await _repository.GetAllCategoriesAsync(tenantId);

    return CategoryDto.FromEntities(categories);
}
```

**Query Executed** (DynamoDB):
```
PK = "TENANT#default#CATEGORIES"
SK begins_with "CATEGORY#"
```

**Result Count**: Typically 5-15 categories

---

### GetCategoryWithDetailsQueryHandler

**Flow**:
```
1. Extract TenantId from context
2. Call repository.GetCategoryWithDetailsAsync(categoryId, tenantId)
3. Check if category exists (return null if not)
4. Use CategoryWithDetailsDto.FromEntities() to map and group
5. Return CategoryWithDetailsDto
```

**Code**:
```csharp
public async Task<CategoryWithDetailsDto?> Handle(...)
{
    var tenantId = _tenantContext.TenantId;
    var (category, sections, subcategories) =
        await _repository.GetCategoryWithDetailsAsync(request.CategoryId, tenantId);

    if (category is null or { Id: null })
        return null;

    return CategoryWithDetailsDto.FromEntities(category, sections, subcategories);
}
```

**Queries Executed** (DynamoDB):
```
Query 1: Get Category
  PK = "TENANT#default#CATEGORY#cat_bats"
  SK = "METADATA"

Query 2: Get Sections and Subcategories
  PK = "TENANT#default#CATEGORY#cat_bats"
  SK begins_with "SECTION#"
```

**Result**: 1 category + 2-5 sections + 5-20 subcategories

---

### GetMegaMenuData (Controller Method)

**Flow**:
```
1. Get all categories (GetAllCategoriesQuery)
2. For each category:
   a. Get details (GetCategoryWithDetailsQuery)
   b. Add to result list if not null
3. Return List<CategoryWithDetailsDto>
```

**Code**:
```csharp
public async Task<IActionResult> GetMegaMenuData()
{
    var categories = await _mediator.Send(new GetAllCategoriesQuery());
    var result = new List<CategoryWithDetailsDto>();

    foreach (var category in categories)
    {
        var categoryDetails = await _mediator.Send(
            new GetCategoryWithDetailsQuery(category.Id));

        if (categoryDetails != null)
        {
            result.Add(categoryDetails);
        }
    }

    return Ok(result);
}
```

**Performance Note**: This endpoint makes N+1 queries (1 for categories + 1 per category for details). For production, consider optimizing with a dedicated GetAllCategoriesWithDetailsQuery.

---

## Example Data Collection

### Complete Dataset Example

**Tenant**: `default`

**Categories**:
```json
[
  {
    "id": "cat_bats",
    "name": "Bats",
    "slug": "bats",
    "description": "Cricket bats for all levels",
    "icon": "bat",
    "imageUrl": "",
    "displayOrder": 1,
    "isActive": true
  },
  {
    "id": "cat_balls",
    "name": "Balls",
    "slug": "balls",
    "description": "Cricket balls for all formats",
    "icon": "ball",
    "imageUrl": "",
    "displayOrder": 2,
    "isActive": true
  },
  {
    "id": "cat_protective_gear",
    "name": "Protective Gear",
    "slug": "protective-gear",
    "description": "Safety equipment for cricket",
    "icon": "helmet",
    "imageUrl": "",
    "displayOrder": 3,
    "isActive": true
  },
  {
    "id": "cat_clothing",
    "name": "Clothing",
    "slug": "clothing",
    "description": "Cricket apparel and uniforms",
    "icon": "tshirt",
    "imageUrl": "",
    "displayOrder": 4,
    "isActive": true
  },
  {
    "id": "cat_footwear",
    "name": "Footwear",
    "slug": "footwear",
    "description": "Cricket shoes and spikes",
    "icon": "shoe",
    "imageUrl": "",
    "displayOrder": 5,
    "isActive": true
  },
  {
    "id": "cat_accessories",
    "name": "Accessories",
    "slug": "accessories",
    "description": "Cricket accessories and gear",
    "icon": "accessories",
    "imageUrl": "",
    "displayOrder": 6,
    "isActive": true
  },
  {
    "id": "cat_training_equipment",
    "name": "Training Equipment",
    "slug": "training-equipment",
    "description": "Equipment for cricket training",
    "icon": "training",
    "imageUrl": "",
    "displayOrder": 7,
    "isActive": true
  },
  {
    "id": "cat_team_kits",
    "name": "Team Kits",
    "slug": "team-kits",
    "description": "Complete team equipment sets",
    "icon": "team",
    "imageUrl": "",
    "displayOrder": 8,
    "isActive": true
  },
  {
    "id": "cat_deals",
    "name": "Deals",
    "slug": "deals",
    "description": "Special offers and deals",
    "icon": "deals",
    "imageUrl": "",
    "displayOrder": 9,
    "isActive": true
  }
]
```

**Category with Details Example** (cat_bats):
```json
{
  "category": {
    "id": "cat_bats",
    "name": "Bats",
    "slug": "bats",
    "description": "Cricket bats for all levels",
    "icon": "bat",
    "imageUrl": "",
    "displayOrder": 1,
    "isActive": true
  },
  "sections": [
    {
      "id": "sec_bats_1",
      "title": "By Brand",
      "slug": "by-brand",
      "showTitle": true,
      "displayOrder": 1,
      "items": [
        {
          "id": "sub_bats_1",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "SS",
          "slug": "ss",
          "description": "",
          "imageUrl": "",
          "displayOrder": 1,
          "productCount": 15,
          "isActive": true
        },
        {
          "id": "sub_bats_2",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "MRF",
          "slug": "mrf",
          "description": "",
          "imageUrl": "",
          "displayOrder": 2,
          "productCount": 12,
          "isActive": true
        },
        {
          "id": "sub_bats_3",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "SG",
          "slug": "sg",
          "description": "",
          "imageUrl": "",
          "displayOrder": 3,
          "productCount": 8,
          "isActive": true
        },
        {
          "id": "sub_bats_4",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "Kookaburra",
          "slug": "kookaburra",
          "description": "",
          "imageUrl": "",
          "displayOrder": 4,
          "productCount": 10,
          "isActive": true
        },
        {
          "id": "sub_bats_5",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_1",
          "name": "DSC",
          "slug": "dsc",
          "description": "",
          "imageUrl": "",
          "displayOrder": 5,
          "productCount": 6,
          "isActive": true
        }
      ]
    },
    {
      "id": "sec_bats_2",
      "title": "By Type",
      "slug": "by-type",
      "showTitle": true,
      "displayOrder": 2,
      "items": [
        {
          "id": "sub_bats_6",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_2",
          "name": "English Willow",
          "slug": "english-willow",
          "description": "Premium quality English willow bats",
          "imageUrl": "",
          "displayOrder": 1,
          "productCount": 25,
          "isActive": true
        },
        {
          "id": "sub_bats_7",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_2",
          "name": "Kashmir Willow",
          "slug": "kashmir-willow",
          "description": "Affordable Kashmir willow bats",
          "imageUrl": "",
          "displayOrder": 2,
          "productCount": 30,
          "isActive": true
        },
        {
          "id": "sub_bats_8",
          "categoryId": "cat_bats",
          "sectionId": "sec_bats_2",
          "name": "Tennis Ball Bats",
          "slug": "tennis-ball-bats",
          "description": "Bats designed for tennis ball cricket",
          "imageUrl": "",
          "displayOrder": 3,
          "productCount": 12,
          "isActive": true
        }
      ]
    }
  ]
}
```

---

## DynamoDB Storage Format

### Single-Table Design Pattern

All category data is stored in the `gearify-catalog` table using a single-table design pattern.

**Key Structure**:
```
PK (Partition Key): TENANT#{tenantId}#CATEGORY#{categoryId}
SK (Sort Key): Varies by entity type
```

### Entity Types

#### 1. Category Metadata
```
PK: "TENANT#default#CATEGORY#cat_bats"
SK: "METADATA"
EntityType: "CATEGORY"

Attributes:
  Id: "cat_bats"
  Name: "Bats"
  Slug: "bats"
  Description: "Cricket bats for all levels"
  Icon: "bat"
  ImageUrl: ""
  DisplayOrder: 1
  IsActive: true
  TenantId: "default"
  CreatedAt: "2025-12-21T00:00:00Z"
  UpdatedAt: "2025-12-21T00:00:00Z"
```

#### 2. Category Section
```
PK: "TENANT#default#CATEGORY#cat_bats"
SK: "SECTION#sec_bats_1"
EntityType: "CATEGORY_SECTION"

Attributes:
  Id: "sec_bats_1"
  CategoryId: "cat_bats"
  Title: "By Brand"
  Slug: "by-brand"
  ShowTitle: true
  DisplayOrder: 1
  IsActive: true
  TenantId: "default"
```

#### 3. Subcategory
```
PK: "TENANT#default#CATEGORY#cat_bats"
SK: "SECTION#sec_bats_1#ITEM#sub_bats_1"
EntityType: "SUBCATEGORY"

Attributes:
  Id: "sub_bats_1"
  CategoryId: "cat_bats"
  SectionId: "sec_bats_1"
  Name: "SS"
  Slug: "ss"
  Description: ""
  ImageUrl: ""
  DisplayOrder: 1
  ProductCount: 15
  IsActive: true
  TenantId: "default"
```

### Query Patterns

**Pattern 1: Get All Categories**
```
GSI2PK = "TENANT#default#CATEGORIES"
GSI2SK begins_with "ORDER#"
```

**Pattern 2: Get Category with Details**
```
PK = "TENANT#default#CATEGORY#cat_bats"
```
Returns: Category metadata + all sections + all subcategories (single query)

**Pattern 3: Get Single Category**
```
PK = "TENANT#default#CATEGORY#cat_bats"
SK = "METADATA"
```

---

## Client Integration Examples

### Angular Service

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private baseUrl = '/api/catalog/categories';

  constructor(private http: HttpClient) {}

  // Get all categories for dropdown/listing
  getAllCategories(): Observable<CategoryDto[]> {
    return this.http.get<CategoryDto[]>(this.baseUrl);
  }

  // Get category details for mega menu
  getCategoryDetails(categoryId: string): Observable<CategoryWithDetailsDto> {
    return this.http.get<CategoryWithDetailsDto>(
      `${this.baseUrl}/${categoryId}/details`
    );
  }

  // Get complete mega menu data
  getMegaMenuData(): Observable<CategoryWithDetailsDto[]> {
    return this.http.get<CategoryWithDetailsDto[]>(
      `${this.baseUrl}/mega-menu`
    );
  }
}
```

### React Hook

```typescript
import { useQuery } from 'react-query';
import axios from 'axios';

const BASE_URL = '/api/catalog/categories';

// Hook to fetch all categories
export const useCategories = () => {
  return useQuery('categories', async () => {
    const { data } = await axios.get<CategoryDto[]>(BASE_URL);
    return data;
  });
};

// Hook to fetch category details
export const useCategoryDetails = (categoryId: string) => {
  return useQuery(['category', categoryId], async () => {
    const { data } = await axios.get<CategoryWithDetailsDto>(
      `${BASE_URL}/${categoryId}/details`
    );
    return data;
  });
};

// Hook to fetch mega menu
export const useMegaMenu = () => {
  return useQuery('mega-menu', async () => {
    const { data } = await axios.get<CategoryWithDetailsDto[]>(
      `${BASE_URL}/mega-menu`
    );
    return data;
  });
};
```

---

## Performance Considerations

### Caching Strategy

**Recommended**: Cache mega-menu data on client side
```typescript
// Cache for 5 minutes
const { data } = useQuery('mega-menu', fetchMegaMenu, {
  staleTime: 5 * 60 * 1000,
  cacheTime: 10 * 60 * 1000
});
```

### Response Sizes

| Endpoint | Typical Size | Max Expected Size |
|----------|--------------|-------------------|
| GET /categories | ~2-5 KB | ~10 KB |
| GET /categories/{id}/details | ~5-15 KB | ~50 KB |
| GET /categories/mega-menu | ~50-100 KB | ~500 KB |

### Optimization Opportunities

1. **Add Caching Header**: Consider adding Cache-Control headers
2. **Compression**: Enable GZIP compression for JSON responses
3. **Batch Query**: Optimize mega-menu to use single database query
4. **CDN**: Cache mega-menu data at CDN edge for global users

---

## Testing

### cURL Examples

**Test 1: Get All Categories**
```bash
curl -X GET http://localhost:5001/api/catalog/categories \
  -H "X-Tenant-ID: default" \
  | jq .
```

**Test 2: Get Category Details**
```bash
curl -X GET http://localhost:5001/api/catalog/categories/cat_bats/details \
  -H "X-Tenant-ID: default" \
  | jq .
```

**Test 3: Get Mega Menu**
```bash
curl -X GET http://localhost:5001/api/catalog/categories/mega-menu \
  -H "X-Tenant-ID: default" \
  | jq . | head -100
```

**Test 4: Test 404 (Category Not Found)**
```bash
curl -X GET http://localhost:5001/api/catalog/categories/invalid_id/details \
  -H "X-Tenant-ID: default" \
  -i
```

### Postman Collection

Import this collection to test all endpoints:

```json
{
  "info": {
    "name": "Categories API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Get All Categories",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "X-Tenant-ID",
            "value": "default"
          }
        ],
        "url": {
          "raw": "{{baseUrl}}/api/catalog/categories",
          "host": ["{{baseUrl}}"],
          "path": ["api", "catalog", "categories"]
        }
      }
    },
    {
      "name": "Get Category Details",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "X-Tenant-ID",
            "value": "default"
          }
        ],
        "url": {
          "raw": "{{baseUrl}}/api/catalog/categories/cat_bats/details",
          "host": ["{{baseUrl}}"],
          "path": ["api", "catalog", "categories", "cat_bats", "details"]
        }
      }
    },
    {
      "name": "Get Mega Menu",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "X-Tenant-ID",
            "value": "default"
          }
        ],
        "url": {
          "raw": "{{baseUrl}}/api/catalog/categories/mega-menu",
          "host": ["{{baseUrl}}"],
          "path": ["api", "catalog", "categories", "mega-menu"]
        }
      }
    }
  ],
  "variable": [
    {
      "key": "baseUrl",
      "value": "http://localhost:5001"
    }
  ]
}
```

---

## Summary

The Categories Controller provides a clean, hierarchical API for managing product categories with:

✅ **3 endpoints** for different use cases
✅ **Static factory methods** for clean DTO mapping
✅ **Tenant isolation** via X-Tenant-ID header
✅ **Hierarchical data structure** (Category → Section → Subcategory)
✅ **DynamoDB single-table design** for efficient queries
✅ **CQRS pattern** with MediatR for separation of concerns

**Related Documentation**:
- [DynamoDB Single-Table Design](./dynamodb-single-table-design.md)
- [DTO Factory Methods Pattern](./dto-factory-methods.md)
- [CQRS Implementation Guide](./cqrs-implementation.md)
