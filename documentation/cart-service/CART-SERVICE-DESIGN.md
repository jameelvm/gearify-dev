# Cart Service Design Document

## 1. Current State Analysis

### Existing Implementation
The CartService has a basic foundation:

| Component | Status | Location |
|-----------|--------|----------|
| Cart Entity | Implemented | `Domain/Entities/Cart.cs` |
| CartItem Entity | Implemented | `Domain/Entities/Cart.cs` |
| ICartRepository | Implemented | `Infrastructure/Repositories/ICartRepository.cs` |
| RedisCartRepository | Implemented | `Infrastructure/Repositories/RedisCartRepository.cs` |
| AddToCartCommand | Implemented | `Application/Commands/AddToCartCommand.cs` |
| RemoveFromCartCommand | Implemented | `Application/Commands/RemoveFromCartCommand.cs` |
| ClearCartCommand | Implemented | `Application/Commands/ClearCartCommand.cs` |
| GetCartQuery | Implemented | `Application/Queries/GetCartQuery.cs` |
| CartController | Partial | `API/CartController.cs` |

### Current Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                      Cart Service                            │
├─────────────────────────────────────────────────────────────┤
│  API Layer          │  CartController (partial endpoints)   │
├─────────────────────────────────────────────────────────────┤
│  Application Layer  │  Commands: Add, Remove, Clear         │
│                     │  Queries: GetCart                      │
├─────────────────────────────────────────────────────────────┤
│  Domain Layer       │  Cart, CartItem entities              │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure     │  RedisCartRepository (7-day TTL)      │
└─────────────────────────────────────────────────────────────┘
```

### Issues Found
1. **ICartRepository not registered** in `Startup.cs` - DI will fail
2. **No UpdateQuantity endpoint** - can only add or remove items
3. **No GetCart endpoint** in controller
4. **No product validation** - accepts any product data without verification
5. **Price stored at add time** - no handling for price changes
6. **No guest cart support** - requires UserId
7. **No cart-to-order conversion** hooks

---

## 2. Proposed Enhancements

### 2.1 Storage Strategy

**Hybrid Approach: Redis + DynamoDB**

| Storage | Use Case | TTL |
|---------|----------|-----|
| Redis | Active session carts, fast access | 7 days |
| DynamoDB | Persistent carts for logged-in users, abandoned cart recovery | 30 days |

### 2.2 Cart Flow

```
                    ┌──────────────┐
                    │   Frontend   │
                    └──────┬───────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                      Cart Service                             │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    CartController                        │ │
│  │  POST /cart/{userId}/items      - Add item              │ │
│  │  PUT  /cart/{userId}/items/{id} - Update quantity       │ │
│  │  DELETE /cart/{userId}/items/{id} - Remove item         │ │
│  │  GET  /cart/{userId}            - Get cart              │ │
│  │  DELETE /cart/{userId}          - Clear cart            │ │
│  │  POST /cart/merge               - Merge guest cart      │ │
│  └─────────────────────────────────────────────────────────┘ │
│                           │                                   │
│                           ▼                                   │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              Product Validation (HTTP)                   │ │
│  │         Calls Catalog Service to verify product          │ │
│  └─────────────────────────────────────────────────────────┘ │
│                           │                                   │
│              ┌────────────┴────────────┐                     │
│              ▼                         ▼                     │
│  ┌───────────────────┐    ┌───────────────────────┐         │
│  │   Redis Cache     │    │   DynamoDB (backup)   │         │
│  │   (Primary)       │    │   (Persistent)        │         │
│  └───────────────────┘    └───────────────────────┘         │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. DynamoDB Table Design

### Table: `gearify-carts`

#### Primary Key Design
| Key | Pattern | Description |
|-----|---------|-------------|
| PK | `TENANT#{tenantId}#USER#{userId}` | Partition key per user per tenant |
| SK | `CART#METADATA` or `ITEM#{productId}` | Sort key for cart metadata vs items |

#### Access Patterns

| Access Pattern | Key Condition | Index |
|----------------|---------------|-------|
| Get user's cart (all items) | `PK = TENANT#X#USER#Y` | Primary |
| Get specific item | `PK = TENANT#X#USER#Y AND SK = ITEM#Z` | Primary |
| Find abandoned carts | `GSI1PK = TENANT#X#ABANDONED AND GSI1SK < timestamp` | GSI1 |
| Get carts by user across tenants | `GSI2PK = USER#Y` | GSI2 |

#### Entity Schemas

**Cart Metadata Record:**
```json
{
  "PK": "TENANT#tenant-123#USER#user-456",
  "SK": "CART#METADATA",
  "Id": "cart-789",
  "UserId": "user-456",
  "TenantId": "tenant-123",
  "ItemCount": 3,
  "TotalAmount": 299.97,
  "Currency": "USD",
  "Status": "active",
  "CreatedAt": "2024-01-15T10:30:00Z",
  "UpdatedAt": "2024-01-15T14:45:00Z",
  "ExpiresAt": "2024-02-14T10:30:00Z",
  "GSI1PK": "TENANT#tenant-123#STATUS#active",
  "GSI1SK": "2024-01-15T14:45:00Z",
  "GSI2PK": "USER#user-456",
  "GSI2SK": "TENANT#tenant-123",
  "TTL": 1707991800
}
```

**Cart Item Record:**
```json
{
  "PK": "TENANT#tenant-123#USER#user-456",
  "SK": "ITEM#product-001",
  "ProductId": "product-001",
  "ProductName": "SS Ton Cricket Bat",
  "Sku": "SS-TON-001",
  "Quantity": 2,
  "UnitPrice": 149.99,
  "LineTotal": 299.98,
  "ImageUrl": "https://...",
  "AddedAt": "2024-01-15T10:30:00Z",
  "UpdatedAt": "2024-01-15T14:45:00Z",
  "PriceAtAdd": 149.99,
  "CurrentPrice": 149.99,
  "ProductSlug": "ss-ton-cricket-bat",
  "Attributes": {
    "Size": "SH",
    "Weight": "2.8 lbs"
  }
}
```

#### GSI Definitions

| GSI | PK | SK | Purpose |
|-----|----|----|---------|
| GSI1 | `GSI1PK` | `GSI1SK` | Find carts by status (abandoned cart emails) |
| GSI2 | `GSI2PK` | `GSI2SK` | Find all carts for a user (cross-tenant) |

---

## 4. API Endpoints

### Complete API Specification

| Method | Endpoint | Description | Request Body |
|--------|----------|-------------|--------------|
| `GET` | `/api/cart/{userId}` | Get user's cart | - |
| `POST` | `/api/cart/{userId}/items` | Add item to cart | `AddItemRequest` |
| `PUT` | `/api/cart/{userId}/items/{productId}` | Update item quantity | `UpdateQuantityRequest` |
| `DELETE` | `/api/cart/{userId}/items/{productId}` | Remove item | - |
| `DELETE` | `/api/cart/{userId}` | Clear entire cart | - |
| `POST` | `/api/cart/merge` | Merge guest cart to user | `MergeCartRequest` |
| `GET` | `/api/cart/{userId}/validate` | Validate cart (prices, stock) | - |

### Request/Response Models

```csharp
// Add Item
public record AddItemRequest(
    string ProductId,
    int Quantity = 1
);

// Update Quantity
public record UpdateQuantityRequest(
    int Quantity
);

// Merge Cart (guest -> logged in user)
public record MergeCartRequest(
    string GuestCartId,
    string UserId,
    MergeStrategy Strategy = MergeStrategy.Combine
);

public enum MergeStrategy
{
    Combine,      // Add quantities together
    ReplaceGuest, // Keep user cart, discard guest
    ReplaceUser   // Keep guest cart, discard user
}

// Cart Response
public record CartResponse(
    string Id,
    string UserId,
    List<CartItemResponse> Items,
    decimal Subtotal,
    decimal? Discount,
    decimal Total,
    string Currency,
    int ItemCount,
    DateTime UpdatedAt
);

public record CartItemResponse(
    string ProductId,
    string ProductName,
    string Sku,
    string ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    bool IsAvailable,
    bool PriceChanged,
    decimal? OriginalPrice
);
```

---

## 5. Task Breakdown

### Phase 1: Fix Current Issues (Critical)

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 1.1 | Register `ICartRepository` in `Startup.cs` | Critical | Low |
| 1.2 | Add `GetCart` endpoint to controller | Critical | Low |
| 1.3 | Add `RemoveFromCart` endpoint to controller | Critical | Low |
| 1.4 | Add `ClearCart` endpoint to controller | Critical | Low |
| 1.5 | Add `appsettings.json` with Redis config | Critical | Low |

### Phase 2: Core Enhancements

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 2.1 | Create `UpdateCartItemCommand` + handler | High | Medium |
| 2.2 | Add `UpdateQuantity` endpoint | High | Low |
| 2.3 | Create `ICatalogServiceClient` for product validation | High | Medium |
| 2.4 | Validate product exists when adding to cart | High | Medium |
| 2.5 | Create `CartResponse` DTOs for API responses | High | Low |

### Phase 3: DynamoDB Integration

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 3.1 | Create `DynamoDbCartRepository` | Medium | Medium |
| 3.2 | Add DynamoDB configuration classes | Medium | Low |
| 3.3 | Update `Startup.cs` with DynamoDB setup | Medium | Low |
| 3.4 | Add cart table to `init-aws.sh` | Medium | Low |
| 3.5 | Implement write-through caching (Redis + DynamoDB) | Medium | High |

### Phase 4: Guest Cart & Merge

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 4.1 | Support anonymous cart (generate guest ID) | Medium | Medium |
| 4.2 | Create `MergeCartCommand` + handler | Medium | Medium |
| 4.3 | Add merge endpoint to controller | Medium | Low |
| 4.4 | Handle merge strategies (combine, replace) | Medium | Medium |

### Phase 5: Cart Events (SNS)

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 5.1 | Create `CartEvents` (ItemAdded, ItemRemoved, CartAbandoned) | Low | Low |
| 5.2 | Add `ISnsEventPublisher` integration | Low | Medium |
| 5.3 | Publish events on cart changes | Low | Low |
| 5.4 | Add SNS topic configuration | Low | Low |

### Phase 6: Cart Validation & Price Sync

| # | Task | Priority | Complexity |
|---|------|----------|------------|
| 6.1 | Create `ValidateCartQuery` + handler | Low | Medium |
| 6.2 | Check product availability on validation | Low | Medium |
| 6.3 | Detect price changes since item added | Low | Medium |
| 6.4 | Add validation endpoint | Low | Low |

---

## 6. Configuration

### appsettings.json
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "gearify-cart:"
  },
  "StorageConfiguration": {
    "DynamoDb": {
      "CartTableName": "gearify-carts"
    }
  },
  "CatalogService": {
    "BaseUrl": "http://localhost:5001"
  },
  "CartConfiguration": {
    "DefaultExpirationDays": 30,
    "GuestCartExpirationDays": 7,
    "MaxItemsPerCart": 50,
    "MaxQuantityPerItem": 99
  }
}
```

---

## 7. Recommended Implementation Order

```
Week 1: Phase 1 (Fix critical issues) + Phase 2.1-2.2 (Update quantity)
Week 2: Phase 2.3-2.5 (Product validation, DTOs)
Week 3: Phase 3 (DynamoDB integration)
Week 4: Phase 4 (Guest cart & merge)
Future: Phase 5-6 (Events, validation)
```

---

## 8. Questions to Consider

1. **Guest Cart Identification**: Cookie-based ID vs localStorage vs session?
2. **Stock Reservation**: Should adding to cart reserve inventory?
3. **Price Lock**: Lock price at add time or always use current price?
4. **Cart Limits**: Max items? Max quantity per item?
5. **Multi-currency**: Support different currencies per tenant?
