# API Gateway Configuration Guide (appsettings.json)

Complete explanation of how the API Gateway routes requests to microservices using YARP (Yet Another Reverse Proxy).

---

## What is the API Gateway?

The **API Gateway** is the **single entry point** for all client requests. Instead of clients calling each microservice directly, they send all requests to the gateway, which routes them to the appropriate service.

### Without API Gateway (Problems):
```
Frontend → http://catalog-svc:5001/api/products     ❌ Clients need to know all URLs
Frontend → http://cart-svc:5002/api/cart            ❌ Multiple URLs to manage
Frontend → http://payment-svc:5005/api/payments     ❌ CORS for each service
Frontend → http://order-svc:5004/api/orders         ❌ Different auth for each
```

### With API Gateway (Better):
```
Frontend → http://api-gateway:5000/api/catalog/...   ✅ Single URL
         → http://api-gateway:5000/api/cart/...      ✅ One CORS config
         → http://api-gateway:5000/api/payments/...  ✅ Centralized auth
         → http://api-gateway:5000/api/orders/...    ✅ Rate limiting
                                 ↓
                         API Gateway routes internally
                                 ↓
                    catalog-svc, cart-svc, etc.
```

---

## Configuration Breakdown

Let's go through `appsettings.json` section by section:

---

## Section 1: Logging (Lines 2-7)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### What it does:
- **Default: Information** - Logs INFO level and above (INFO, WARNING, ERROR)
- **Microsoft.AspNetCore: Warning** - Reduces noise from ASP.NET Core framework (only warnings/errors)

### Log Levels (least to most severe):
1. **Trace** - Very detailed, for debugging
2. **Debug** - Debugging information
3. **Information** - General info (what we use)
4. **Warning** - Something unusual happened
5. **Error** - Something failed
6. **Critical** - Application is crashing

**Example logs you'll see:**
```
[Information] API Gateway starting...
[Warning] Service catalog-svc took 5s to respond
[Error] Failed to connect to payment-svc
```

---

## Section 2: AllowedHosts (Line 8)

```json
{
  "AllowedHosts": "*"
}
```

### What it does:
- **"*"** = Accept requests from any hostname
- Prevents host header injection attacks

### In Production, change to:
```json
{
  "AllowedHosts": "api.gearify.com;gearify.com"
}
```

---

## Section 3: ReverseProxy - The Core Configuration

This is where the magic happens! It has two main parts:
1. **Routes** - URL patterns to match
2. **Clusters** - Where to send matched requests

### Architecture:

```
Request comes in
       ↓
  Match Route (by URL pattern)
       ↓
  Find Cluster (destination service)
       ↓
  Forward request to backend service
       ↓
  Return response to client
```

---

## Section 3A: Routes (Lines 10-65)

Routes define **URL patterns** that the gateway watches for.

### Example: Catalog Route

```json
{
  "ReverseProxy": {
    "Routes": {
      "catalog-route": {                          // ← Route name (can be anything)
        "ClusterId": "catalog-cluster",           // ← Which cluster to forward to
        "Match": {
          "Path": "/api/catalog/{**catch-all}"    // ← URL pattern to match
        }
      }
    }
  }
}
```

### Let's break down the Path pattern:

**`/api/catalog/{**catch-all}`**

- **`/api/catalog/`** - Must start with this exact text
- **`{**catch-all}`** - Matches EVERYTHING after `/api/catalog/`
  - `**` = Catch-all parameter (greedy match)
  - Captures the rest of the URL

### Real-world examples:

| Client Request | Matches? | Captured by {**catch-all} |
|----------------|----------|---------------------------|
| `GET /api/catalog/products` | ✅ Yes | `products` |
| `GET /api/catalog/products/123` | ✅ Yes | `products/123` |
| `POST /api/catalog/products` | ✅ Yes | `products` |
| `GET /api/catalog/products/category/electronics` | ✅ Yes | `products/category/electronics` |
| `GET /api/cart/items` | ❌ No | - |
| `GET /products` | ❌ No | - |

### What happens when matched:

**Client sends:**
```
GET http://api-gateway:5000/api/catalog/products/123
```

**Gateway transforms to:**
```
GET http://catalog-svc:80/api/catalog/products/123
```

**Then forwards to Catalog Service**, which handles it normally.

---

## All Routes Explained

### 1. Catalog Route (Lines 11-16)
```json
"catalog-route": {
  "ClusterId": "catalog-cluster",
  "Match": { "Path": "/api/catalog/{**catch-all}" }
}
```

**Matches:**
- `/api/catalog/products`
- `/api/catalog/products/123`
- `/api/catalog/categories`

**Forwards to:** `http://catalog-svc:80/api/catalog/...`

---

### 2. Cart Route (Lines 17-22)
```json
"cart-route": {
  "ClusterId": "cart-cluster",
  "Match": { "Path": "/api/cart/{**catch-all}" }
}
```

**Matches:**
- `/api/cart/items`
- `/api/cart/add`
- `/api/cart/clear`

**Forwards to:** `http://cart-svc:80/api/cart/...`

---

### 3. Search Route (Lines 23-28)
```json
"search-route": {
  "ClusterId": "search-cluster",
  "Match": { "Path": "/api/search/{**catch-all}" }
}
```

**Matches:**
- `/api/search/products?q=laptop`
- `/api/search/products?category=electronics`

**Forwards to:** `http://search-svc:80/api/search/...`

---

### 4. Order Route (Lines 29-34)
```json
"order-route": {
  "ClusterId": "order-cluster",
  "Match": { "Path": "/api/orders/{**catch-all}" }
}
```

**Matches:**
- `/api/orders`
- `/api/orders/123`
- `/api/orders/user/abc`

**Forwards to:** `http://order-svc:80/api/orders/...`

---

### 5. Payment Route (Lines 35-40)
```json
"payment-route": {
  "ClusterId": "payment-cluster",
  "Match": { "Path": "/api/payments/{**catch-all}" }
}
```

**Matches:**
- `/api/payments/process`
- `/api/payments/123/refund`

**Forwards to:** `http://payment-svc:80/api/payments/...`

---

### (Similar pattern for Shipping, Inventory, Tenant, Media routes...)

---

## Section 3B: Clusters (Lines 66-130)

Clusters define **where** to send the requests (the actual backend services).

### Example: Catalog Cluster

```json
{
  "Clusters": {
    "catalog-cluster": {                    // ← Cluster name (referenced by route)
      "Destinations": {                     // ← One or more backend servers
        "catalog-destination": {            // ← Destination name (can be anything)
          "Address": "http://catalog-svc:80" // ← Actual service URL
        }
      }
    }
  }
}
```

### Understanding the Address:

**`http://catalog-svc:80`**

- **`http://`** - Protocol (not HTTPS in development)
- **`catalog-svc`** - Docker container name (from docker-compose)
- **`:80`** - Port inside the container

### Why "catalog-svc" and not "localhost"?

**In Docker networking:**
- ✅ `catalog-svc` - Docker container name (works)
- ❌ `localhost` - Would point to gateway itself (fails)

**When running locally (not in Docker):**
```json
"Address": "http://localhost:5001"  // Local development
```

---

## Complete Request Flow Example

Let's trace a real request from start to finish:

### Step-by-Step:

**1. Frontend makes request:**
```javascript
fetch('http://localhost:5000/api/catalog/products/123', {
  headers: { 'X-Tenant-Id': 'tenant1' }
})
```

**2. API Gateway receives:**
```
GET http://localhost:5000/api/catalog/products/123
Headers: X-Tenant-Id: tenant1
```

**3. Gateway matches route:**
```
Path: /api/catalog/products/123
Matches: "catalog-route" (/api/catalog/{**catch-all})
ClusterId: "catalog-cluster"
```

**4. Gateway finds cluster:**
```
Cluster: "catalog-cluster"
Destination: "catalog-destination"
Address: http://catalog-svc:80
```

**5. Gateway forwards request:**
```
GET http://catalog-svc:80/api/catalog/products/123
Headers: X-Tenant-Id: tenant1 (headers preserved)
```

**6. Catalog Service processes:**
```csharp
// In ProductsController.cs
[HttpGet("{id}")]
public async Task<IActionResult> GetProduct(string id) {
    // Receives id = "123"
    var product = await _mediator.Send(new GetProductByIdQuery(id, tenantId));
    return Ok(product);
}
```

**7. Catalog Service responds:**
```json
{
  "id": "123",
  "name": "Gaming Laptop",
  "price": 1299.99
}
```

**8. Gateway forwards response to frontend:**
```
200 OK
{
  "id": "123",
  "name": "Gaming Laptop",
  "price": 1299.99
}
```

**9. Frontend receives the data!** ✅

---

## Section 4: Cognito (Lines 132-137)

```json
{
  "Cognito": {
    "UserPoolId": "us-east-1_53b31cb045fd499d80dc09eabdcbf912",
    "ClientId": "rrd810v6eyejkdlhm0vf6q3dir",
    "Region": "us-east-1",
    "Authority": "http://localhost:4566"
  }
}
```

### What it does:
Configures **JWT authentication** using AWS Cognito (via LocalStack in development).

### Explanation:

- **UserPoolId** - Cognito user pool identifier (like a database of users)
- **ClientId** - Application identifier (which app is making requests)
- **Region** - AWS region (us-east-1)
- **Authority** - Where to validate JWTs (LocalStack for dev, real AWS for prod)

### How authentication works:

**1. User logs in:**
```javascript
// Frontend
const response = await cognito.login(username, password);
const token = response.accessToken; // JWT token
```

**2. Frontend sends token with requests:**
```javascript
fetch('http://localhost:5000/api/catalog/products', {
  headers: {
    'Authorization': 'Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...'
  }
})
```

**3. API Gateway validates token:**
```csharp
// In Program.cs
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options => {
        options.Authority = $"{cognitoAuthority}/{userPoolId}";
        // Gateway asks Cognito: "Is this token valid?"
    });
```

**4. If valid, request proceeds. If invalid, returns 401 Unauthorized.**

---

## Section 5: RateLimiting (Lines 138-141)

```json
{
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": 60
  }
}
```

### What it does:
Prevents abuse by limiting requests per tenant.

### Explanation:

- **PermitLimit: 100** - Maximum 100 requests
- **Window: 60** - Per 60 seconds (1 minute)

### How it works:

**Rate limiting is per tenant** (based on `X-Tenant-Id` header):

```
Tenant "tenant1" → Max 100 requests/minute
Tenant "tenant2" → Max 100 requests/minute (separate limit)
```

**What happens when limit exceeded:**
```
Response: 429 Too Many Requests
{
  "error": "Rate limit exceeded. Try again in 30 seconds."
}
```

**Configured in Program.cs:**
```csharp
builder.Services.AddRateLimiter(options => {
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
        var tenantId = context.Request.Headers["X-Tenant-Id"].ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(tenantId, _ =>
            new FixedWindowRateLimiterOptions {
                PermitLimit = 100,  // From appsettings.json
                Window = TimeSpan.FromSeconds(60)
            });
    });
});
```

---

## Common Scenarios

### Scenario 1: Add a New Service Route

Let's say you want to add a **Wishlist Service**.

**Step 1: Add route** (in `Routes` section):
```json
"wishlist-route": {
  "ClusterId": "wishlist-cluster",
  "Match": {
    "Path": "/api/wishlist/{**catch-all}"
  }
}
```

**Step 2: Add cluster** (in `Clusters` section):
```json
"wishlist-cluster": {
  "Destinations": {
    "wishlist-destination": {
      "Address": "http://wishlist-svc:80"
    }
  }
}
```

**Done!** Now requests to `/api/wishlist/*` go to `wishlist-svc`.

---

### Scenario 2: Multiple Destinations (Load Balancing)

If you have multiple instances of a service:

```json
"catalog-cluster": {
  "Destinations": {
    "catalog-1": {
      "Address": "http://catalog-svc-1:80"
    },
    "catalog-2": {
      "Address": "http://catalog-svc-2:80"
    },
    "catalog-3": {
      "Address": "http://catalog-svc-3:80"
    }
  },
  "LoadBalancingPolicy": "RoundRobin"  // Distribute requests evenly
}
```

YARP automatically balances requests across all 3 instances!

---

### Scenario 3: Path Transformation

Sometimes you want to change the URL:

**Client sends:**
```
GET /api/products/123
```

**But service expects:**
```
GET /catalog/products/123
```

**Configuration:**
```json
"catalog-route": {
  "ClusterId": "catalog-cluster",
  "Match": {
    "Path": "/api/products/{**catch-all}"
  },
  "Transforms": [
    { "PathPattern": "/catalog/products/{**catch-all}" }
  ]
}
```

---

## Local Development vs Docker

### When running locally (without Docker):

**Change cluster addresses to:**
```json
"catalog-cluster": {
  "Destinations": {
    "catalog-destination": {
      "Address": "http://localhost:5001"  // ← Local port
    }
  }
}
```

### When running in Docker (current config):

```json
"catalog-cluster": {
  "Destinations": {
    "catalog-destination": {
      "Address": "http://catalog-svc:80"  // ← Docker service name
    }
  }
}
```

### Use environment-specific configs:

**appsettings.Development.json:**
```json
{
  "ReverseProxy": {
    "Clusters": {
      "catalog-cluster": {
        "Destinations": {
          "catalog-destination": {
            "Address": "http://localhost:5001"
          }
        }
      }
    }
  }
}
```

**appsettings.Production.json:**
```json
{
  "ReverseProxy": {
    "Clusters": {
      "catalog-cluster": {
        "Destinations": {
          "catalog-destination": {
            "Address": "http://catalog-svc:80"
          }
        }
      }
    }
  }
}
```

---

## Testing the Gateway

### 1. Start all services:
```bash
docker-compose up -d
```

### 2. Test direct service access:
```bash
# Direct to Catalog Service (bypassing gateway)
curl http://localhost:5001/api/catalog/products
```

### 3. Test through gateway:
```bash
# Through API Gateway
curl http://localhost:5000/api/catalog/products
```

Both should return the same response!

### 4. Test routing to different services:
```bash
# Catalog
curl http://localhost:5000/api/catalog/products

# Cart
curl http://localhost:5000/api/cart/items

# Orders
curl http://localhost:5000/api/orders
```

---

## Common Issues & Solutions

### Issue 1: "404 Not Found" from gateway

**Cause:** Route pattern doesn't match

**Solution:** Check the path pattern in `Routes`:
```json
// Make sure your path matches the pattern
"Match": { "Path": "/api/catalog/{**catch-all}" }

// Request must start with: /api/catalog/
```

---

### Issue 2: "503 Service Unavailable"

**Cause:** Backend service is not running

**Solution:**
```bash
# Check if service is up
docker ps | grep catalog-svc

# Check gateway logs
docker logs gearify-api-gateway
```

---

### Issue 3: Gateway forwards but service returns 404

**Cause:** Service doesn't have a controller for that path

**Solution:** Check the backend service has the endpoint:
```csharp
// In catalog-svc/API/Controllers/ProductsController.cs
[HttpGet("products/{id}")]  // Must match the forwarded path
```

---

## Summary

### Key Concepts:

1. **Routes** = URL patterns to match (`/api/catalog/{**catch-all}`)
2. **Clusters** = Backend services to forward to (`http://catalog-svc:80`)
3. **Flow** = Client → Gateway (matches route) → Backend Service → Response

### The Big Picture:

```
┌─────────────┐
│   Frontend  │
│ (React/Vue) │
└──────┬──────┘
       │ All requests to: http://localhost:5000/api/...
       ↓
┌─────────────────────────────────────────┐
│        API Gateway (Port 5000)          │
│  • Routes requests by URL pattern       │
│  • Handles authentication (JWT)         │
│  • Rate limiting per tenant             │
│  • CORS configuration                   │
│  • Centralized logging/tracing          │
└─────────────────────────────────────────┘
       ↓        ↓        ↓        ↓
   Catalog   Cart   Payment   Order
   (5001)   (5002)  (5005)   (5004)
```

### Benefits:

✅ Single URL for clients (no CORS issues)
✅ Centralized authentication
✅ Rate limiting
✅ Easy to add/remove services
✅ Load balancing
✅ Request/response transformation
✅ Monitoring all traffic in one place

---

**Now you understand how the API Gateway routes all your microservices!** 🎉
