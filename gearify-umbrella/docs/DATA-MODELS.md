# Data Models

## DynamoDB Tables

### gearify-products
**Keys:**
- `tenantId` (HASH) - Tenant identifier
- `productId` (RANGE) - Unique product ID

**Attributes:**
- `name` (String) - Product name
- `category` (String) - bats, pads, gloves, balls, helmets
- `price` (Number) - Price in USD
- `weight` (Number) - Weight in grams
- `grade` (String) - Quality grade
- `weightType` (String) - Heavy, Medium, Light
- `stock` (Number) - Available quantity
- `addOns` (List) - ["Knocking", "Oiling", "Toe Binding"]
- `createdAt` (String) - ISO timestamp

**GSI:**
- ProductCategoryIndex: `tenantId` (HASH), `category` (RANGE)

### gearify-orders
**Keys:**
- `tenantId` (HASH)
- `orderId` (RANGE)

**Attributes:**
- `items` (List) - Order line items
- `total` (Number) - Total amount
- `status` (String) - pending, confirmed, shipped, delivered
- `paymentIntentId` (String) - Stripe/PayPal reference
- `shippingAddress` (Map)
- `createdAt` (String)
- `updatedAt` (String)

### gearify-tenants
**Keys:**
- `tenantId` (HASH)

**Attributes:**
- `name` (String)
- `domain` (String)
- `theme` (Map) - primaryColor, logoUrl, fontFamily
- `features` (Map) - Feature flags
- `createdAt` (String)

### gearify-feature-flags
**Keys:**
- `tenantId` (HASH)
- `flagKey` (RANGE)

**Attributes:**
- `enabled` (Boolean)
- `updatedAt` (String)

## PostgreSQL Tables

### payment_transactions
```sql
CREATE TABLE payment_transactions (
    id SERIAL PRIMARY KEY,
    tenant_id VARCHAR(50) NOT NULL,
    order_id VARCHAR(100) NOT NULL,
    payment_provider VARCHAR(20) NOT NULL, -- 'stripe' or 'paypal'
    payment_intent_id VARCHAR(255),
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    status VARCHAR(20) NOT NULL, -- 'pending', 'succeeded', 'failed'
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_payment_tenant ON payment_transactions(tenant_id);
CREATE INDEX idx_payment_order ON payment_transactions(order_id);
CREATE INDEX idx_payment_status ON payment_transactions(status);
```

## Redis Data Structures

### Feature Flags (String)
```
Key: feature:{tenantId}:{flagKey}
Value: "true" | "false"
TTL: None (persistent)
```

### Shopping Cart (Hash)
```
Key: cart:{sessionId}
Fields: {productId}: {quantity}
TTL: 7 days
```

### Session Data (String/JSON)
```
Key: session:{sessionId}
Value: JSON {userId, tenantId, ...}
TTL: 24 hours
```

### Product Cache (String/JSON)
```
Key: product:{tenantId}:{productId}
Value: JSON product object
TTL: 1 hour
```

## Event Schemas (SQS/SNS)

### OrderCreated
```json
{
  "eventType": "OrderCreated",
  "tenantId": "default",
  "orderId": "order-123",
  "userId": "user-456",
  "items": [...],
  "total": 299.99,
  "timestamp": "2025-01-15T10:30:00Z"
}
```

### PaymentSucceeded
```json
{
  "eventType": "PaymentSucceeded",
  "tenantId": "default",
  "orderId": "order-123",
  "paymentProvider": "stripe",
  "paymentIntentId": "pi_xxx",
  "amount": 299.99,
  "timestamp": "2025-01-15T10:31:00Z"
}
```

### ShipmentDispatched
```json
{
  "eventType": "ShipmentDispatched",
  "tenantId": "default",
  "orderId": "order-123",
  "trackingNumber": "TRK123456789",
  "carrier": "DHL",
  "timestamp": "2025-01-16T09:00:00Z"
}
```
