# C# Record vs Class - Complete Guide

## Overview

C# 9.0 introduced the `record` keyword as a new reference type designed specifically for immutable data. This guide explains the differences between `record` and `class`, and when to use each.

## Table of Contents
- [Quick Comparison](#quick-comparison)
- [Detailed Differences](#detailed-differences)
- [Syntax Examples](#syntax-examples)
- [When to Use What](#when-to-use-what)
- [Gearify Examples](#gearify-examples)

## Quick Comparison

| Feature | `record` | `class` |
|---------|----------|---------|
| **Primary Purpose** | Immutable data containers | Objects with behavior and state |
| **Equality** | Value-based (compares data) | Reference-based (compares memory address) |
| **Mutability** | Immutable by default | Mutable by default |
| **Syntax** | Concise, one-liner | Verbose, multi-line |
| **ToString()** | Auto-generates readable output | Returns type name only |
| **Inheritance** | Supports inheritance | Supports inheritance |
| **Use Case** | DTOs, Commands, Queries, Value Objects | Entities, Services, Complex objects |

## Detailed Differences

### 1. Equality Comparison

#### Record - Value-Based Equality

```csharp
public record AddToCartCommand(string UserId, string ProductId, int Quantity);

var cmd1 = new AddToCartCommand("user1", "prod1", 5);
var cmd2 = new AddToCartCommand("user1", "prod1", 5);

Console.WriteLine(cmd1 == cmd2);           // TRUE ✅
Console.WriteLine(cmd1.Equals(cmd2));      // TRUE ✅
Console.WriteLine(ReferenceEquals(cmd1, cmd2)); // FALSE (different memory locations)
```

Two records are equal if **all their property values are equal**.

#### Class - Reference-Based Equality

```csharp
public class AddToCartCommandClass
{
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}

var cmd1 = new AddToCartCommandClass { UserId = "user1", ProductId = "prod1", Quantity = 5 };
var cmd2 = new AddToCartCommandClass { UserId = "user1", ProductId = "prod1", Quantity = 5 };

Console.WriteLine(cmd1 == cmd2);           // FALSE ❌
Console.WriteLine(cmd1.Equals(cmd2));      // FALSE ❌
Console.WriteLine(ReferenceEquals(cmd1, cmd2)); // FALSE

var cmd3 = cmd1;
Console.WriteLine(cmd1 == cmd3);           // TRUE (same reference)
```

Two classes are equal only if they are **the same instance in memory**.

### 2. Immutability

#### Record - Immutable by Default

```csharp
public record AddToCartCommand(string UserId, string ProductId, int Quantity);

var cmd = new AddToCartCommand("user1", "prod1", 5);

// ❌ COMPILE ERROR - Properties are init-only
// cmd.Quantity = 10;

// ✅ Use 'with' expression to create a modified copy
var modifiedCmd = cmd with { Quantity = 10 };

Console.WriteLine(cmd.Quantity);          // 5 (original unchanged)
Console.WriteLine(modifiedCmd.Quantity);  // 10 (new instance)
```

#### Class - Mutable by Default

```csharp
public class AddToCartCommandClass
{
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}

var cmd = new AddToCartCommandClass { UserId = "user1", ProductId = "prod1", Quantity = 5 };

// ✅ Allowed - modifies the existing object
cmd.Quantity = 10;

Console.WriteLine(cmd.Quantity);  // 10 (mutated)
```

### 3. ToString() Behavior

#### Record - Readable Output

```csharp
public record AddToCartCommand(string UserId, string ProductId, int Quantity);

var cmd = new AddToCartCommand("user1", "prod1", 5);
Console.WriteLine(cmd);

// Output:
// AddToCartCommand { UserId = user1, ProductId = prod1, Quantity = 5 }
```

Perfect for logging and debugging!

#### Class - Type Name Only

```csharp
public class AddToCartCommandClass
{
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}

var cmd = new AddToCartCommandClass { UserId = "user1", ProductId = "prod1", Quantity = 5 };
Console.WriteLine(cmd);

// Output:
// Gearify.Application.Commands.AddToCartCommandClass
```

Not useful unless you override `ToString()` manually.

### 4. Deconstruction

#### Record - Built-in Deconstruction

```csharp
public record AddToCartCommand(string UserId, string ProductId, int Quantity);

var cmd = new AddToCartCommand("user1", "prod1", 5);

// Deconstruct into variables
var (userId, productId, quantity) = cmd;

Console.WriteLine(userId);     // user1
Console.WriteLine(productId);  // prod1
Console.WriteLine(quantity);   // 5
```

#### Class - Manual Implementation Required

```csharp
public class AddToCartCommandClass
{
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }

    // Must manually implement deconstruction
    public void Deconstruct(out string userId, out string productId, out int quantity)
    {
        userId = UserId;
        productId = ProductId;
        quantity = Quantity;
    }
}
```

## Syntax Examples

### Record Syntax Variations

#### 1. Positional Record (Most Common for DTOs)

```csharp
// One-liner - Properties are auto-generated
public record CreateOrderCommand(
    string TenantId,
    string UserId,
    List<OrderItem> Items,
    string ShippingAddress
) : IRequest<CreateOrderResult>;
```

Equivalent to:
```csharp
public record CreateOrderCommand : IRequest<CreateOrderResult>
{
    public string TenantId { get; init; }
    public string UserId { get; init; }
    public List<OrderItem> Items { get; init; }
    public string ShippingAddress { get; init; }

    public CreateOrderCommand(string tenantId, string userId, List<OrderItem> items, string shippingAddress)
    {
        TenantId = tenantId;
        UserId = userId;
        Items = items;
        ShippingAddress = shippingAddress;
    }

    // Plus: Equals, GetHashCode, ToString, Deconstruct, Clone, etc.
}
```

#### 2. Record with Additional Members

```csharp
public record ProcessPaymentCommand(
    string TenantId,
    string OrderId,
    decimal Amount,
    string Currency
) : IRequest<ProcessPaymentResult>
{
    // Additional validation method
    public bool IsValid() => Amount > 0 && !string.IsNullOrEmpty(Currency);

    // Additional property with default value
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}
```

#### 3. Traditional Record Syntax

```csharp
public record AddToCartResult
{
    public bool Success { get; init; }
    public Cart? Cart { get; init; }
    public string? ErrorMessage { get; init; }
}

// Usage
var result = new AddToCartResult
{
    Success = true,
    Cart = myCart
};
```

#### 4. Record with Default Values

```csharp
public record SearchProductsQuery(
    string TenantId,
    string? SearchTerm = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null
) : IRequest<SearchProductsResult>;

// Usage - only provide required parameters
var query = new SearchProductsQuery("tenant1");
var queryWithSearch = new SearchProductsQuery("tenant1", "laptop");
```

### Class Syntax

```csharp
public class Cart
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public List<CartItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Behavior - methods that operate on the state
    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Price * i.Quantity);
    }

    public void AddItem(CartItem item)
    {
        var existing = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existing != null)
        {
            existing.Quantity += item.Quantity;
        }
        else
        {
            Items.Add(item);
        }
        RecalculateTotal();
    }

    public void RemoveItem(string productId)
    {
        Items.RemoveAll(i => i.ProductId == productId);
        RecalculateTotal();
    }
}
```

## When to Use What?

### Use `record` for:

#### ✅ CQRS Commands and Queries
```csharp
public record CreateOrderCommand(...) : IRequest<CreateOrderResult>;
public record GetOrderQuery(...) : IRequest<Order>;
```

**Why:** Commands and queries are immutable messages that carry data from one layer to another.

#### ✅ Command/Query Results
```csharp
public record CreateOrderResult(bool Success, string? OrderId, string? ErrorMessage);
public record SearchProductsResult(List<ProductSearchResult> Products, int TotalCount);
```

**Why:** Results are immutable snapshots of the operation outcome.

#### ✅ API Request/Response Models
```csharp
public record AddItemRequest(string ProductId, string ProductName, string Sku, int Quantity, decimal Price);
public record ApiResponse<T>(bool Success, T? Data, string? Error);
```

**Why:** DTOs should be immutable to prevent accidental modification during serialization/deserialization.

#### ✅ Value Objects (Domain-Driven Design)
```csharp
public record Money(decimal Amount, string Currency);
public record Address(string Street, string City, string State, string ZipCode, string Country);
public record EmailAddress(string Value);
```

**Why:** Value objects are defined by their values, not identity. Two addresses with same values are the same address.

#### ✅ Configuration Objects
```csharp
public record DatabaseConfig(string ConnectionString, int MaxRetries, int TimeoutSeconds);
public record AwsConfig(string Region, string AccessKey, string SecretKey);
```

**Why:** Configuration should be immutable once loaded.

### Use `class` for:

#### ✅ Domain Entities
```csharp
public class Order
{
    public string Id { get; set; }          // Identity
    public OrderStatus Status { get; set; } // Mutable state
    public List<OrderItem> Items { get; set; } = new();

    public void AddItem(OrderItem item) { /* logic */ }
    public void Cancel() { Status = OrderStatus.Cancelled; }
}

public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }  // Changes frequently

    public void UpdatePrice(decimal newPrice) { Price = newPrice; }
    public void DecrementStock(int quantity) { StockQuantity -= quantity; }
}
```

**Why:** Entities have identity and mutable state that changes over their lifetime. An Order with ID "123" is the same order even if its status changes.

#### ✅ Services and Handlers
```csharp
public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ILogger<AddToCartCommandHandler> _logger;

    public AddToCartCommandHandler(ICartRepository repository, ILogger<AddToCartCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AddToCartResult> Handle(AddToCartCommand request, CancellationToken ct)
    {
        // Business logic
    }
}
```

**Why:** Services contain behavior and dependencies, not just data.

#### ✅ Complex Objects with Behavior
```csharp
public class PaymentProcessor
{
    private readonly IStripeProvider _stripe;
    private readonly IPayPalProvider _paypal;

    public async Task<PaymentResult> ProcessAsync(PaymentRequest request)
    {
        return request.Provider switch
        {
            PaymentProvider.Stripe => await _stripe.ChargeAsync(request),
            PaymentProvider.PayPal => await _paypal.ChargeAsync(request),
            _ => throw new NotSupportedException()
        };
    }
}
```

**Why:** Complex orchestration and behavior belongs in classes.

#### ✅ Objects with Mutable Collections
```csharp
public class Cart
{
    public List<CartItem> Items { get; set; } = new();  // Frequently modified

    public void AddItem(CartItem item) { Items.Add(item); }
    public void RemoveItem(string id) { Items.RemoveAll(i => i.ProductId == id); }
    public void Clear() { Items.Clear(); }
}
```

**Why:** If you need to frequently modify collections, `class` is more natural.

## Gearify Examples

### Commands (Records)

```csharp
// Cart Service
public record AddToCartCommand(
    string UserId,
    string TenantId,
    string ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal Price,
    string? ImageUrl = null
) : IRequest<AddToCartResult>;

public record RemoveFromCartCommand(
    string UserId,
    string TenantId,
    string ProductId
) : IRequest<RemoveFromCartResult>;

// Payment Service
public record ProcessPaymentCommand(
    string TenantId,
    string OrderId,
    string UserId,
    decimal Amount,
    string Currency,
    PaymentProvider Provider,
    string PaymentMethodToken,
    string IdempotencyKey
) : IRequest<ProcessPaymentResult>;

// Order Service
public record CreateOrderCommand(
    string TenantId,
    string UserId,
    List<OrderItem> Items,
    string ShippingAddress
) : IRequest<CreateOrderResult>;
```

### Queries (Records)

```csharp
// Search Service
public record SearchProductsQuery(
    string TenantId,
    string? SearchTerm = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null
) : IRequest<SearchProductsResult>;

// Cart Service
public record GetCartQuery(
    string UserId,
    string TenantId
) : IRequest<Cart?>;
```

### Results (Records)

```csharp
public record AddToCartResult(bool Success, Cart? Cart = null, string? ErrorMessage = null);

public record ProcessPaymentResult(
    bool Success,
    Guid? TransactionId = null,
    PaymentStatus? Status = null,
    string? ErrorMessage = null
);

public record SearchProductsResult(List<ProductSearchResult> Products, int TotalCount);

public record ProductSearchResult(
    string Id,
    string Name,
    string Category,
    decimal Price,
    string Brand,
    string ImageUrl
);
```

### Entities (Classes)

```csharp
// Cart entity - has identity and mutable state
public class Cart
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Price * i.Quantity);
    }
}

// Order entity - has lifecycle and state transitions
public class Order
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Payment Transaction - has state that changes
public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Handlers (Classes)

```csharp
public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ILogger<AddToCartCommandHandler> _logger;

    public AddToCartCommandHandler(ICartRepository repository, ILogger<AddToCartCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AddToCartResult> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        // Business logic
    }
}
```

## Common Patterns

### Pattern 1: Command with Nested Record

```csharp
public record CreateProductCommand(
    string TenantId,
    string Sku,
    string Name,
    decimal Price,
    ProductImages Images  // Nested record
) : IRequest<CreateProductResult>;

public record ProductImages(
    string MainImage,
    List<string> ThumbnailImages
);
```

### Pattern 2: Record Inheritance

```csharp
public abstract record BaseCommand(string TenantId, string UserId);

public record CreateOrderCommand(
    string TenantId,
    string UserId,
    List<OrderItem> Items
) : BaseCommand(TenantId, UserId), IRequest<CreateOrderResult>;

public record CancelOrderCommand(
    string TenantId,
    string UserId,
    string OrderId
) : BaseCommand(TenantId, UserId), IRequest<CancelOrderResult>;
```

### Pattern 3: Record with Validation

```csharp
public record ProcessPaymentCommand(
    string TenantId,
    string OrderId,
    decimal Amount,
    string Currency
) : IRequest<ProcessPaymentResult>
{
    // Validation method
    public bool IsValid()
    {
        return Amount > 0
            && !string.IsNullOrEmpty(Currency)
            && Currency.Length == 3
            && !string.IsNullOrEmpty(OrderId);
    }
}
```

## Performance Considerations

### Memory Allocation

```csharp
// Record - each 'with' creates a new instance
var original = new AddToCartCommand("user1", "tenant1", "prod1", "Product", "SKU1", 5, 99.99m);
var modified1 = original with { Quantity = 10 };  // New allocation
var modified2 = modified1 with { Price = 89.99m }; // Another new allocation
var modified3 = modified2 with { Quantity = 15 };  // Yet another allocation

// Class - mutates in-place (no new allocation)
var cmd = new AddToCartCommandClass { UserId = "user1", ProductId = "prod1", Quantity = 5 };
cmd.Quantity = 10;  // Same instance
cmd.Price = 89.99m; // Same instance
cmd.Quantity = 15;  // Same instance
```

**Verdict:** For high-frequency mutations, `class` is more efficient. But for DTOs/messages (created once, used once), `record` is perfect.

### Equality Checks

```csharp
// Record - value equality is slower (compares all properties)
var cmd1 = new AddToCartCommand(...);
var cmd2 = new AddToCartCommand(...);
if (cmd1 == cmd2) { /* Compares all 7 properties */ }

// Class - reference equality is very fast (compares memory addresses)
var cmd1 = new AddToCartCommandClass { ... };
var cmd2 = new AddToCartCommandClass { ... };
if (cmd1 == cmd2) { /* Compares 1 pointer */ }
```

**Verdict:** Reference equality is faster, but value equality is usually what you want for DTOs.

## Migration Guide

### Converting Class to Record

**Before (Class):**
```csharp
public class AddToCartCommand
{
    public string UserId { get; set; }
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}
```

**After (Record):**
```csharp
public record AddToCartCommand(
    string UserId,
    string ProductId,
    int Quantity
);
```

**Steps:**
1. Change `class` to `record`
2. Move properties to constructor parameters
3. Remove property declarations (auto-generated)
4. Change `{ get; set; }` to constructor parameters
5. Test equality behavior (now value-based!)

## Best Practices

### ✅ DO

```csharp
// Use records for DTOs
public record CreateOrderCommand(...) : IRequest<CreateOrderResult>;

// Use records for results
public record CreateOrderResult(bool Success, string? OrderId);

// Use classes for entities
public class Order { /* with behavior */ }

// Use classes for services
public class OrderService { /* with dependencies */ }

// Use positional syntax for simple records
public record Money(decimal Amount, string Currency);

// Use 'with' for modifications
var updated = original with { Quantity = 10 };
```

### ❌ DON'T

```csharp
// Don't use record for entities with complex behavior
public record Order { /* BAD - entities should be classes */ }

// Don't use class for simple DTOs
public class CreateOrderCommand { /* BAD - should be record */ }

// Don't mutate records (defeats the purpose)
// public record Data(string Value) { public string Value { get; set; } } // BAD

// Don't use records for objects that need frequent mutation
public record Cache { public Dictionary<string, object> Data { get; set; } } // BAD
```

## Summary

| Scenario | Use |
|----------|-----|
| CQRS Command | `record` |
| CQRS Query | `record` |
| Command/Query Result | `record` |
| API Request/Response | `record` |
| Value Object | `record` |
| Domain Entity | `class` |
| Service/Handler | `class` |
| Repository | `class` |
| Objects with behavior | `class` |
| Objects with frequent mutations | `class` |

## Further Reading

- [C# Records Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Value Objects in DDD](https://martinfowler.com/bliki/ValueObject.html)
- [Immutability in C#](https://docs.microsoft.com/en-us/dotnet/csharp/write-safe-efficient-code)
