# Price Ranges Implementation - Tenant-Specific Configuration

## Overview

This implementation provides tenant-specific price range filtering stored in DynamoDB. Each tenant can have custom price ranges configured through an admin application.

## Architecture

### Database Design

**Table Name:** `gearify-price-ranges`

**Access Pattern:**
```
PK: TENANT#{tenantId}
SK: PRICERANGE#{guid}
```

**Attributes:**
- `Id` (String, GUID) - Unique identifier
- `TenantId` (String) - Tenant identifier
- `Label` (String) - Display label (e.g., "Under $50")
- `MinPrice` (Number) - Minimum price (inclusive)
- `MaxPrice` (Number, optional) - Maximum price (inclusive, null = no upper limit)
- `Currency` (String) - Currency code (e.g., "USD")
- `DisplayOrder` (Number) - Sort order
- `Category` (String, optional) - Category-specific ranges
- `IsActive` (Boolean) - Whether range is active
- `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` - Audit fields

## Implementation Details

### Backend (C#/.NET)

#### 1. Domain Entity
**File:** `gearify-catalog-svc/Domain/Entities/PriceRange.cs`
- Contains all price range properties
- Supports optional category filtering
- Includes audit trail fields

#### 2. Repository
**Files:**
- `Infrastructure/Repositories/IPriceRangeRepository.cs` - Interface
- `Infrastructure/Repositories/DynamoDbPriceRangeRepository.cs` - Implementation

**Methods:**
- `GetPriceRangesAsync(tenantId, category?)` - Get all active price ranges
- `GetByIdAsync(id, tenantId)` - Get single price range
- `CreateAsync(priceRange)` - Create new range
- `UpdateAsync(priceRange)` - Update existing range
- `DeleteAsync(id, tenantId)` - Delete price range

#### 3. Query Handler
**Files:**
- `Application/Queries/GetPriceRangesQuery.cs`
- `Application/Queries/GetPriceRangesQueryHandler.cs`

**Features:**
- Fetches price ranges for tenant
- Calculates product counts dynamically
- Returns sorted by DisplayOrder

#### 4. API Controller
**File:** `API/Controllers/PriceRangesController.cs`

**Endpoint:**
```
GET /api/catalog/price-ranges?category={category}
```

**Response:**
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "label": "Under $50",
    "minPrice": 0,
    "maxPrice": 50,
    "currency": "USD",
    "displayOrder": 1,
    "productCount": 172,
    "value": "0-50"
  }
]
```

#### 5. Dependency Injection
**File:** `Startup.cs:118`
```csharp
services.AddScoped<IPriceRangeRepository, DynamoDbPriceRangeRepository>();
```

### Frontend (Angular)

#### 1. Service
**File:** `gearify-web/src/app/core/services/price-range.service.ts`

**Methods:**
- `getPriceRanges(category?)` - Fetch price ranges from API

#### 2. Filter Component
**File:** `gearify-web/src/app/features/product/filter/filter.component.ts`

**Features:**
- Loads price ranges on initialization
- Displays loading/error states
- Shows dynamic product counts
- Uses Angular signals for reactive updates

**Signals:**
- `priceRanges()` - Current price ranges
- `priceRangesLoading()` - Loading state
- `priceRangesError()` - Error message

#### 3. API Constants
**File:** `gearify-web/src/app/shared/constants/api.constants.ts:19`
```typescript
PRICE_RANGES: '/api/catalog/price-ranges'
```

## Setup Instructions

### 1. Create DynamoDB Table

**Using Shell Script (Recommended):**
```bash
cd C:\Gearify
bash setup-price-ranges-table.sh
```

**Using AWS CLI Manually:**
```bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

aws dynamodb create-table \
    --table-name gearify-price-ranges \
    --attribute-definitions \
        AttributeName=PK,AttributeType=S \
        AttributeName=SK,AttributeType=S \
    --key-schema \
        AttributeName=PK,KeyType=HASH \
        AttributeName=SK,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

### 2. Seed Initial Data

The `setup-price-ranges-table.sh` script automatically seeds 5 default price ranges:

1. Under $50 (0-50)
2. $50-$100 (50-100)
3. $100-$250 (100-250)
4. $250-$500 (250-500)
5. $500 & Above (500+)

**Verify Data:**
```bash
aws dynamodb query \
    --table-name gearify-price-ranges \
    --key-condition-expression "PK = :pk AND begins_with(SK, :sk)" \
    --expression-attribute-values '{
        ":pk": {"S": "TENANT#default"},
        ":sk": {"S": "PRICERANGE#"}
    }' \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

### 3. Rebuild and Run Services

```bash
# Rebuild catalog service
cd gearify-catalog-svc
dotnet build

# Restart docker containers
cd ..
docker-compose restart catalog-svc

# Or rebuild completely
docker-compose up -d --build catalog-svc
```

### 4. Test Frontend

1. Navigate to `http://localhost:4200/products`
2. Click on the **Price** dropdown
3. You should see the price ranges with product counts
4. Check browser console for debug logs

## Adding Price Ranges for Other Tenants

To add price ranges for a different tenant (e.g., "acme"):

```bash
aws dynamodb put-item \
    --table-name gearify-price-ranges \
    --item '{
        "PK": {"S": "TENANT#acme"},
        "SK": {"S": "PRICERANGE#'$(uuidgen)'"},
        "Id": {"S": "'$(uuidgen)'"},
        "TenantId": {"S": "acme"},
        "Label": {"S": "Under $100"},
        "MinPrice": {"N": "0"},
        "MaxPrice": {"N": "100"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "1"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"},
        "UpdatedAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"},
        "CreatedBy": {"S": "admin"},
        "UpdatedBy": {"S": "admin"}
    }' \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

## Testing API Endpoints

### Get Price Ranges
```bash
curl -H "X-Tenant-Id: default" \
     http://localhost:8080/api/catalog/price-ranges
```

### Get Price Ranges for Category
```bash
curl -H "X-Tenant-Id: default" \
     http://localhost:8080/api/catalog/price-ranges?category=Electronics
```

## Future Enhancements

### Admin UI Integration
Once you have an admin application, you can add:

1. **Create Price Range**
   - Form with Label, Min/Max Price, Currency, Display Order
   - Optional category association

2. **Edit Price Range**
   - Update existing ranges
   - Activate/deactivate ranges

3. **Bulk Import**
   - Import price ranges from CSV
   - Clone ranges from another tenant

### Advanced Features
- **Currency Support:** Different price ranges per currency
- **Category-Specific Ranges:** Different ranges for different product categories
- **Seasonal Ranges:** Time-based price range activation
- **Analytics:** Track which ranges are used most for filtering

## Troubleshooting

### Price ranges not loading
1. Check browser console for errors
2. Verify table exists in DynamoDB
3. Verify tenant ID in localStorage matches seed data
4. Check API Gateway routing to catalog service

### Product counts showing 0
1. Verify products exist in `gearify-products` table
2. Check that products have valid `Price` attribute
3. Verify product `Brand` attribute matches brand IDs

### Empty response from API
1. Check `X-Tenant-Id` header is being sent
2. Verify price ranges exist for that tenant
3. Check `IsActive` flag is true
4. Review catalog service logs

## Files Modified/Created

### Backend
- ✅ `Domain/Entities/PriceRange.cs`
- ✅ `API/DTOs/PriceRangeDto.cs`
- ✅ `Infrastructure/Repositories/IPriceRangeRepository.cs`
- ✅ `Infrastructure/Repositories/DynamoDbPriceRangeRepository.cs`
- ✅ `Application/Queries/GetPriceRangesQuery.cs`
- ✅ `Application/Queries/GetPriceRangesQueryHandler.cs`
- ✅ `API/Controllers/PriceRangesController.cs`
- ✅ `Startup.cs` (DI registration)

### Frontend
- ✅ `core/services/price-range.service.ts`
- ✅ `shared/constants/api.constants.ts`
- ✅ `features/product/filter/filter.component.ts`
- ✅ `features/product/filter/filter.component.html`

### Setup Scripts
- ✅ `seed-price-ranges.json`
- ✅ `setup-price-ranges-table.sh`

## Summary

✅ **Tenant-specific price ranges** stored in DynamoDB
✅ **Dynamic product counts** calculated at runtime
✅ **Flexible configuration** via admin application
✅ **Category support** for category-specific ranges
✅ **Full CRUD operations** ready for admin UI
✅ **Frontend integration** with loading states and error handling
