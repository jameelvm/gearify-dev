# DynamoDB Table Design - Gearify Platform

This document provides a comprehensive overview of all DynamoDB tables used in the Gearify e-commerce platform, including their structure, access patterns, and design rationale.

## Table of Contents
1. [Design Principles](#design-principles)
2. [Users Table](#users-table)
3. [User Sessions Table](#user-sessions-table)
4. [MFA Codes Table](#mfa-codes-table)
5. [Products Table](#products-table)
6. [Orders Table](#orders-table)
7. [Tenants Table](#tenants-table)
8. [Feature Flags Table](#feature-flags-table)
9. [Best Practices](#best-practices)

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
  PK (String, HASH):  "TENANT#{tenantId}"
  SK (String, RANGE): "USER#{userId}"

GSI1 (Email Lookup):
  GSI1PK (HASH):  "TENANT#{tenantId}#EMAIL#{email}"
  GSI1SK (RANGE): "USER#{userId}"

GSI2 (Refresh Token Lookup):
  GSI2PK (HASH):  "TENANT#{tenantId}"
  GSI2SK (RANGE): "REFRESH#{refreshToken}"
```

### Item Schema

```json
{
  "PK": "TENANT#default",
  "SK": "USER#usr_abc123",
  "GSI1PK": "TENANT#default#EMAIL#john@example.com",
  "GSI1SK": "USER#usr_abc123",
  "GSI2PK": "TENANT#default",
  "GSI2SK": "REFRESH#xyz789...",

  "Id": "usr_abc123",
  "TenantId": "default",
  "Email": "john@example.com",
  "PasswordHash": "$2a$12$...",
  "FirstName": "John",
  "LastName": "Doe",
  "Phone": "+1234567890",
  "Role": "Customer",
  "IsActive": true,
  "EmailVerified": true,

  "RefreshToken": "xyz789...",
  "RefreshTokenExpiry": "2025-10-30T12:00:00Z",

  "EmailVerificationToken": "token_abc...",
  "EmailVerificationTokenExpiry": "2025-10-24T12:00:00Z",

  "PasswordResetToken": null,
  "PasswordResetTokenExpiry": null,
  "LastPasswordChangeAt": "2025-10-20T12:00:00Z",
  "PasswordHistory": ["$2a$12$old1...", "$2a$12$old2..."],

  "MfaEnabled": false,
  "PreferredMfaMethod": null,
  "TotpSecret": null,
  "BackupCodes": [],
  "LastMfaSetupAt": null,

  "FailedLoginAttempts": 0,
  "LockoutEnd": null,
  "LockoutEnabled": true,

  "ActiveSessionCount": 2,

  "CreatedAt": "2025-10-23T12:00:00Z",
  "UpdatedAt": "2025-10-23T12:00:00Z",
  "LastLoginAt": "2025-10-23T12:00:00Z"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get user by ID | GetItem | PK = `TENANT#{tenantId}`, SK = `USER#{userId}` | Load user profile, verify permissions |
| Find user by email | Query GSI1 | GSI1PK = `TENANT#{tenantId}#EMAIL#{email}` | Login, check if email exists |
| Validate refresh token | Query GSI2 | GSI2PK = `TENANT#{tenantId}`, SK begins_with `REFRESH#` | Token refresh flow |
| List users in tenant | Query | PK = `TENANT#{tenantId}`, SK begins_with `USER#` | Admin user management |
| Update user profile | UpdateItem | PK + SK | Profile updates |
| Deactivate user | UpdateItem | PK + SK, set `IsActive=false` | Account suspension |
| Find by email verification token | Scan | FilterExpression on `EmailVerificationToken` | Email verification (infrequent) |
| Find by password reset token | Scan | FilterExpression on `PasswordResetToken` | Password reset (infrequent) |

### Design Rationale

#### Why PK = "TENANT#{tenantId}" and SK = "USER#{userId}"?
1. **Tenant Isolation**: All users for a tenant are co-located in the same partition, enabling efficient tenant-wide operations
2. **Direct Access**: Given userId and tenantId, we can directly access the item with GetItem (faster than Query)
3. **Range Queries**: Can query all users in a tenant using `SK begins_with "USER#"`
4. **Partition Distribution**: Each tenant forms one partition, which works well for small-to-medium tenant sizes
5. **Potential Hot Partition Risk**: Large tenants (>1000 users) could create hot partitions, but acceptable for current scale

#### Why GSI1 for Email Lookup?
- **Login Flow**: Users log in with email/password, so we need fast email → user lookups
- **Uniqueness Check**: Before creating a user, check if email already exists in the tenant
- **Tenant Scoped**: Email is only unique within a tenant (multi-tenancy requirement)
- **Query Pattern**: GSI1PK includes both tenant and email for direct lookup

#### Why GSI2 for Refresh Token?
- **Token Validation**: When a refresh token is presented, we need to find the associated user quickly
- **Security**: Refresh tokens are stored in the user record and indexed for fast lookup
- **Tenant Scoped**: Tokens are scoped to tenant to prevent cross-tenant token reuse
- **Query Pattern**: Query by tenant + refresh token prefix for efficient lookups

#### Why Store Sessions in Separate Table?
- **Multiple Sessions**: Users can have multiple active sessions across different devices
- **Session Management**: Easy to list, revoke, or expire sessions independently
- **Dedicated Table**: `UserSessions` table (see below) provides better access patterns for session operations
- **Redis Alternative**: While Redis could be used for sessions, DynamoDB provides persistence and better multi-device tracking

#### Security Features Implementation
- **Password History**: Stores last 5 password hashes to prevent password reuse
- **Account Lockout**: Tracks failed login attempts and implements temporary lockout
- **MFA Support**: Stores MFA configuration (TOTP secret, backup codes, preferred method)
- **Token Management**: Separate tokens for email verification, password reset, and refresh
- **Session Tracking**: `ActiveSessionCount` helps monitor concurrent sessions per user

---

## User Sessions Table

**Table Name**: `UserSessions`
**Service**: Auth Service
**Purpose**: Store and manage user sessions across multiple devices for authentication and session tracking

### Table Structure

```
Primary Key:
  PK (String, HASH):  "USER#{userId}"
  SK (String, RANGE): "SESSION#{sessionId}"

No GSIs
```

### Item Schema

```json
{
  "PK": "USER#usr_abc123",
  "SK": "SESSION#ses_xyz789",

  "Id": "ses_xyz789",
  "UserId": "usr_abc123",
  "TenantId": "default",

  "RefreshToken": "rt_token123...",

  "DeviceInfo": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)...",
  "IpAddress": "192.168.1.100",
  "Location": "New York, NY, USA",

  "CreatedAt": "2025-11-26T10:00:00Z",
  "LastAccessedAt": "2025-11-26T14:30:00Z",
  "ExpiresAt": "2025-12-26T10:00:00Z",

  "IsActive": true
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get session by ID | GetItem | PK = `USER#{userId}`, SK = `SESSION#{sessionId}` | Validate specific session |
| Get all user sessions | Query | PK = `USER#{userId}` | List all sessions for a user |
| Get active sessions | Query | PK = `USER#{userId}`, filter `IsActive=true` | Show active devices to user |
| Find session by refresh token | Query + Filter | PK = `USER#{userId}`, filter on `RefreshToken` | Token-based session lookup (inefficient) |
| Update session activity | UpdateItem | PK + SK, update `LastAccessedAt` | Track session usage |
| Revoke session | UpdateItem | PK + SK, set `IsActive=false` | User logs out from device |
| Delete session | DeleteItem | PK + SK | Permanent session removal |
| Delete all user sessions | Query + BatchWriteItem | PK = `USER#{userId}`, delete all | Logout from all devices |
| Delete expired sessions | Query + Filter + Delete | Filter where `ExpiresAt < now()` | Cleanup expired sessions |

### Design Rationale

#### Why Separate Sessions Table?
1. **Multi-Device Support**: Users can have multiple active sessions (phone, laptop, tablet, etc.)
2. **Session Management**: Easy to list all active sessions and allow users to revoke specific devices
3. **Independent Lifecycle**: Sessions have different expiration and cleanup requirements than user records
4. **Audit Trail**: Tracks device info, IP address, and location for security monitoring
5. **Scalability**: Separates high-write session data from less frequently updated user data

#### Why PK = "USER#{userId}"?
- **User-Centric Access**: All sessions for a user are co-located for efficient queries
- **List All Sessions**: Single query retrieves all user sessions for "Active Devices" page
- **Revocation**: Easy to delete specific sessions or all sessions for a user

#### Why No GSI for Refresh Token?
- **Trade-off Decision**: Refresh token lookups typically include userId, making GSI unnecessary
- **Session Lookup Flow**: Auth service validates JWT (contains userId) → query sessions by userId
- **Cost Savings**: Avoid additional write capacity for GSI
- **Limitation**: Cannot efficiently lookup session by token alone (rare use case)

#### Session Expiration Strategy
- **ExpiresAt Attribute**: Stores absolute expiration timestamp
- **TTL Consideration**: Could enable DynamoDB TTL for automatic deletion of expired sessions
- **Manual Cleanup**: Currently uses scheduled job to delete expired sessions
- **Grace Period**: Sessions may persist briefly after expiration for audit purposes

#### Session Security Features
- **Device Fingerprinting**: Stores device info to detect session hijacking
- **IP Tracking**: Monitors IP changes for security alerts
- **Location Tracking**: Provides user-friendly session identification
- **Active Flag**: Allows soft deletion (revocation) while maintaining audit trail

---

## MFA Codes Table

**Table Name**: `MfaCodes`
**Service**: Auth Service
**Purpose**: Store temporary multi-factor authentication codes for two-factor authentication workflows

### Table Structure

```
Primary Key:
  PK (String, HASH):  "USER#{userId}"
  SK (String, RANGE): "MFACODE#{codeId}"

No GSIs
```

### Item Schema

```json
{
  "PK": "USER#usr_abc123",
  "SK": "MFACODE#mfa_xyz789",

  "Id": "mfa_xyz789",
  "UserId": "usr_abc123",
  "TenantId": "default",

  "CodeHash": "$2a$12$hashedCode...",
  "Method": "Email",

  "CreatedAt": "2025-11-26T10:00:00Z",
  "ExpiresAt": "2025-11-26T10:10:00Z",

  "IsUsed": false,
  "AttemptCount": 0,

  "Purpose": "login"
}
```

### Attributes

- **CodeHash**: BCrypt hashed verification code (never stores plain text)
- **Method**: Delivery method - `Email`, `SMS`, or `Authenticator`
- **Purpose**: Code purpose - `login`, `setup`, `disable`
- **IsUsed**: Prevents code reuse (one-time use only)
- **AttemptCount**: Tracks verification attempts for rate limiting
- **ExpiresAt**: Codes expire after 10 minutes (configurable)

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Create MFA code | PutItem | PK + SK | Generate code for 2FA challenge |
| Get code for verification | Query | PK = `USER#{userId}`, SK begins_with `MFACODE#` | Retrieve codes to verify user input |
| Get active codes | Query + Filter | PK = `USER#{userId}`, filter `IsUsed=false` AND `ExpiresAt > now()` | Find valid codes for user |
| Mark code as used | UpdateItem | PK + SK, set `IsUsed=true` | Prevent code reuse |
| Increment attempt count | UpdateItem | PK + SK, increment `AttemptCount` | Track failed attempts |
| Delete expired codes | Query + Filter + Delete | Filter where `ExpiresAt < now()` | Cleanup expired codes |
| Delete all user codes | Query + BatchWriteItem | PK = `USER#{userId}` | Clear codes on password change |

### Design Rationale

#### Why Separate MFA Codes Table?
1. **Temporary Data**: MFA codes are short-lived (10 minutes) and frequently created/deleted
2. **High Churn**: Separating from Users table reduces write load on the main user records
3. **Multiple Codes**: Users may have multiple concurrent codes (email + SMS backup, retries, etc.)
4. **Cleanup**: Easier to batch-delete expired codes without affecting user data
5. **Security**: Isolates sensitive verification codes from general user data

#### Why PK = "USER#{userId}"?
- **User-Centric**: All MFA codes for a user are co-located for efficient lookup
- **Verification Flow**: When user submits code, query all their active codes to find match
- **Bulk Operations**: Easy to delete all codes for a user (e.g., on password reset)

#### Why No GSI?
- **Simple Access Pattern**: Only need to query by userId
- **Small Data Set**: Each user typically has 1-2 active codes at a time
- **Cost Optimization**: No need for additional indexes

#### MFA Code Security
- **Hashed Storage**: Codes are BCrypt hashed, never stored in plain text
- **One-Time Use**: `IsUsed` flag ensures each code works only once
- **Rate Limiting**: `AttemptCount` prevents brute force attacks (max 3-5 attempts)
- **Time-Boxed**: Short expiration window (10 minutes) limits attack window
- **Purpose Scoping**: Codes are tied to specific purposes (login vs setup vs disable)

#### MFA Methods Supported
1. **Email**: Code sent to verified email address
2. **SMS**: Code sent via SMS (requires phone number)
3. **Authenticator**: TOTP code from app (validated against `TotpSecret` in Users table)

#### Code Generation Strategy
- **Format**: 6-digit numeric code (e.g., `123456`)
- **Randomization**: Cryptographically secure random number generator
- **Uniqueness**: Each code has unique ID even if numeric code repeats
- **Storage**: Only hash is stored, plain code is sent to user once and discarded

#### Cleanup Strategy
- **Automatic Expiration**: Codes expire after 10 minutes
- **Scheduled Cleanup**: Background job deletes expired codes hourly
- **TTL Consideration**: Could use DynamoDB TTL for automatic deletion
- **On-Demand Cleanup**: Codes deleted after successful use or when new code requested

---

## Products Table

**Table Name**: `gearify-products`
**Service**: Catalog Service
**Purpose**: Store product catalog, categories, and inventory data

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}"
  SK (String, RANGE): "PRODUCT#{productId}"

GSI1 (Category Lookup):
  GSI1PK (HASH):  "TENANT#{tenantId}#CATEGORY#{category}"
  GSI1SK (RANGE): "PRODUCT#{productId}"
```

### Item Schema

```json
{
  "PK": "TENANT#default-tenant",
  "SK": "PRODUCT#prd_xyz123",
  "GSI1PK": "TENANT#default-tenant#CATEGORY#Electronics",
  "GSI1SK": "PRODUCT#prd_xyz123",

  "Id": "prd_xyz123",
  "TenantId": "default-tenant",
  "Sku": "WM-2024-BLK",
  "Name": "Wireless Mouse",
  "Description": "Ergonomic wireless mouse with USB receiver",
  "Category": "Electronics",
  "Brand": "TechCorp",

  "Price": 29.99,
  "CompareAtPrice": 39.99,
  "Currency": "USD",

  "ImageUrls": [
    "https://s3.amazonaws.com/gearify-product-images/prd_xyz123_1.jpg",
    "https://s3.amazonaws.com/gearify-product-images/prd_xyz123_2.jpg"
  ],

  "Attributes": "{\"color\":\"Black\",\"connectivity\":\"2.4GHz Wireless\",\"batteryLife\":\"12 months\"}",

  "Tags": ["wireless", "ergonomic", "office"],

  "IsActive": true,

  "CreatedAt": "2025-10-23T12:00:00Z",
  "UpdatedAt": "2025-10-23T12:00:00Z",
  "CreatedBy": "usr_admin123",
  "UpdatedBy": "usr_admin123"
}
```

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get product by ID | GetItem | PK = `TENANT#{tenantId}`, SK = `PRODUCT#{productId}` | Product detail page |
| List all tenant products | Query | PK = `TENANT#{tenantId}`, SK begins_with `PRODUCT#` | Admin product management |
| List products by category | Query GSI1 | GSI1PK = `TENANT#{tenantId}#CATEGORY#{category}` | Category browsing |
| Search products | ElasticSearch | - | Full-text search (delegated to Search Service) |
| Create product | PutItem | PK + SK | Add new product to catalog |
| Update product | PutItem | PK + SK (upsert pattern) | Update product details |
| Delete product | DeleteItem | PK + SK | Remove product from catalog |
| Bulk product import | BatchWriteItem | - | Initial catalog setup |

### Design Rationale

#### Why PK = "TENANT#{tenantId}" and SK = "PRODUCT#{productId}"?
1. **Tenant Isolation**: All products for a tenant are co-located in the same partition
2. **Direct Access**: Fast GetItem lookups when productId is known (cart, checkout, order processing)
3. **List All Products**: Single query can retrieve all tenant products for admin views
4. **Consistent Pattern**: Matches the key structure used across all other tables

#### Why GSI1 for Category?
- **Browsing**: Users frequently browse by category ("Show me all Electronics")
- **Category Pages**: Efficient queries for category-specific product listings
- **Filtering**: Can efficiently filter by category without scanning entire table
- **Note**: GSI1SK uses productId instead of timestamp, so products are not chronologically sorted by default

#### Why Not Sort by CreatedAt in GSI1SK?
- **Current Design**: GSI1SK = `PRODUCT#{productId}` (not timestamp-based)
- **Impact**: Products within a category are not sorted by creation date
- **Consideration**: If "newest first" sorting is needed, could change to `PRODUCT#{createdAt}#{productId}`
- **Trade-off**: Current design is simpler but lacks chronological sorting within categories

#### Why Attributes as JSON String?
- **DynamoDB Map Limit**: DynamoDB Map types have nested attribute limitations
- **Flexibility**: JSON string allows arbitrary nested structures
- **Trade-off**: Cannot query or filter on individual attributes in DynamoDB (must use Search Service)
- **Serialization**: Attributes are serialized/deserialized in application code

#### Why Use ElasticSearch for Search?
- **Complex Queries**: Full-text search, fuzzy matching, relevance scoring
- **DynamoDB Limitation**: DynamoDB doesn't support text search natively
- **Multi-Field Search**: Search across name, description, brand, tags simultaneously
- **Architecture**: Search Service maintains an ElasticSearch index (synced separately)

#### Alternative Designs Considered

**Brand Index (Not Implemented)**
- **Decision**: Not implemented as a separate GSI
- **Rationale**: Brand filtering is handled by Search Service
- **Trade-off**: Saves write capacity and storage, but requires Search Service for brand queries

**Inventory Tracking (Simplified)**
- **Current**: Simple `StockQuantity` field (not shown in schema above, but available)
- **Alternative**: Separate Inventory table with detailed tracking (lot numbers, warehouses, etc.)
- **Decision**: Start simple, can migrate to dedicated Inventory Service if needed

---

## Orders Table

**Table Name**: `gearify-orders`
**Service**: Order Service
**Purpose**: Store order transactions, line items, and order history

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}"
  SK (String, RANGE): "ORDER#{orderId}"

GSI1 (User Orders):
  GSI1PK (HASH):  "TENANT#{tenantId}#USER#{userId}"
  GSI1SK (RANGE): "ORDER#{orderId}"
```

### Item Schema

```json
{
  "PK": "TENANT#default",
  "SK": "ORDER#ord_abc123",
  "GSI1PK": "TENANT#default#USER#usr_xyz789",
  "GSI1SK": "ORDER#ord_abc123",

  "Id": "ord_abc123",
  "TenantId": "default",
  "UserId": "usr_xyz789",

  "Status": "Pending",
  "TotalAmount": 149.97,
  "Currency": "USD",

  "Items": [
    {
      "ProductId": "prd_xyz123",
      "ProductName": "Wireless Mouse",
      "Quantity": 2,
      "Price": 29.99
    },
    {
      "ProductId": "prd_abc456",
      "ProductName": "Keyboard",
      "Quantity": 1,
      "Price": 89.99
    }
  ],

  "ShippingAddress": {
    "Street": "123 Main St",
    "City": "New York",
    "State": "NY",
    "ZipCode": "10001",
    "Country": "USA"
  },

  "PaymentId": "pay_xyz789",

  "CreatedAt": "2025-10-23T12:00:00Z",
  "UpdatedAt": "2025-10-23T12:00:00Z"
}
```

**Note**: Order line items are stored as a **JSON array** within the `Items` attribute, not as separate DynamoDB items.

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get order by ID | GetItem | PK = `TENANT#{tenantId}`, SK = `ORDER#{orderId}` | Order details page |
| Get user's orders | Query GSI1 | GSI1PK = `TENANT#{tenantId}#USER#{userId}` | Order history page |
| List all tenant orders | Query | PK = `TENANT#{tenantId}`, SK begins_with `ORDER#` | Admin order management |
| Create order | PutItem | PK + SK with Items array | Complete order in single write |
| Update order status | UpdateItem | PK + SK, update Status field | Order processing workflow |
| Update entire order | PutItem | PK + SK (upsert pattern) | Modify order details |

### Design Rationale

#### Why Store Line Items as Embedded JSON Array?
1. **Single Item**: Complete order (header + all line items) stored as one DynamoDB item
2. **Atomic Writes**: Create entire order in a single PutItem operation
3. **Fast Retrieval**: One GetItem returns complete order with all line items
4. **Simplicity**: No need to query multiple items or manage separate item relationships
5. **Trade-offs**:
   - Cannot query line items independently (e.g., "find all orders containing product X")
   - Item size limited to 400KB (acceptable for most orders with reasonable item counts)
   - Updating a single line item requires updating the entire Items array

#### Alternative Approach: Separate Line Items (Not Used)
The documentation initially suggested storing line items as separate DynamoDB items with SK = "ITEM#{id}":
- **Pros**: Can query line items independently, no item size concerns for large orders
- **Cons**: Multiple items per order, more complex queries, higher read costs
- **Decision**: Embedded JSON is simpler and sufficient for current e-commerce use case

#### Why PK = "TENANT#{tenantId}" and SK = "ORDER#{orderId}"?
1. **Tenant Isolation**: All orders for a tenant co-located in same partition
2. **Direct Access**: Fast GetItem lookups when orderId is known
3. **List All Orders**: Can query all tenant orders for admin dashboard
4. **Consistent Pattern**: Matches key structure across all tables

#### Why GSI1 on User?
- **Order History**: Users need to see "My Orders" page
- **Customer Service**: Support needs to quickly find all orders for a specific user
- **Analytics**: Track purchasing patterns per user
- **User-Centric Access**: Most common access pattern after direct order lookup

#### Why Not Sort Orders by Date in GSI1SK?
- **Current Design**: GSI1SK = `ORDER#{orderId}` (not timestamp-based)
- **Impact**: Orders are not chronologically sorted in "My Orders" view
- **Workaround**: Application sorts orders by CreatedAt after retrieval
- **Consideration**: Could change to `ORDER#{createdAt}#{orderId}` for chronological sorting
- **Trade-off**: Current design is simpler but requires client-side sorting

#### Why Not Store Cart in DynamoDB?
- **High Churn**: Carts are frequently modified (add, remove, update quantity)
- **Temporary Data**: Most carts are abandoned and never convert to orders
- **Better Alternative**: Redis for cart data (faster, cheaper for ephemeral data)
- **Current Implementation**: Cart Service uses Redis (see RedisCartRepository)

#### Alternative Designs Considered

**Status Index (Not Implemented)**
- **Potential GSI**: Index on order status for queries like "all Pending orders"
- **Decision**: Not implemented
- **Rationale**: Status-based queries are primarily admin/operational, can use Scan with filter for infrequent use
- **Trade-off**: Saves write capacity and GSI costs, acceptable for low-frequency admin queries

**Separate Payments Table (Not Used)**
- **Alternative**: Store payment details in separate gearify-payments table
- **Current**: Orders table stores PaymentId reference, Payment Service owns payment data
- **Decision**: Payment data lives in Payment Service (uses PostgreSQL, not DynamoDB)
- **Rationale**: Separation of concerns, payments have different compliance requirements (PCI-DSS)

---

## Tenants Table

**Table Name**: `gearify-tenants`
**Service**: Tenant Service
**Purpose**: Store multi-tenant configuration, branding, and settings

### Table Structure

```
Primary Key:
  PK (String, HASH):  "TENANT#{tenantId}"
  SK (String, RANGE): "TENANT#{tenantId}"

No GSIs
```

### Item Schema

```json
{
  "PK": "TENANT#default",
  "SK": "TENANT#default",

  "Id": "default",
  "Name": "Default Organization",

  "IsActive": true,

  "CreatedAt": "2025-01-01T00:00:00Z",
  "UpdatedAt": "2025-10-23T12:00:00Z",

  "CustomDomain": "default.gearify.local",
  "Plan": "Free",
  "MaxUsers": 10,
  "ContactEmail": "admin@default.gearify.local"
}
```

**Note**: The current implementation has a simplified schema compared to the originally documented design. Features like settings, branding, and detailed limits are not yet implemented.

### Access Patterns

| Access Pattern | Method | Keys Used | Use Case |
|---------------|--------|-----------|----------|
| Get tenant by ID | GetItem | PK = `TENANT#{tenantId}`, SK = `TENANT#{tenantId}` | Load tenant settings for request |
| List all tenants | Scan | - | Admin tenant management (infrequent) |
| Get active tenants | Scan + Filter | FilterExpression `IsActive=true` | List active tenants only |
| Create tenant | PutItem | PK + SK with ConditionExpression | Prevent duplicate tenant IDs |
| Update tenant | PutItem | PK + SK (upsert) | Modify tenant configuration |
| Soft delete tenant | UpdateItem | PK + SK, set `IsActive=false` | Deactivate tenant |

### Design Rationale

#### Why PK = SK = "TENANT#{tenantId}"?
1. **Single Item per Tenant**: Each tenant has exactly one configuration record
2. **Direct Access**: GetItem is faster than Query for single-item retrieval
3. **Consistent Pattern**: Uses composite key structure even though both keys are identical
4. **Future Flexibility**: Could add related items with same PK but different SK (e.g., tenant metadata, settings)

**Note**: This differs from the originally documented design which suggested a simple hash key. The actual implementation uses the composite PK/SK pattern for consistency with other tables.

#### Why No GSI?
- **Small Table**: Expected to have < 1000 tenants
- **Access Pattern**: 99.9% of accesses are by tenantId (direct GetItem)
- **Scan Acceptable**: Admin operations (list all tenants) are rare and can use Scan
- **Cost Optimization**: No need for additional indexes

#### Why Not Store Feature Flags Here?
- **Separation of Concerns**: Feature flags are dynamic and change frequently
- **Granularity**: Feature flags can be per-tenant, per-user, or per-feature
- **Dedicated Table**: `gearify-feature-flags` table allows for more complex flag management
- **Independent Evolution**: Feature flag system can evolve independently of tenant config

#### Caching Strategy (Planned)
- **Future Enhancement**: Tenant configs could be cached with Redis (1-hour TTL)
- **Rationale**: Tenant settings rarely change, caching could reduce DynamoDB reads by 99%+
- **Invalidation**: Cache would be invalidated when tenant settings are updated
- **Current State**: Caching not yet implemented in Tenant Service

#### Simplified Schema
The current implementation has a minimal schema with only essential fields:
- **Current**: Id, Name, IsActive, Plan, MaxUsers, ContactEmail, CustomDomain
- **Planned**: Settings (currency, locale, timezone), Branding (logo, colors), Limits (detailed quotas)
- **Decision**: Start with MVP, add features as needed

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

## Summary of DynamoDB Tables

### Tables by Service

| Service | Tables | Purpose |
|---------|--------|---------|
| **Auth Service** | gearify-users<br>UserSessions<br>MfaCodes | User authentication, multi-device sessions, MFA codes |
| **Catalog Service** | gearify-products | Product catalog and inventory |
| **Order Service** | gearify-orders | Order transactions with embedded line items |
| **Tenant Service** | gearify-tenants<br>gearify-feature-flags | Multi-tenancy config and feature flags |
| **Cart Service** | _(uses Redis)_ | Shopping cart (ephemeral data) |
| **Payment Service** | _(uses PostgreSQL)_ | Payment transactions (PCI compliance) |

### Total Tables: 7 DynamoDB Tables

### Key Design Pattern: Tenant-Scoped Partitioning

All tables use a consistent partitioning pattern:
```
PK = "TENANT#{tenantId}"
SK = "ENTITY#{entityId}"
```

**Benefits**:
- Strong tenant isolation (data co-location)
- Efficient tenant-wide queries
- Consistent access patterns across services

**Trade-offs**:
- Potential hot partitions for large tenants (>1000 entities)
- Requires tenant context for all queries

### GSI Usage Summary

| Table | GSI Count | Purpose |
|-------|-----------|---------|
| gearify-users | 2 | Email lookup, refresh token lookup |
| UserSessions | 0 | Simple user-centric queries |
| MfaCodes | 0 | Simple user-centric queries |
| gearify-products | 1 | Category browsing |
| gearify-orders | 1 | User order history |
| gearify-tenants | 0 | Small table, direct access only |
| gearify-feature-flags | 0 | Composite key suffices |

**GSI Philosophy**: Only create GSIs for frequent, performance-critical access patterns. Prefer Scan for infrequent admin queries.

### LocalStack Development Setup

**Infrastructure Location**: `C:\Gearify\gearify-umbrella\localstack\`

**Key Files**:
- `init-aws.sh`: Creates all DynamoDB tables on startup
- `dynamodb/tables/*.json`: Table definition files
- `dynamodb/data/*.json`: Seed data for local development
- `scripts/seed/seed-dynamodb.js`: Alternative Node.js seeder

**Configuration**:
- Endpoint: `http://localhost:4566`
- Region: `us-east-1`
- Billing Mode: `PAY_PER_REQUEST` (on-demand)

### Repository Implementation Locations

All DynamoDB repositories follow the pattern:
```
gearify-{service}-svc/
  ├── Domain/Entities/{Entity}.cs
  ├── Infrastructure/Repositories/DynamoDb{Entity}Repository.cs
  └── Application/Services/{Entity}Service.cs
```

### Common Attributes Across Tables

**Timestamps**: All tables include
- `CreatedAt` (ISO 8601 string)
- `UpdatedAt` (ISO 8601 string)

**Multi-tenancy**: All tables (except feature flags) include
- `Id` (entity identifier)
- `TenantId` (tenant identifier)
- `PK` containing tenant in key

**Soft Deletes**: Most tables use
- `IsActive` boolean for soft deletion

## Conclusion

The Gearify DynamoDB design balances:
- **Performance**: Fast queries via well-designed keys and GSIs
- **Cost**: Minimal GSIs (only 4 total across 7 tables), upsert patterns reduce writes
- **Scalability**: Tenant-based partitioning ensures good distribution for multi-tenant SaaS
- **Maintainability**: Consistent PK/SK patterns, clear naming conventions, comprehensive documentation
- **Flexibility**: Started simple (embedded line items, minimal tenant schema) with room to evolve

### Key Differences from Original Documentation

1. **Key Pattern**: Changed from `PK="TENANT#{tenantId}#ENTITY#{entityId}"` to `PK="TENANT#{tenantId}", SK="ENTITY#{entityId}"`
2. **Order Line Items**: Stored as embedded JSON array instead of separate DynamoDB items
3. **New Tables**: Added UserSessions and MfaCodes tables for auth features
4. **Simplified Schemas**: Tenants table has minimal fields (vs. comprehensive settings/branding)
5. **No Timestamp Sorting**: GSI sort keys use entity IDs instead of timestamps

### Areas for Future Enhancement

1. **Chronological Sorting**: Add timestamps to GSI sort keys for date-based ordering
2. **TTL Attributes**: Enable automatic cleanup of expired sessions and MFA codes
3. **Tenant Schema**: Expand tenant configuration with settings, branding, detailed limits
4. **Additional GSIs**: Consider adding if new access patterns emerge (e.g., order status index)
5. **Caching Layer**: Implement Redis caching for tenant configs and frequently accessed data
6. **DynamoDB Streams**: Enable streams for real-time indexing to ElasticSearch and event publishing

---

## References

- [DynamoDB Best Practices](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/best-practices.html)
- [Single-Table Design](https://www.alexdebrie.com/posts/dynamodb-single-table/)
- [DynamoDB GSI Best Practices](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/bp-indexes.html)
- [Multi-Tenant SaaS Storage Strategies](https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/storage.html)
- [LocalStack DynamoDB Documentation](https://docs.localstack.cloud/user-guide/aws/dynamodb/)

---

**Document Version**: 2.0
**Last Updated**: 2025-11-26
**Maintained By**: Platform Team
**Repository**: C:\Gearify
**Implementation Files**: See repository locations above
