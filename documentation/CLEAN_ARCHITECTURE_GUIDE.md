# Clean Architecture Guide for Gearify Microservices

## Table of Contents
- [Overview](#overview)
- [Architectural Principles](#architectural-principles)
- [Folder Structure](#folder-structure)
- [Layer Responsibilities](#layer-responsibilities)
- [Dependency Rules](#dependency-rules)
- [Implementation Patterns](#implementation-patterns)
- [Real Examples from Gearify](#real-examples-from-gearify)
- [Best Practices](#best-practices)

---

## Overview

Clean Architecture (also known as Onion Architecture or Hexagonal Architecture) is a software design philosophy that emphasizes:

1. **Independence of Frameworks** - Business logic doesn't depend on external libraries
2. **Testability** - Business rules can be tested without UI, database, or external services
3. **Independence of UI** - The UI can change without affecting business logic
4. **Independence of Database** - You can swap databases without affecting business rules
5. **Independence of External Services** - Business logic doesn't know about the outside world

### The Dependency Rule

**Source code dependencies must point only inward, toward higher-level policies.**

```
┌─────────────────────────────────────┐
│         Domain Layer                │  ← Core business entities
│    (No external dependencies)       │
└─────────────────────────────────────┘
              ↑
┌─────────────────────────────────────┐
│      Application Layer              │  ← Business logic & use cases
│  (Depends only on Domain)           │     (MediatR, FluentValidation)
└─────────────────────────────────────┘
              ↑
┌─────────────────────────────────────┐
│    Infrastructure Layer             │  ← External services & data access
│  (Depends on Domain + Application)  │     (DynamoDB, Redis, S3, etc.)
└─────────────────────────────────────┘
              ↑
┌─────────────────────────────────────┐
│         API Layer                   │  ← HTTP endpoints
│  (Depends on all other layers)      │     (Controllers, Middleware)
└─────────────────────────────────────┘
```

---

## Folder Structure

### Standard Gearify Service Structure

```
gearify-[service-name]-svc/
│
├── Domain/                          # Core business entities (innermost layer)
│   ├── Entities/                    # Business objects with identity
│   │   ├── Product.cs
│   │   ├── Order.cs
│   │   └── Cart.cs
│   │
│   ├── ValueObjects/                # Immutable objects without identity
│   │   ├── Money.cs
│   │   ├── Address.cs
│   │   └── Email.cs
│   │
│   ├── Events/                      # Domain events
│   │   ├── ProductCreatedEvent.cs
│   │   └── OrderPlacedEvent.cs
│   │
│   └── Interfaces/                  # Core abstractions (optional)
│       └── IRepository.cs
│
├── Application/                     # Business logic & use cases
│   ├── Commands/                    # Write operations (CQRS)
│   │   ├── CreateProductCommand.cs
│   │   └── CreateProductCommandHandler.cs
│   │
│   ├── Queries/                     # Read operations (CQRS)
│   │   ├── GetProductByIdQuery.cs
│   │   └── GetProductByIdQueryHandler.cs
│   │
│   ├── Validators/                  # Input validation
│   │   ├── CreateProductValidator.cs
│   │   └── UpdateProductValidator.cs
│   │
│   ├── DTOs/                        # Data Transfer Objects
│   │   ├── ProductDto.cs
│   │   └── OrderDto.cs
│   │
│   └── Interfaces/                  # Application-specific interfaces
│       └── IEmailService.cs
│
├── Infrastructure/                  # External services & implementations
│   ├── Repositories/                # Data access implementations
│   │   ├── IProductRepository.cs
│   │   └── DynamoDbProductRepository.cs
│   │
│   ├── Adapters/                    # External service adapters
│   │   ├── StripePaymentProvider.cs
│   │   └── PayPalPaymentProvider.cs
│   │
│   ├── Database/                    # Database-specific code
│   │   ├── schema.sql
│   │   └── migrations/
│   │
│   └── External/                    # Third-party integrations
│       ├── S3ImageStorage.cs
│       └── SqsMessagePublisher.cs
│
├── API/                             # HTTP layer (outermost layer)
│   ├── Controllers/                 # REST endpoints
│   │   ├── ProductsController.cs
│   │   └── OrdersController.cs
│   │
│   ├── Middleware/                  # HTTP middleware
│   │   ├── ErrorHandlingMiddleware.cs
│   │   └── AuthenticationMiddleware.cs
│   │
│   └── Filters/                     # Action filters
│       └── ValidationFilter.cs
│
├── Tests/                           # Testing projects
│   ├── UnitTests/                   # Unit tests (no dependencies)
│   │   └── CreateProductCommandHandlerTests.cs
│   │
│   └── IntegrationTests/            # Integration tests (with dependencies)
│       └── ProductRepositoryTests.cs
│
├── Program.cs                       # Application entry point
├── appsettings.json                 # Configuration
├── Dockerfile                       # Container definition
└── [ServiceName].csproj             # Project file
```

---

## Layer Responsibilities

### 1. Domain Layer (Core)

**Purpose:** Contains enterprise-wide business rules and entities

**Characteristics:**
- ✅ No external dependencies (except .NET base classes)
- ✅ Pure C# classes with business logic
- ✅ Highly testable
- ✅ Framework-agnostic

**What goes here:**
- Business entities with behavior
- Value objects (immutable types)
- Domain events
- Business rule validations
- Enums and constants

**Example: Order Entity**
```csharp
namespace Gearify.OrderService.Domain.Entities;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // Business logic in domain
    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(item => item.Price * item.Quantity);
    }

    public bool CanBeCancelled()
    {
        return Status is OrderStatus.Pending or OrderStatus.Confirmed;
    }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
```

---

### 2. Application Layer

**Purpose:** Contains application-specific business logic and orchestration

**Characteristics:**
- ✅ Depends only on Domain layer
- ✅ Uses MediatR for CQRS pattern
- ✅ Uses FluentValidation for input validation
- ✅ Orchestrates domain objects
- ✅ Defines interfaces for infrastructure

**What goes here:**
- Commands (write operations)
- Queries (read operations)
- Command/Query handlers
- Validators
- DTOs (Data Transfer Objects)
- Application services
- Interface definitions (implemented by Infrastructure)

**Example: Command**
```csharp
using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Commands;

// Command definition (request)
public record CreateProductCommand(
    string TenantId,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string Sku,
    int Stock
) : IRequest<CreateProductResult>;

// Result definition (response)
public record CreateProductResult(
    bool Success,
    string? ProductId = null,
    string? ErrorMessage = null
);
```

**Example: Command Handler**
```csharp
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = new Product
            {
                TenantId = request.TenantId,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Price = request.Price,
                Sku = request.Sku,
                Stock = request.Stock
            };

            await _repository.CreateAsync(product);

            _logger.LogInformation(
                "Product {ProductId} created successfully",
                product.Id);

            return new CreateProductResult(true, product.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product");
            return new CreateProductResult(false, null, ex.Message);
        }
    }
}
```

**Example: Query**
```csharp
using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductByIdQuery(
    string ProductId,
    string TenantId
) : IRequest<Product?>;
```

**Example: Query Handler**
```csharp
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _repository;

    public GetProductByIdQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<Product?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.ProductId,
            request.TenantId);
    }
}
```

**Example: Validator**
```csharp
using FluentValidation;
using Gearify.CatalogService.Application.Commands;

namespace Gearify.CatalogService.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero");

        RuleFor(x => x.Sku)
            .NotEmpty()
            .Matches(@"^[A-Z0-9-]+$")
            .WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Stock cannot be negative");
    }
}
```

---

### 3. Infrastructure Layer

**Purpose:** Implements external concerns (databases, APIs, file systems)

**Characteristics:**
- ✅ Depends on Domain and Application layers
- ✅ Contains all external dependencies
- ✅ Implements interfaces defined in Application
- ✅ Framework-specific code lives here

**What goes here:**
- Repository implementations (DynamoDB, PostgreSQL, Redis)
- External API clients (Stripe, PayPal, EasyPost)
- File storage (S3)
- Message queues (SQS, SNS)
- Email services (MailHog, SES)
- Caching implementations
- Database schemas and migrations

**Example: Repository Interface** (defined in Application/Infrastructure boundary)
```csharp
using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string productId, string tenantId);
    Task<List<Product>> GetByCategoryAsync(string category, string tenantId);
    Task CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(string productId, string tenantId);
}
```

**Example: DynamoDB Repository Implementation**
```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public class DynamoDbProductRepository : IProductRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "gearify-products";

    public DynamoDbProductRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task<Product?> GetByIdAsync(string productId, string tenantId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"PRODUCT#{productId}" } }
            }
        });

        return response.IsItemSet ? DeserializeProduct(response.Item) : null;
    }

    public async Task<List<Product>> GetByCategoryAsync(
        string category,
        string tenantId)
    {
        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue
                    { S = $"TENANT#{tenantId}#CATEGORY#{category}" } }
            }
        });

        return response.Items.Select(DeserializeProduct).ToList();
    }

    public async Task CreateAsync(Product product)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },
            { "GSI1PK", new AttributeValue
                { S = $"TENANT#{product.TenantId}#CATEGORY#{product.Category}" } },
            { "GSI1SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },
            { "Id", new AttributeValue { S = product.Id } },
            { "Name", new AttributeValue { S = product.Name } },
            { "Description", new AttributeValue { S = product.Description } },
            { "Category", new AttributeValue { S = product.Category } },
            { "Price", new AttributeValue { N = product.Price.ToString() } },
            { "Sku", new AttributeValue { S = product.Sku } },
            { "Stock", new AttributeValue { N = product.Stock.ToString() } },
            { "CreatedAt", new AttributeValue
                { S = product.CreatedAt.ToString("O") } }
        };

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    private Product DeserializeProduct(Dictionary<string, AttributeValue> item)
    {
        return new Product
        {
            Id = item["Id"].S,
            TenantId = item["PK"].S.Replace("TENANT#", ""),
            Name = item["Name"].S,
            Description = item["Description"].S,
            Category = item["Category"].S,
            Price = decimal.Parse(item["Price"].N),
            Sku = item["Sku"].S,
            Stock = int.Parse(item["Stock"].N),
            CreatedAt = DateTime.Parse(item["CreatedAt"].S)
        };
    }

    // ... other methods
}
```

**Example: Payment Provider Abstraction**
```csharp
// Interface (in Infrastructure)
namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public interface IPaymentProvider
{
    Task<PaymentResult> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodId);

    Task<RefundResult> RefundPaymentAsync(
        string transactionId,
        decimal amount);
}

// Stripe Implementation
public class StripePaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _configuration;

    public StripePaymentProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodId)
    {
        var service = new PaymentIntentService();

        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100), // Stripe uses cents
            Currency = currency.ToLower(),
            PaymentMethod = paymentMethodId,
            Confirm = true
        };

        var intent = await service.CreateAsync(options);

        return new PaymentResult
        {
            Success = intent.Status == "succeeded",
            TransactionId = intent.Id,
            Status = intent.Status
        };
    }

    // ... RefundPaymentAsync implementation
}

// PayPal Implementation
public class PayPalPaymentProvider : IPaymentProvider
{
    // Different implementation, same interface
}
```

---

### 4. API Layer

**Purpose:** HTTP-specific code and presentation logic

**Characteristics:**
- ✅ Depends on all other layers
- ✅ ASP.NET Core controllers
- ✅ Middleware and filters
- ✅ HTTP-specific concerns

**What goes here:**
- REST controllers
- Middleware (authentication, error handling)
- Action filters
- API models (if different from DTOs)
- Swagger/OpenAPI configuration

**Example: Controller**
```csharp
using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IMediator mediator,
        ILogger<ProductsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(
        string id,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId)
    {
        var query = new GetProductByIdQuery(id, tenantId);
        var product = await _mediator.Send(query);

        if (product == null)
        {
            return NotFound(new { message = "Product not found" });
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = result.ProductId },
            new { productId = result.ProductId });
    }

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetProductsByCategory(
        string category,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId)
    {
        var query = new GetProductsByCategoryQuery(category, tenantId);
        var products = await _mediator.Send(query);
        return Ok(products);
    }
}
```

---

## Dependency Rules

### ✅ Allowed Dependencies

```
Domain → (nothing)
Application → Domain
Infrastructure → Domain + Application
API → Domain + Application + Infrastructure
```

### ❌ Forbidden Dependencies

```
Domain → Application (NO!)
Domain → Infrastructure (NO!)
Domain → API (NO!)
Application → Infrastructure (NO!)
Application → API (NO!)
```

### Why These Rules?

1. **Domain is pure** - No external dependencies means maximum portability
2. **Application defines contracts** - Infrastructure implements them (Dependency Inversion Principle)
3. **Easy to test** - Mock infrastructure, test application logic
4. **Easy to swap** - Change DynamoDB to PostgreSQL? Just change Infrastructure!

---

## Implementation Patterns

### CQRS (Command Query Responsibility Segregation)

**Commands** - Modify state (write operations)
**Queries** - Return data (read operations)

```
┌─────────────────────────────────────┐
│         Controller                  │
│  POST /products                     │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│   CreateProductCommand              │ ← Command
│  (request to create)                │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│ CreateProductCommandHandler         │ ← Handler
│  (business logic)                   │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│   IProductRepository                │ ← Repository
│  (data access)                      │
└─────────────────────────────────────┘
              ↓
┌─────────────────────────────────────┐
│      DynamoDB                       │ ← Database
└─────────────────────────────────────┘
```

### Repository Pattern

**Purpose:** Abstracts data access

```csharp
// Interface (contract)
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id, string tenantId);
    Task CreateAsync(Product product);
}

// Implementation can be swapped
public class DynamoDbProductRepository : IProductRepository { }
public class MongoDbProductRepository : IProductRepository { }
public class InMemoryProductRepository : IProductRepository { } // For tests!
```

### Dependency Injection

All dependencies are injected through constructors:

```csharp
public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    // Dependencies injected here
    public CreateProductCommandHandler(
        IProductRepository repository,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        // Use injected dependencies
        await _repository.CreateAsync(product);
        _logger.LogInformation("Product created");

        return result;
    }
}
```

Configured in `Program.cs`:

```csharp
// Register dependencies
builder.Services.AddScoped<IProductRepository, DynamoDbProductRepository>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

---

## Real Examples from Gearify

### Example 1: Catalog Service (Complete)

```
gearify-catalog-svc/
├── Domain/
│   ├── Entities/
│   │   └── Product.cs              ← Pure business entity
│   └── Events/
│       └── ProductCreatedEvent.cs  ← Domain event
│
├── Application/
│   ├── Commands/
│   │   ├── CreateProductCommand.cs          ← Write operation
│   │   ├── CreateProductCommandHandler.cs
│   │   ├── UpdateProductCommand.cs
│   │   └── UpdateProductCommandHandler.cs
│   │
│   ├── Queries/
│   │   ├── GetProductByIdQuery.cs           ← Read operation
│   │   └── GetProductByIdQueryHandler.cs
│   │
│   └── Validators/
│       └── CreateProductValidator.cs        ← Input validation
│
├── Infrastructure/
│   └── Repositories/
│       ├── IProductRepository.cs            ← Interface
│       └── DynamoDbProductRepository.cs     ← DynamoDB implementation
│
├── API/
│   └── Controllers/
│       └── ProductsController.cs            ← REST endpoints
│
└── Program.cs                               ← Dependency injection setup
```

### Example 2: Payment Service (Complete)

```
gearify-payment-svc/
├── Domain/
│   └── Entities/
│       ├── PaymentTransaction.cs
│       └── PaymentLedger.cs
│
├── Application/
│   └── Commands/
│       ├── ProcessPaymentCommand.cs
│       └── ProcessPaymentCommandHandler.cs
│
├── Infrastructure/
│   ├── PaymentProviders/
│   │   ├── IPaymentProvider.cs              ← Abstraction
│   │   ├── StripePaymentProvider.cs         ← Stripe implementation
│   │   ├── PayPalPaymentProvider.cs         ← PayPal implementation
│   │   └── RedisIdempotencyService.cs       ← Idempotency cache
│   │
│   ├── Repositories/
│   │   ├── IPaymentRepository.cs
│   │   └── PostgresPaymentRepository.cs     ← PostgreSQL + Dapper
│   │
│   └── Database/
│       └── schema.sql                       ← DB schema
│
└── API/
    └── Controllers/
        └── PaymentsController.cs
```

### Example 3: Cart Service (Complete)

```
gearify-cart-svc/
├── Domain/
│   └── Entities/
│       └── Cart.cs                          ← With business logic
│
├── Application/
│   ├── Commands/
│   │   └── AddToCartCommand.cs              ← Multiple commands in one file
│   │       ├── AddToCartCommand
│   │       ├── RemoveFromCartCommand
│   │       └── ClearCartCommand
│   │
│   └── Queries/
│       ├── GetCartQuery.cs
│       └── GetCartQueryHandler.cs
│
├── Infrastructure/
│   └── Repositories/
│       ├── ICartRepository.cs
│       └── RedisCartRepository.cs           ← Redis implementation
│
└── Program.cs
```

---

## Best Practices

### 1. Keep Domain Pure

❌ **Bad:** Domain depends on external library
```csharp
using Amazon.DynamoDBv2.DataModel; // External dependency!

public class Product
{
    [DynamoDBHashKey] // Don't put infrastructure attributes here!
    public string Id { get; set; }
}
```

✅ **Good:** Pure domain entity
```csharp
public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }

    // Business logic
    public bool IsInStock() => Stock > 0;
}
```

### 2. Use Interfaces for Abstraction

❌ **Bad:** Direct dependency on implementation
```csharp
public class CreateProductCommandHandler
{
    private readonly DynamoDbProductRepository _repository; // Concrete class!

    public CreateProductCommandHandler(DynamoDbProductRepository repository)
    {
        _repository = repository;
    }
}
```

✅ **Good:** Depend on abstraction
```csharp
public class CreateProductCommandHandler
{
    private readonly IProductRepository _repository; // Interface!

    public CreateProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }
}
```

### 3. Keep Controllers Thin

❌ **Bad:** Business logic in controller
```csharp
[HttpPost]
public async Task<IActionResult> CreateProduct(CreateProductRequest request)
{
    // Business logic in controller - BAD!
    var product = new Product
    {
        Name = request.Name,
        Price = request.Price
    };

    if (product.Price <= 0)
    {
        return BadRequest("Price must be positive");
    }

    await _dynamoDb.PutItemAsync(...); // Direct DB access - BAD!

    return Ok(product);
}
```

✅ **Good:** Controller delegates to handler
```csharp
[HttpPost]
public async Task<IActionResult> CreateProduct(CreateProductCommand command)
{
    var result = await _mediator.Send(command); // Delegate to handler

    if (!result.Success)
    {
        return BadRequest(new { error = result.ErrorMessage });
    }

    return CreatedAtAction(nameof(GetProduct),
        new { id = result.ProductId },
        result);
}
```

### 4. Use Result Objects

❌ **Bad:** Throwing exceptions for business logic
```csharp
public async Task<Product> Handle(CreateProductCommand request, ...)
{
    if (request.Price <= 0)
    {
        throw new Exception("Invalid price"); // Don't use exceptions for flow control
    }

    return product;
}
```

✅ **Good:** Return result objects
```csharp
public async Task<CreateProductResult> Handle(CreateProductCommand request, ...)
{
    if (request.Price <= 0)
    {
        return new CreateProductResult(
            Success: false,
            ErrorMessage: "Price must be positive");
    }

    return new CreateProductResult(Success: true, ProductId: product.Id);
}
```

### 5. Single Responsibility

Each handler does ONE thing:

```csharp
// ✅ Good: One command, one handler
CreateProductCommand → CreateProductCommandHandler
UpdateProductCommand → UpdateProductCommandHandler
DeleteProductCommand → DeleteProductCommandHandler

// ❌ Bad: One handler for everything
ProductManager.CreateProduct()
ProductManager.UpdateProduct()
ProductManager.DeleteProduct()
ProductManager.GetProduct()
ProductManager.ListProducts()
```

### 6. Validation at Boundaries

```csharp
// Application layer validator
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Sku).Matches(@"^[A-Z0-9-]+$");
    }
}

// Domain layer business rules
public class Product
{
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new InvalidOperationException("Price cannot be negative");

        Price = newPrice;
    }
}
```

### 7. Use Records for Immutable DTOs

```csharp
// ✅ Good: Immutable command
public record CreateProductCommand(
    string Name,
    decimal Price,
    string Sku
) : IRequest<CreateProductResult>;

// ✅ Good: Immutable result
public record CreateProductResult(
    bool Success,
    string? ProductId = null,
    string? ErrorMessage = null
);
```

---

## Testing Benefits

### Unit Tests (Fast, No Dependencies)

```csharp
public class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidProduct_CreatesSuccessfully()
    {
        // Arrange
        var mockRepo = Substitute.For<IProductRepository>();
        var mockLogger = Substitute.For<ILogger<CreateProductCommandHandler>>();
        var handler = new CreateProductCommandHandler(mockRepo, mockLogger);

        var command = new CreateProductCommand(
            TenantId: "tenant1",
            Name: "Test Product",
            Price: 99.99m,
            Sku: "TEST-001"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        await mockRepo.Received(1).CreateAsync(Arg.Any<Product>());
    }
}
```

### Integration Tests (With Real Dependencies)

```csharp
public class ProductRepositoryTests : IAsyncLifetime
{
    private readonly DynamoDbLocalContainer _container;
    private IAmazonDynamoDB _dynamoDb;

    public async Task InitializeAsync()
    {
        _container = new DynamoDbLocalContainer();
        await _container.StartAsync();
        _dynamoDb = _container.CreateClient();
    }

    [Fact]
    public async Task CreateAsync_SavesProductToDynamoDB()
    {
        // Arrange
        var repository = new DynamoDbProductRepository(_dynamoDb);
        var product = new Product { Name = "Test", Price = 100 };

        // Act
        await repository.CreateAsync(product);

        // Assert
        var retrieved = await repository.GetByIdAsync(product.Id, product.TenantId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test");
    }
}
```

---

## Summary

### Key Takeaways

1. **Domain Layer** = Pure business logic, no dependencies
2. **Application Layer** = Use cases and orchestration (CQRS with MediatR)
3. **Infrastructure Layer** = External services (DynamoDB, Redis, S3, etc.)
4. **API Layer** = HTTP endpoints (thin controllers)

5. **Dependencies flow inward** = Outer layers depend on inner layers, never the reverse

6. **Benefits**:
   - Easy to test (mock interfaces)
   - Easy to change (swap implementations)
   - Clear separation of concerns
   - Framework-independent business logic
   - Highly maintainable

### Quick Reference

| Layer | Purpose | Dependencies | Examples |
|-------|---------|--------------|----------|
| **Domain** | Business entities | None | Product, Order, Cart |
| **Application** | Business logic | Domain | Commands, Queries, Handlers |
| **Infrastructure** | External services | Domain + Application | Repositories, Payment providers |
| **API** | HTTP layer | All layers | Controllers |

---

## Additional Resources

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)

---

**Generated for Gearify Microservices**
**Date:** October 17, 2025
**Architecture:** Clean Architecture + CQRS + DDD
