# DynamoDB Table Design - Gearify Platform

This document provides a comprehensive overview of all DynamoDB tables used in the Gearify e-commerce platform, including their structure, access patterns, and design rationale.

## Table of Contents
1. [Design Principles](#design-principles)
2. [Users Table](#users-table)
3. [Products Table](#products-table)
4. [Orders Table](#orders-table)
5. [Tenants Table](#tenants-table)
6. [Feature Flags Table](#feature-flags-table)
7. [Best Practices](#best-practices)

---

## Design Principles

### Why Single-Table Design Pattern?
The Gearify platform uses **multiple specialized tables** instead of a pure single-table design for the following reasons:

1. **Service Isolation**: Each microservice owns its data domain
2. **Scalability**: Tables can be scaled independently based on access patterns
3. **Clear Boundaries**: Easier to understand and maintain service boundaries
4. **Flexibility**: Different tables can have different backup and retention policies

### Key DynamoDB Concepts

#### Primary Key (PK) and Sort Key (SK)
- **PK (Partition Key)**: Determines data distribution across partitions. Items with the same PK are stored together.
- **SK (Sort Key)**: Enables range queries and sorting within a partition. Creates a composite primary key with PK.

#### Global Secondary Indexes (GSI)
- **Purpose**: Enable additional query patterns beyond the primary key
- **Trade-off**: Consume additional storage and write capacity, but enable efficient queries
- **Projection**: We use `ProjectionType: ALL` to include all attributes in the GSI

#### Why Use GSI Instead of Scan?
- **Performance**: GSI queries are O(log n) vs Scan which is O(n)
- **Cost**: GSI queries only read matched items vs Scan reads entire table
- **Scalability**: Scans don't scale well with table growth

---

## Users Table

**Table Name**: `gearify-users`
**Service**: Auth Service
**Purpose**: Store user accounts, authentication data, and profile information

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}#USER#{userId}"
  SK (String, RANGE): "METADATA"

GSI1 (Email Lookup):
  GSI1PK (HASH):  "TENANT#{tenantId}#EMAIL#{email}"
  GSI1SK (RANGE): "USER"

GSI2 (Refresh Token Lookup):
  GSI2PK (HASH):  "REFRESH_TOKEN#{hashedToken}"
  GSI2SK (RANGE): "TENANT#{tenantId}"
```

### Item Schema

```json
{
  "PK": "TENANT#default#USER#usr_abc123",
  "SK": "METADATA",
  "GSI1PK": "TENANT#default#EMAIL#john@example.com",
  "GSI1SK": "USER",
  "GSI2PK": "REFRESH_TOKEN#xyz789...",
  "GSI2SK": "TENANT#default",

  "userId": "usr_abc123",
  "tenantId": "default",
  "email": "john@example.com",
  "passwordHash": "$2a$12$...",
  "firstName": "John",
  "lastName": "Doe",
  "phone": "+1234567890",
  "role": "Customer",
  "isActive": true,
  "emailVerified": true,
  "refreshToken": "xyz789...",
  "refreshTokenExpiry": "2025-10-30T12:00:00Z",
  "createdAt": "2025-10-23T12:00:00Z",
  "updatedAt": "2025-10-23T12:00:00Z",
  "lastLoginAt": "2025-10-23T12:00:00Z"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get user by ID | Query | PK = `TENANT#{tenantId}#USER#{userId}` | Load user profile, verify permissions |
| Find user by email | Query GSI1 | GSI1PK = `TENANT#{tenantId}#EMAIL#{email}` | Login, check if email exists |
| Validate refresh token | Query GSI2 | GSI2PK = `REFRESH_TOKEN#{token}` | Token refresh flow |
| List users in tenant | Query | PK begins_with `TENANT#{tenantId}#USER#` | Admin user management |
| Update user profile | UpdateItem | PK + SK | Profile updates |
| Deactivate user | UpdateItem | PK + SK, set `isActive=false` | Account suspension |

### Design Rationale

#### Why PK = "TENANT#{tenantId}#USER#{userId}"?
1. **Tenant Isolation**: All users for a tenant are co-located, enabling efficient tenant-wide operations
2. **Deterministic Access**: Given userId and tenantId, we can directly access the item without a query
3. **Partition Distribution**: Users are distributed across partitions by tenant and userId

#### Why GSI1 for Email Lookup?
- **Login Flow**: Users log in with email/password, so we need fast email → user lookups
- **Uniqueness Check**: Before creating a user, check if email already exists in the tenant
- **Tenant Scoped**: Email is only unique within a tenant (multi-tenancy requirement)

#### Why GSI2 for Refresh Token?
- **Token Validation**: When a refresh token is presented, we need to find the associated user quickly
- **Security**: Tokens are hashed before storage, GSI2 indexes the hashed value
- **Expiration**: We can efficiently find and validate tokens without scanning

#### Why Not Store Sessions in DynamoDB?
- **Performance**: Session lookups are very frequent (every authenticated request)
- **Better Alternative**: Redis is used for session storage (sub-millisecond latency)
- **Cost**: Redis is more cost-effective for high-frequency, short-lived data

---

## Products Table

**Table Name**: `gearify-products`
**Service**: Catalog Service
**Purpose**: Store product catalog, categories, and inventory data

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}#PRODUCT#{productId}"
  SK (String, RANGE): "METADATA"

GSI1 (Category Lookup):
  GSI1PK (HASH):  "TENANT#{tenantId}#CATEGORY#{category}"
  GSI1SK (RANGE): "PRODUCT#{createdAt}"
```

### Item Schema

```json
{
  "PK": "TENANT#default#PRODUCT#prd_xyz123",
  "SK": "METADATA",
  "GSI1PK": "TENANT#default#CATEGORY#Electronics",
  "GSI1SK": "PRODUCT#2025-10-23T12:00:00Z",

  "productId": "prd_xyz123",
  "tenantId": "default",
  "name": "Wireless Mouse",
  "description": "Ergonomic wireless mouse with USB receiver",
  "category": "Electronics",
  "brand": "TechCorp",
  "price": 29.99,
  "currency": "USD",
  "stockQuantity": 150,
  "sku": "WM-2024-BLK",
  "images": [
    "https://s3.amazonaws.com/gearify-product-images/prd_xyz123_1.jpg"
  ],
  "attributes": {
    "color": "Black",
    "connectivity": "2.4GHz Wireless",
    "batteryLife": "12 months"
  },
  "tags": ["wireless", "ergonomic", "office"],
  "isActive": true,
  "createdAt": "2025-10-23T12:00:00Z",
  "updatedAt": "2025-10-23T12:00:00Z"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get product by ID | Query | PK = `TENANT#{tenantId}#PRODUCT#{productId}` | Product detail page |
| List products by category | Query GSI1 | GSI1PK = `TENANT#{tenantId}#CATEGORY#{category}` | Category browsing |
| Get recent products | Query GSI1 | GSI1PK starts with tenant, sort by GSI1SK | New arrivals page |
| Search products | ElasticSearch | - | Full-text search (delegated to Search Service) |
| Update inventory | UpdateItem | PK + SK | Stock level updates |
| Bulk product import | BatchWriteItem | - | Initial catalog setup |

### Design Rationale

#### Why PK = "TENANT#{tenantId}#PRODUCT#{productId}"?
1. **Direct Access**: Fast lookups when productId is known (cart, checkout, order processing)
2. **Tenant Isolation**: Products are partitioned by tenant for multi-tenancy
3. **Predictable Performance**: Even distribution across partitions

#### Why GSI1 for Category?
- **Browsing**: Users frequently browse by category ("Show me all Electronics")
- **Sorting**: GSI1SK includes timestamp for "newest first" sorting
- **Filtering**: Can efficiently filter by category without scanning entire table

#### Why Use ElasticSearch for Search?
- **Complex Queries**: Full-text search, fuzzy matching, relevance scoring
- **DynamoDB Limitation**: DynamoDB doesn't support text search natively
- **Architecture**: Search Service maintains an ElasticSearch index synced via DynamoDB Streams

#### Alternative Design Considered: Brand Index
- **Decision**: Not implemented as a separate GSI
- **Rationale**: Brand filtering is handled by Search Service
- **Trade-off**: Saves write capacity and storage, but requires Search Service for brand queries

---

## Orders Table

**Table Name**: `gearify-orders`
**Service**: Order Service
**Purpose**: Store order transactions, line items, and order history

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}#ORDER#{orderId}"
  SK (String, RANGE): "METADATA" or "ITEM#{lineItemId}"

GSI1 (User Orders):
  GSI1PK (HASH):  "TENANT#{tenantId}#USER#{userId}"
  GSI1SK (RANGE): "ORDER#{orderDate}"
```

### Item Schema

#### Order Header
```json
{
  "PK": "TENANT#default#ORDER#ord_abc123",
  "SK": "METADATA",
  "GSI1PK": "TENANT#default#USER#usr_xyz789",
  "GSI1SK": "ORDER#2025-10-23T12:00:00Z",

  "orderId": "ord_abc123",
  "tenantId": "default",
  "userId": "usr_xyz789",
  "status": "Pending",
  "totalAmount": 149.97,
  "currency": "USD",
  "shippingAddress": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001",
    "country": "USA"
  },
  "paymentMethod": "CreditCard",
  "paymentStatus": "Pending",
  "createdAt": "2025-10-23T12:00:00Z",
  "updatedAt": "2025-10-23T12:00:00Z"
}
```

#### Order Line Item
```json
{
  "PK": "TENANT#default#ORDER#ord_abc123",
  "SK": "ITEM#001",

  "lineItemId": "001",
  "productId": "prd_xyz123",
  "productName": "Wireless Mouse",
  "quantity": 2,
  "unitPrice": 29.99,
  "totalPrice": 59.98,
  "sku": "WM-2024-BLK"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get order with items | Query | PK = `TENANT#{tenantId}#ORDER#{orderId}` | Order details page |
| Get user's orders | Query GSI1 | GSI1PK = `TENANT#{tenantId}#USER#{userId}` | Order history page |
| Get recent orders | Query GSI1 | Sort by GSI1SK descending | Show recent orders first |
| Update order status | UpdateItem | PK + SK=METADATA | Order processing workflow |
| Add order line item | PutItem | PK + SK=ITEM# | During order creation |
| Calculate order total | Query | PK, aggregate SK=ITEM# | Order summary |

### Design Rationale

#### Why Store Order Header and Line Items Together?
1. **Single Partition**: All order data (header + items) is in one partition = single-digit millisecond access
2. **Atomic Operations**: Can use transactions to ensure order consistency
3. **Efficient Retrieval**: One query gets complete order with all line items

#### Why SK = "METADATA" vs "ITEM#{id}"?
- **Polymorphic Items**: Allows storing different entity types in same partition
- **Sort Key Prefix**: "METADATA" sorts before "ITEM#", so header comes first in query results
- **Query Efficiency**: Can query just metadata (SK = "METADATA") or everything (SK begins_with "")

#### Why GSI1 on User?
- **Order History**: Users need to see "My Orders" sorted by date
- **Customer Service**: Support needs to quickly find all orders for a user
- **Analytics**: Track purchasing patterns per user

#### Why Not Store Cart in DynamoDB?
- **High Churn**: Carts are frequently modified (add, remove, update quantity)
- **Temporary Data**: Most carts are abandoned and never convert to orders
- **Better Alternative**: Redis for cart data (faster, cheaper for ephemeral data)

#### Alternative Design: Status Index
- **Not Implemented**: No GSI for order status
- **Rationale**: Status-based queries ("all Pending orders") are admin/operational queries, handled by Order Service with filtering
- **Trade-off**: Saves write capacity, acceptable for low-frequency admin queries

---

## Tenants Table

**Table Name**: `gearify-tenants`
**Service**: Tenant Service
**Purpose**: Store multi-tenant configuration, branding, and settings

### Table Structure

```
Primary Key:
  tenantId (String, HASH): Unique tenant identifier
```

### Item Schema

```json
{
  "tenantId": "default",
  "name": "Default Store",
  "domain": "default.gearify.local",
  "plan": "Enterprise",
  "status": "Active",
  "settings": {
    "currency": "USD",
    "locale": "en-US",
    "timezone": "America/New_York",
    "taxRate": 0.08
  },
  "branding": {
    "logoUrl": "https://s3.amazonaws.com/gearify-tenant-assets/default/logo.png",
    "primaryColor": "#3B82F6",
    "secondaryColor": "#1E40AF"
  },
  "features": {
    "multiCurrency": true,
    "inventory": true,
    "analytics": true,
    "customDomain": true
  },
  "limits": {
    "maxProducts": 10000,
    "maxUsers": 1000,
    "maxOrders": 100000
  },
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-10-23T12:00:00Z"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get tenant config | GetItem | tenantId | Load tenant settings for request |
| List all tenants | Scan | - | Admin tenant management (infrequent) |
| Update tenant settings | UpdateItem | tenantId | Admin configuration changes |
| Check tenant status | GetItem | tenantId | Verify tenant is active before processing |

### Design Rationale

#### Why Simple Primary Key (No Sort Key)?
1. **Single Item per Tenant**: Each tenant has exactly one configuration record
2. **Direct Access**: GetItem is faster than Query for single-item retrieval
3. **Simplicity**: No need for complex key structure

#### Why No GSI?
- **Small Table**: Expected to have < 1000 tenants
- **Access Pattern**: 99.9% of accesses are by tenantId (direct GetItem)
- **Scan Acceptable**: Admin operations (list all tenants) are rare and can use Scan

#### Why Not Store Feature Flags Here?
- **Separation of Concerns**: Feature flags are dynamic and change frequently
- **Granularity**: Feature flags can be per-tenant, per-user, or per-feature
- **Dedicated Table**: `gearify-feature-flags` table allows for more complex flag management

#### Caching Strategy
- **Redis Cache**: Tenant configs are cached with 1-hour TTL
- **Rationale**: Tenant settings rarely change, caching reduces DynamoDB reads by 99%+
- **Invalidation**: Cache is invalidated when tenant settings are updated

---

## Feature Flags Table

**Table Name**: `gearify-feature-flags`
**Service**: Tenant Service
**Purpose**: Store feature flag configuration for progressive rollout and A/B testing

### Table Structure

```
Primary Key:
  tenantId (String, HASH): Tenant identifier
  flagKey (String, RANGE): Feature flag key
```

### Item Schema

```json
{
  "tenantId": "default",
  "flagKey": "enableNewCheckout",

  "enabled": true,
  "rolloutPercentage": 50,
  "targetUsers": ["usr_abc123", "usr_xyz789"],
  "targetRoles": ["Admin", "Beta"],
  "description": "New streamlined checkout flow",
  "metadata": {
    "owner": "checkout-team",
    "jiraTicket": "GEAR-1234"
  },
  "createdAt": "2025-10-01T00:00:00Z",
  "updatedAt": "2025-10-23T12:00:00Z"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get specific flag | GetItem | tenantId + flagKey | Check if feature is enabled |
| Get all flags for tenant | Query | tenantId | Load all feature flags at once |
| Update flag status | UpdateItem | tenantId + flagKey | Enable/disable feature |
| Create new flag | PutItem | tenantId + flagKey | Feature rollout initialization |

### Design Rationale

#### Why Composite Primary Key?
1. **Tenant Isolation**: Each tenant can have different flag values
2. **Efficient Queries**: Can get all flags for a tenant in one query
3. **Flag Management**: Easy to enable/disable features per tenant

#### Why No GSI?
- **Simple Access Pattern**: All queries are by tenantId or tenantId+flagKey
- **Small Data Set**: Each tenant has ~10-50 feature flags
- **Fast Queries**: Query by partition key (tenantId) is highly efficient

#### Progressive Rollout Support
- **Percentage Rollout**: `rolloutPercentage` enables gradual rollout (e.g., 10% of users)
- **Targeted Users**: `targetUsers` array for early access/beta testing
- **Role-Based**: `targetRoles` for role-specific features

#### Why Not Use External Service (LaunchDarkly, etc.)?
- **Cost**: External services charge per seat or flag evaluation
- **Control**: Full control over flag evaluation logic
- **Privacy**: Sensitive feature flags stay within infrastructure
- **Trade-off**: Less sophisticated than commercial products, but sufficient for needs

---

## Best Practices

### 1. Key Design Principles

#### Use Compound Keys for Hierarchy
```
Good:  PK = "TENANT#{tenantId}#USER#{userId}"
Bad:   PK = userId
```
**Why**: Enables tenant-scoped queries and ensures multi-tenant isolation

#### Include Entity Type in Keys
```
Good:  SK = "METADATA" vs "ITEM#001"
Bad:   SK = "1" vs "2"
```
**Why**: Makes items self-describing and enables polymorphic patterns

#### Use ISO 8601 Dates in Sort Keys
```
Good:  GSI1SK = "ORDER#2025-10-23T12:00:00Z"
Bad:   GSI1SK = "ORDER#1698073200"
```
**Why**: Human-readable and sortable lexicographically

### 2. GSI Design

#### When to Add a GSI
- Access pattern is frequent (> 10% of queries)
- Query cannot be satisfied by primary key
- Scan would be required otherwise
- Query latency matters for user experience

#### When NOT to Add a GSI
- Access pattern is rare (< 1% of queries)
- Admin-only queries (Scan is acceptable)
- Data can be cached effectively
- Alternative solutions exist (ElasticSearch, Redis)

#### GSI Projection Types
```
ALL:        Copy all attributes (used for all Gearify GSIs)
KEYS_ONLY:  Only keys (use when GSI is for existence checks)
INCLUDE:    Specify attributes (use when only few attributes needed)
```
**Gearify Choice**: `ProjectionType: ALL` for simplicity, acceptable storage cost

### 3. Attribute Naming

#### Consistent Prefixes
```
TENANT#    Tenant identifiers
USER#      User identifiers
PRODUCT#   Product identifiers
ORDER#     Order identifiers
ITEM#      Line items
```

#### Reserved Attribute Names
Avoid DynamoDB reserved words: `name`, `status`, `date`, `time`, `year`, etc.
**Solution**: Use specific names like `productName`, `orderStatus`, `createdAt`

### 4. Capacity Planning

#### On-Demand vs Provisioned
- **Gearify Choice**: Pay-per-request (On-Demand)
- **Rationale**: Unpredictable traffic patterns in development/testing
- **Production**: Consider provisioned capacity with auto-scaling

#### Write Capacity Considerations
- Each GSI doubles write cost (writes to table + GSI)
- Users table: 2 GSIs = 3x write cost
- Trade-off: Query performance > write cost for auth use case

### 5. Data Modeling Anti-Patterns to Avoid

#### ❌ Using Scan for Queries
```javascript
// Bad
const result = await dynamodb.scan({
  TableName: 'gearify-users',
  FilterExpression: 'email = :email'
}).promise();
```
**Problem**: Reads entire table, very expensive

```javascript
// Good
const result = await dynamodb.query({
  TableName: 'gearify-users',
  IndexName: 'GSI1',
  KeyConditionExpression: 'GSI1PK = :pk',
  ExpressionAttributeValues: {
    ':pk': `TENANT#${tenantId}#EMAIL#${email}`
  }
}).promise();
```

#### ❌ Hot Partitions
```javascript
// Bad: All users share same partition
PK = "USERS"
SK = userId
```
**Problem**: Single partition gets overwhelmed with traffic

```javascript
// Good: Users distributed across partitions
PK = `TENANT#{tenantId}#USER#{userId}`
SK = "METADATA"
```

#### ❌ Large Items
**Limit**: 400KB per item
**Bad**: Storing full order history in user item
**Good**: Separate orders into their own items/table

### 6. Testing Considerations

#### LocalStack Configuration
```bash
# Uses LocalStack for local DynamoDB
DYNAMODB_ENDPOINT=http://localhost:4566
```
**Benefits**:
- Fast local development
- No AWS charges
- Consistent with production DynamoDB API

#### Test Data Seeding
- Seed scripts in `gearify-umbrella/localstack/dynamodb/data/`
- Use `BatchWriteItem` for bulk test data (max 25 items)
- Include edge cases: empty attributes, max string lengths, special characters

---

## Future Enhancements

### Potential Additional Tables

#### 1. Shopping Cart Table (Maybe)
**Current**: Redis (ephemeral)
**Future**: DynamoDB for persistence across sessions
**Reason**: Allow users to resume carts across devices

#### 2. Product Reviews Table
```
PK: TENANT#{tenantId}#PRODUCT#{productId}
SK: REVIEW#{timestamp}
GSI1: TENANT#{tenantId}#USER#{userId} (user's reviews)
```

#### 3. Wishlist Table
```
PK: TENANT#{tenantId}#USER#{userId}
SK: WISHLIST_ITEM#{productId}
```

#### 4. Audit Log Table
```
PK: TENANT#{tenantId}#DATE#{date}
SK: LOG#{timestamp}#{eventType}
TTL: expireAt (auto-delete old logs)
```

### Advanced Features to Consider

#### DynamoDB Streams
- Enable streams on Users and Products tables
- Trigger Lambda for real-time indexing to ElasticSearch
- Publish events to EventBridge for microservices communication

#### Point-in-Time Recovery (PITR)
- Enable for all production tables
- Allows restore to any point in last 35 days
- Minimal performance impact

#### Global Tables
- Multi-region replication for disaster recovery
- Lower latency for global users
- Requires careful conflict resolution strategy

#### Time-to-Live (TTL)
- Add `expireAt` attribute for ephemeral data
- Automatic deletion of expired items
- Use for: sessions, temporary tokens, rate limiting

---

## Conclusion

The Gearify DynamoDB design balances:
- **Performance**: Fast queries via well-designed keys and GSIs
- **Cost**: Minimal GSIs, smart use of Redis for caching
- **Scalability**: Partition keys ensure good distribution
- **Maintainability**: Clear naming conventions and documentation

Each table is optimized for its specific access patterns while maintaining consistency across the platform. The design supports the current requirements and provides a foundation for future enhancements.

---

## References

- [DynamoDB Best Practices](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/best-practices.html)
- [Single-Table Design](https://www.alexdebrie.com/posts/dynamodb-single-table/)
- [DynamoDB GSI Best Practices](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/bp-indexes.html)
- [Multi-Tenant SaaS Storage Strategies](https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/storage.html)

**Document Version**: 1.0
**Last Updated**: 2025-10-23
**Maintained By**: Platform Team
