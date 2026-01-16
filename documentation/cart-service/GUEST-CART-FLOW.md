# Guest Cart Flow

## Overview

Guest carts allow anonymous users to add items to their cart without creating an account. When they decide to checkout and log in, their guest cart is merged with their user cart.

## Guest Cart Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           GUEST USER JOURNEY                                 │
└─────────────────────────────────────────────────────────────────────────────┘

1. USER VISITS SITE (Anonymous)
   ┌──────────────────┐
   │  No account      │
   │  No login        │
   │  Just browsing   │
   └──────────────────┘
           │
           ▼
2. CLICKS "ADD TO CART"
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  Frontend calls: POST /api/cart/guest/new                                │
   │                                                                          │
   │  Backend generates GUID: "a1b2c3d4-e5f6-7890-abcd-1234567890ab"         │
   │                                                                          │
   │  Frontend stores GUID in localStorage for subsequent requests            │
   └──────────────────────────────────────────────────────────────────────────┘
           │
           ▼
3. CART CREATED WITH GUEST ID (GUID)
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  POST /api/cart/{guestId}/items                                          │
   │  { "productId": "prod-001", "quantity": 1 }                              │
   │                                                                          │
   │  Cart stored with 7-day TTL (shorter than user carts)                    │
   └──────────────────────────────────────────────────────────────────────────┘
           │
           ▼
4. USER CONTINUES SHOPPING (Still anonymous)
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  Add more items, update quantities...                                    │
   │  Cart persists using the guest GUID                                      │
   └──────────────────────────────────────────────────────────────────────────┘
           │
           ▼
5. USER DECIDES TO CHECKOUT → LOGS IN
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  User now has real userId from JWT token                                 │
   │                                                                          │
   │  Problem: User may have TWO carts                                        │
   │  - Guest cart (GUID): Items added while anonymous                        │
   │  - User cart: Items from previous logged-in sessions                     │
   └──────────────────────────────────────────────────────────────────────────┘
           │
           ▼
6. MERGE CARTS
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  POST /api/cart/merge                                                    │
   │  {                                                                       │
   │    "guestCartId": "a1b2c3d4-e5f6-7890-abcd-1234567890ab",               │
   │    "userId": "user-123",                                                 │
   │    "strategy": "Combine"                                                 │
   │  }                                                                       │
   │                                                                          │
   │  Result: User's cart now contains items from both carts                  │
   │  Guest cart: DELETED                                                     │
   └──────────────────────────────────────────────────────────────────────────┘
```

## API Endpoints

### Generate Guest Cart ID

```
POST /api/cart/guest/new
X-Tenant-Id: {tenantId}

Response:
{
  "guestId": "a1b2c3d4-e5f6-7890-abcd-1234567890ab"
}
```

### Add Item to Guest Cart

```
POST /api/cart/{guestId}/items
X-Tenant-Id: {tenantId}

{
  "productId": "prod-001",
  "quantity": 1
}
```

### Merge Guest Cart to User Cart

```
POST /api/cart/merge
X-Tenant-Id: {tenantId}

{
  "guestCartId": "a1b2c3d4-e5f6-7890-abcd-1234567890ab",
  "userId": "user-123",
  "strategy": "Combine"
}
```

## Merge Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Combine** | Add guest items to user cart. If same product exists, sum the quantities. | Most common - keep everything |
| **ReplaceWithGuest** | Delete user cart, keep only guest cart items | User wants fresh start |
| **KeepUser** | Ignore guest cart, delete it, keep only user cart | User prefers their saved cart |

### Example: Combine Strategy

```
Guest Cart (a1b2c3d4-...):      User Cart (user-123):
├── Cricket Bat (qty: 1)        ├── Cricket Bat (qty: 2)
├── Tennis Ball (qty: 3)        └── Shoes (qty: 1)

After Merge (Combine):
User Cart (user-123):
├── Cricket Bat (qty: 3)    ← 1 + 2 combined
├── Tennis Ball (qty: 3)    ← from guest
└── Shoes (qty: 1)          ← from user

Guest Cart: DELETED
```

## Cart Expiration

| Cart Type | TTL | Reason |
|-----------|-----|--------|
| Guest Cart | 7 days | Anonymous users are less committed |
| User Cart | 30 days | Logged-in users are more valuable |

DynamoDB TTL automatically deletes expired carts.

## Frontend Integration

```javascript
// Check if user has a guest cart ID stored
let guestId = localStorage.getItem('guestCartId');

// If no guest ID and user is not logged in, generate one
if (!guestId && !isLoggedIn) {
  const response = await fetch('/api/cart/guest/new', {
    method: 'POST',
    headers: { 'X-Tenant-Id': tenantId }
  });
  const data = await response.json();
  guestId = data.guestId;
  localStorage.setItem('guestCartId', guestId);
}

// Use guestId or userId for cart operations
const cartId = isLoggedIn ? userId : guestId;
await fetch(`/api/cart/${cartId}/items`, { ... });

// On login, merge guest cart if exists
if (isLoggedIn && guestId) {
  await fetch('/api/cart/merge', {
    method: 'POST',
    headers: { 'X-Tenant-Id': tenantId },
    body: JSON.stringify({
      guestCartId: guestId,
      userId: userId,
      strategy: 'Combine'
    })
  });
  localStorage.removeItem('guestCartId');
}
```

## Data Flow

```
                    ┌─────────────────┐
                    │    Frontend     │
                    └────────┬────────┘
                             │
        ┌────────────────────┴────────────────────┐
        │                                         │
        ▼                                         ▼
┌───────────────────┐                   ┌───────────────────┐
│  Anonymous User   │                   │  Logged-in User   │
│  GUID stored in   │                   │  userId from      │
│  localStorage     │                   │  JWT token        │
└────────┬──────────┘                   └────────┬──────────┘
         │                                       │
         ▼                                       ▼
┌────────────────────────────────────────────────────────────┐
│                     Cart Service                            │
│  - Same endpoints for guest and user carts                  │
│  - Guest carts have shorter TTL (7 days)                    │
│  - Merge endpoint combines carts on login                   │
└────────────────────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────┐
│                  DynamoDB (gearify-carts)                   │
│                                                             │
│  Guest Cart:                                                │
│  PK: TENANT#acme#USER#a1b2c3d4-e5f6-...                    │
│  SK: CART#METADATA                                          │
│  TTL: 7 days                                                │
│                                                             │
│  User Cart:                                                 │
│  PK: TENANT#acme#USER#user-123                             │
│  SK: CART#METADATA                                          │
│  TTL: 30 days                                               │
└────────────────────────────────────────────────────────────┘
```
