# Debugging Gearify Microservices

Complete guide for debugging the Gearify microservices in Visual Studio and VS Code.

---

## Prerequisites

Before debugging, ensure you have:

1. **.NET 8 SDK** installed
2. **Docker Desktop** running (for LocalStack, Redis, PostgreSQL, etc.)
3. **VS Code** with C# Dev Kit extension OR **Visual Studio 2022**

### Start Infrastructure Services

Before debugging any microservice, start the required infrastructure:

```bash
# From C:\Gearify\gearify-umbrella directory
docker-compose up -d

# Verify services are running
docker-compose ps
```

Required services:
- ✅ LocalStack (port 4566) - DynamoDB, S3, SQS, SNS
- ✅ Redis (port 6379) - Cart sessions, caching
- ✅ PostgreSQL (port 5432) - Payment transactions
- ✅ Seq (port 5341) - Logging
- ✅ Jaeger (port 16686) - Tracing

---

## Option 1: Visual Studio 2022

### Debug Single Service

#### Step 1: Open Project
```
File → Open → Project/Solution
Navigate to: C:\Gearify\gearify-catalog-svc\Gearify.CatalogService.csproj
```

#### Step 2: Set Breakpoints
- Click in the left margin (gray area) next to any line
- Red dot appears = breakpoint set
- Try setting one in a controller method or handler

**Example locations to set breakpoints:**
```csharp
// In ProductsController.cs
[HttpPost]
public async Task<IActionResult> CreateProduct(CreateProductCommand command)
{
    var result = await _mediator.Send(command); // ← Set breakpoint here
    return Ok(result);
}

// In CreateProductCommandHandler.cs
public async Task<CreateProductResult> Handle(...)
{
    var product = new Product { ... }; // ← Set breakpoint here
    await _repository.CreateAsync(product);
    return new CreateProductResult(true, product.Id);
}
```

#### Step 3: Start Debugging
- Press **F5** or click green "Play" button
- Service starts and browser opens to Swagger UI
- URL: `http://localhost:5001/swagger`

#### Step 4: Test
1. In Swagger UI, expand an endpoint (e.g., POST /api/products)
2. Click "Try it out"
3. Fill in the request body
4. Click "Execute"
5. Visual Studio will pause at your breakpoint!

#### Step 5: Inspect Variables
- Hover over variables to see values
- Use **Locals** window (Debug → Windows → Locals)
- Use **Watch** window (Debug → Windows → Watch)
- Use **Immediate** window to execute code

#### Step 6: Step Through Code
- **F10** = Step Over (next line)
- **F11** = Step Into (enter method)
- **Shift+F11** = Step Out (exit method)
- **F5** = Continue (run to next breakpoint)

### Debug Multiple Services Simultaneously

Open the solution file to debug multiple services:

```
File → Open → Project/Solution
Navigate to: C:\Gearify\Gearify.sln
```

Then:
1. Right-click on a project (e.g., Gearify.CatalogService)
2. Select "Set as Startup Project"
3. OR right-click solution → "Configure Startup Projects"
4. Select "Multiple startup projects"
5. Set Action = "Start" for each service you want to debug
6. Press F5 to start all selected services

---

## Option 2: VS Code

### Setup (One-time)

1. **Install C# Dev Kit extension:**
   - Open VS Code
   - Go to Extensions (Ctrl+Shift+X)
   - Search for "C# Dev Kit"
   - Install it

2. **Open Gearify folder:**
   ```
   File → Open Folder → C:\Gearify
   ```

### Debug Single Service

#### Step 1: Set Breakpoints
- Open any .cs file (e.g., `gearify-catalog-svc/API/Controllers/ProductsController.cs`)
- Click in the left margin next to a line
- Red dot appears = breakpoint

#### Step 2: Select Debug Configuration
1. Click the **Run and Debug** icon in the left sidebar (or press Ctrl+Shift+D)
2. In the dropdown at the top, select a configuration:
   - "Debug Catalog Service"
   - "Debug Cart Service"
   - "Debug Payment Service"
   - "Debug API Gateway"

#### Step 3: Start Debugging
- Click the green **Play** button or press **F5**
- Service builds and starts
- Browser automatically opens to Swagger UI

#### Step 4: Test
- Use Swagger UI to make requests
- OR use the **REST Client** extension with .http files
- Breakpoints will hit when code executes

#### Step 5: Debug Controls
- **F5** = Continue
- **F10** = Step Over
- **F11** = Step Into
- **Shift+F11** = Step Out
- **Shift+F5** = Stop Debugging

### Debug Multiple Services

Use the **compound configuration**:

1. In Run and Debug panel, select "Debug All Services" from dropdown
2. Press F5
3. All services start simultaneously:
   - Catalog Service → http://localhost:5001
   - Cart Service → http://localhost:5002
   - Payment Service → http://localhost:5005

### Hot Reload (Watch Mode)

For development without debugging:

```bash
# Terminal in VS Code
cd gearify-catalog-svc
dotnet watch run
```

Changes to .cs files automatically reload!

---

## Option 3: Command Line + Attach

### Step 1: Start Service Manually
```bash
cd C:\Gearify\gearify-catalog-svc
dotnet run
```

### Step 2: Attach Debugger

**In Visual Studio:**
- Debug → Attach to Process (Ctrl+Alt+P)
- Find "Gearify.CatalogService" in the list
- Click "Attach"

**In VS Code:**
- Run and Debug → Click dropdown
- Select ".NET Core Attach"
- Choose the Gearify process

---

## Debugging Tips

### 1. Conditional Breakpoints

Right-click a breakpoint → Conditions:

```csharp
// Break only when tenantId equals "tenant1"
tenantId == "tenant1"

// Break only when price > 1000
request.Price > 1000
```

### 2. Logpoints (No Code Changes)

Right-click line → Add Logpoint:

```
Product {product.Id} created with price {product.Price}
```

Prints to Debug Console without stopping execution.

### 3. Watch Variables

Add expressions to Watch window:
```
product.Items.Count
transaction.Amount * 0.1
request.TenantId
```

### 4. Immediate Window (Visual Studio)

Execute code during debugging:
```csharp
// In Immediate window while paused:
product.Name
product.Price * 2
await _repository.GetByIdAsync("prod123", "tenant1")
```

### 5. Call Stack

View the execution path:
- Debug → Windows → Call Stack (Ctrl+Alt+C)
- See which methods called the current method

### 6. Autos/Locals Windows

- **Locals**: All local variables in current scope
- **Autos**: Variables used in current and previous statement

---

## Testing with HTTP Files

Create `.http` files to test APIs:

### Create: `test-catalog.http`

```http
### Variables
@baseUrl = http://localhost:5001
@tenantId = tenant1

### Create Product
POST {{baseUrl}}/api/products
Content-Type: application/json
X-Tenant-Id: {{tenantId}}

{
  "tenantId": "{{tenantId}}",
  "name": "Gaming Laptop",
  "description": "High-performance gaming laptop",
  "category": "Electronics",
  "price": 1299.99,
  "sku": "LAPTOP-001",
  "stock": 50
}

### Get Product by ID
GET {{baseUrl}}/api/products/{{productId}}
X-Tenant-Id: {{tenantId}}

### Get Products by Category
GET {{baseUrl}}/api/products/category/Electronics
X-Tenant-Id: {{tenantId}}
```

**To use:**
1. Install "REST Client" extension in VS Code
2. Click "Send Request" above each `###` section
3. Set breakpoints in your code
4. Click "Send Request" and debugger will pause!

---

## Common Debugging Scenarios

### Scenario 1: Debug CreateProduct Flow

1. **Set breakpoints:**
   ```
   ProductsController.cs → CreateProduct method
   CreateProductCommandHandler.cs → Handle method
   DynamoDbProductRepository.cs → CreateAsync method
   ```

2. **Start debugging** (F5)

3. **Send request** via Swagger or .http file

4. **Step through:**
   - Controller receives request
   - MediatR routes to handler
   - Handler creates entity
   - Repository saves to DynamoDB
   - Response returns

### Scenario 2: Debug Payment Processing

1. **Set breakpoints:**
   ```
   ProcessPaymentCommandHandler.cs → Handle method
   StripePaymentProvider.cs → ProcessPaymentAsync method
   RedisIdempotencyService.cs → CheckIdempotencyAsync method
   ```

2. **Watch variables:**
   - `transaction.Amount`
   - `idempotencyKey`
   - `result.Success`

3. **Test:**
   - Send payment request
   - See Stripe API call
   - Verify idempotency check
   - Check PostgreSQL insert

### Scenario 3: Debug Through API Gateway

1. **Start all services:**
   - API Gateway (5000)
   - Catalog Service (5001)

2. **Set breakpoints in both:**
   - Gateway: YARP proxy handler
   - Catalog: ProductsController

3. **Send request to gateway:**
   ```http
   POST http://localhost:5000/api/catalog/products
   ```

4. **See request flow:**
   - Gateway receives → routes to Catalog
   - Catalog processes → returns response
   - Gateway forwards response back

---

## Environment Variables

All services use these environment variables (already configured in launch settings):

| Variable | Purpose | Default |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | Development |
| `DYNAMODB_ENDPOINT` | LocalStack DynamoDB | http://localhost:4566 |
| `S3_ENDPOINT` | LocalStack S3 | http://localhost:4566 |
| `REDIS_URL` | Redis connection | localhost:6379 |
| `SEQ_URL` | Seq logging | http://localhost:5341 |
| `OTLP_ENDPOINT` | OpenTelemetry | http://localhost:4318 |

---

## Troubleshooting

### Issue: "Port already in use"

```bash
# Find process using port 5001
netstat -ano | findstr :5001

# Kill the process (replace PID)
taskkill /PID <pid> /F
```

### Issue: "Cannot connect to DynamoDB"

```bash
# Ensure LocalStack is running
docker ps | findstr localstack

# Restart if needed
docker restart gearify-localstack
```

### Issue: "Cannot connect to Redis"

```bash
# Check Redis
docker ps | findstr redis

# Test connection
redis-cli -h localhost -p 6379 ping
```

### Issue: Breakpoints not hitting

1. **Rebuild the project:**
   ```bash
   dotnet clean
   dotnet build
   ```

2. **Verify you're in Debug mode:**
   - Configuration should be "Debug", not "Release"

3. **Check breakpoint symbol:**
   - Filled red circle = active
   - Hollow red circle = not loaded (code not executing)

### Issue: "System.IO.FileNotFoundException: DLL not found"

```bash
# Restore NuGet packages
dotnet restore

# Rebuild
dotnet build
```

---

## Quick Start Commands

### Start Infrastructure
```bash
cd C:\Gearify\gearify-umbrella
docker-compose up -d
```

### Debug in VS Code
```bash
# Open VS Code
code C:\Gearify

# Press F5 → Select service → Start debugging
```

### Debug in Visual Studio
```bash
# Open solution
start C:\Gearify\Gearify.sln

# Press F5 to start debugging
```

### Manual Run (No Debugger)
```bash
cd C:\Gearify\gearify-catalog-svc
dotnet run
```

### Run with Watch (Auto-reload)
```bash
cd C:\Gearify\gearify-catalog-svc
dotnet watch run
```

---

## Service Ports Reference

| Service | Port | Swagger URL |
|---------|------|-------------|
| API Gateway | 5000 | http://localhost:5000/swagger |
| Catalog | 5001 | http://localhost:5001/swagger |
| Cart | 5002 | http://localhost:5002/swagger |
| Search | 5003 | http://localhost:5003/swagger |
| Order | 5004 | http://localhost:5004/swagger |
| Payment | 5005 | http://localhost:5005/swagger |
| Shipping | 5006 | http://localhost:5006/swagger |
| Inventory | 5007 | http://localhost:5007/swagger |
| Tenant | 5008 | http://localhost:5008/swagger |
| Media | 5009 | http://localhost:5009/swagger |
| Notification | 5010 | http://localhost:5010/swagger |

---

## Additional Tools

### Seq (Logs)
- URL: http://localhost:5341
- View structured logs from all services
- Filter by service, level, tenant

### Jaeger (Tracing)
- URL: http://localhost:16686
- View distributed traces
- See request flow across services

### Prometheus (Metrics)
- URL: http://localhost:9090
- Query metrics
- Create dashboards

### Grafana (Dashboards)
- URL: http://localhost:3000
- Visualize metrics
- Pre-built dashboards

---

## Best Practices

1. **Always start infrastructure first** (Docker Compose)
2. **Set meaningful breakpoints** (not every line)
3. **Use conditional breakpoints** for specific scenarios
4. **Watch key variables** instead of stepping through everything
5. **Use Swagger UI** for quick API testing
6. **Check logs in Seq** for distributed debugging
7. **Use traces in Jaeger** to see cross-service calls

---

**Happy Debugging! 🐛🔍**
