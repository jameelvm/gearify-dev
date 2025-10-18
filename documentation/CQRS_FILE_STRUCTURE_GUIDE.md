# CQRS File Structure Guide

## Overview

When implementing CQRS (Command Query Responsibility Segregation) with MediatR, it's important to follow proper file organization principles. This guide explains the recommended file structure and why it matters.

## Why Separate Files?

### Bad Practice ❌
```
Application/
└── Commands/
    └── AddToCartCommand.cs  (contains Command + Result + Handler all in one file)
```

### Good Practice ✅
```
Application/
└── Commands/
    ├── AddToCartCommand.cs        (Command + Result only)
    └── AddToCartCommandHandler.cs (Handler only)
```

## Benefits of Separation

### 1. **Single Responsibility Principle**
- Each file has ONE clear purpose
- Commands/Queries define the contract (what you want to do)
- Handlers contain the business logic (how to do it)

### 2. **Improved Maintainability**
- Easy to locate specific logic
- Changes to business logic don't affect the command definition
- Clearer git diffs when reviewing changes

### 3. **Better Testability**
- Test files mirror the structure
- Mock dependencies are isolated to handlers
- Command validation can be tested separately

### 4. **Enhanced Readability**
- Smaller, focused files are easier to understand
- New developers can quickly find what they need
- Code reviews are more focused

## File Naming Conventions

### Commands
```
AddToCartCommand.cs
RemoveFromCartCommand.cs
ClearCartCommand.cs
UpdateQuantityCommand.cs
```

**Contains:**
- Command record (the request)
- Result record (the response)

**Example:**
```csharp
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(
    string UserId,
    string ProductId,
    int Quantity
) : IRequest<AddToCartResult>;

public record AddToCartResult(bool Success, string? ErrorMessage = null);
```

### Handlers
```
AddToCartCommandHandler.cs
RemoveFromCartCommandHandler.cs
ClearCartCommandHandler.cs
UpdateQuantityCommandHandler.cs
```

**Contains:**
- Handler class implementing `IRequestHandler<TRequest, TResponse>`
- Business logic
- Repository calls
- Logging

**Example:**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

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
        // Business logic here
        return new AddToCartResult(true);
    }
}
```

### Queries (Same Pattern)
```
GetCartQuery.cs          -> GetCartQueryHandler.cs
SearchProductsQuery.cs   -> SearchProductsQueryHandler.cs
GetOrderQuery.cs         -> GetOrderQueryHandler.cs
```

## Folder Structure

### Complete Example
```
Gearify.CartService/
├── Application/
│   ├── Commands/
│   │   ├── AddToCartCommand.cs
│   │   ├── AddToCartCommandHandler.cs
│   │   ├── RemoveFromCartCommand.cs
│   │   ├── RemoveFromCartCommandHandler.cs
│   │   ├── ClearCartCommand.cs
│   │   └── ClearCartCommandHandler.cs
│   └── Queries/
│       ├── GetCartQuery.cs
│       ├── GetCartQueryHandler.cs
│       ├── GetCartItemsQuery.cs
│       └── GetCartItemsQueryHandler.cs
├── Domain/
│   └── Entities/
│       └── Cart.cs
└── Infrastructure/
    └── Repositories/
        ├── ICartRepository.cs
        └── RedisCartRepository.cs
```

## When to Group Multiple Classes

### Keep in Same File ✅
- Command and its Result record (small DTOs)
- Query and its Result record

### Separate Files ✅
- Commands and Handlers
- Queries and Handlers
- Validators (if using FluentValidation)
- Different commands/queries

### Example of Keeping Together
```csharp
// AddToCartCommand.cs
public record AddToCartCommand(...) : IRequest<AddToCartResult>;
public record AddToCartResult(...); // ✅ Keep together - they're tightly coupled
```

## Migration Strategy

If you have existing code with everything in one file:

1. **Create the handler file** with proper name (`*CommandHandler.cs`)
2. **Move the handler class** to the new file
3. **Add necessary using statements** to both files
4. **Build and test** to ensure everything still works
5. **Commit** with a clear message

## Gearify Implementation Status

✅ **Properly Structured:**
- Cart Service (refactored)
- Payment Service (already separated)
- Order Service (handlers created)
- Search Service (handlers created)

## Tools and IDE Support

### Visual Studio / Rider
- Use "Go to Implementation" (Ctrl+F12) to jump from command to handler
- File structure mirrors logical structure

### VS Code
- Use "Go to Definition" (F12) to navigate
- File explorer shows clear organization

## Summary

| Aspect | Bad Practice | Good Practice |
|--------|-------------|---------------|
| File size | 100-500 lines | 20-50 lines per file |
| Responsibility | Mixed concerns | Single purpose |
| Navigation | Scroll through large file | Direct file access |
| Testing | Complex test setup | Focused unit tests |
| Git conflicts | Higher chance | Lower chance |
| Code review | Harder to review | Easy to review |

## Further Reading

- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
