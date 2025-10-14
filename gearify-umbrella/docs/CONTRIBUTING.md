# Contributing to Gearify

## Getting Started

### Prerequisites
- Git
- Docker Desktop
- Node.js 18+
- .NET 8 SDK
- LocalStack Pro license

### Setup
```bash
git clone <repo-url>
cd gearify-umbrella
cp .env.template .env
# Add your LOCALSTACK_API_KEY
make clone-all
make up
make seed
```

## Branching Strategy

### Main Branches
- `main` - Production-ready code
- `develop` - Integration branch for features

### Feature Branches
Format: `feature/<ticket-number>-<short-description>`

Example: `feature/GEAR-123-add-product-reviews`

### Bugfix Branches
Format: `bugfix/<ticket-number>-<short-description>`

Example: `bugfix/GEAR-456-fix-cart-total`

### Workflow
```bash
# Create feature branch from develop
git checkout develop
git pull
git checkout -b feature/GEAR-123-add-reviews

# Make changes and commit
git add .
git commit -m "feat(catalog): add product reviews API"

# Push and create PR
git push origin feature/GEAR-123-add-reviews
```

## Pull Request Guidelines

### Before Submitting
- [ ] Code builds successfully
- [ ] All tests pass (`make test`)
- [ ] Lint checks pass
- [ ] Updated documentation if needed
- [ ] Added/updated tests for new features

### PR Title Format
Use conventional commits:
- `feat(service): description` - New feature
- `fix(service): description` - Bug fix
- `docs: description` - Documentation only
- `refactor(service): description` - Code refactoring
- `test(service): description` - Adding tests
- `chore: description` - Maintenance tasks

### PR Description Template
```markdown
## Description
Brief description of changes

## Related Issue
Fixes #123

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
How to test these changes

## Screenshots (if applicable)
```

### Review Process
1. Automated checks must pass (CI/CD)
2. At least 1 approving review required
3. No unresolved conversations
4. Branch up to date with target branch

## Coding Standards

### .NET (C#)
- Follow Microsoft C# coding conventions
- Use async/await for I/O operations
- Implement proper error handling
- Use dependency injection
- Write XML documentation for public APIs

```csharp
/// <summary>
/// Retrieves a product by ID
/// </summary>
/// <param name="productId">The product identifier</param>
/// <returns>Product details or null if not found</returns>
public async Task<Product?> GetProductAsync(string productId)
{
    // Implementation
}
```

### Angular (TypeScript)
- Follow Angular style guide
- Use reactive programming (RxJS)
- Implement smart/dumb component pattern
- Use OnPush change detection where possible
- Write JSDoc comments

```typescript
/**
 * Fetches products for the current tenant
 * @param category Optional category filter
 * @returns Observable of product array
 */
getProducts(category?: string): Observable<Product[]> {
  // Implementation
}
```

### General
- Maximum line length: 120 characters
- Use meaningful variable/function names
- Avoid magic numbers - use constants
- Keep functions small and focused
- Write self-documenting code

## Testing Standards

### Unit Tests
- Minimum 80% code coverage
- Test happy paths and error cases
- Use descriptive test names

```csharp
[Fact]
public async Task GetProduct_WithValidId_ReturnsProduct()
{
    // Arrange
    var productId = "prod-123";

    // Act
    var result = await _service.GetProductAsync(productId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(productId, result.Id);
}
```

### Integration Tests
- Use Testcontainers for dependencies
- Clean up test data after each test
- Test service interactions

### E2E Tests
- Cover critical user journeys
- Run before merging to main
- Use Page Object pattern

## Commit Messages

### Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation
- `style` - Formatting
- `refactor` - Code restructuring
- `test` - Adding tests
- `chore` - Maintenance

### Examples
```
feat(catalog): add product search functionality

Implemented full-text search across product catalog
using DynamoDB query with filters.

Closes #123
```

```
fix(payment): handle Stripe webhook signature validation

Added proper signature verification to prevent
replay attacks.

Fixes #456
```

## Documentation

### When to Update Docs
- Adding new features
- Changing APIs
- Modifying architecture
- Adding configuration options

### Doc Files to Update
- README.md - For setup changes
- ARCHITECTURE.md - For design changes
- API-REFERENCE.md - For API changes
- RUNBOOK.md - For operational changes

## Release Process

### Versioning
Follow Semantic Versioning (SemVer):
- MAJOR.MINOR.PATCH
- Example: 1.2.3

### Release Checklist
- [ ] All tests passing
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
- [ ] Version bumped in Chart.yaml
- [ ] Tagged in Git
- [ ] Deployed to staging first
- [ ] Smoke tests pass
- [ ] Deploy to production

## Getting Help

- Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- Ask in team Slack channel
- Create GitHub issue for bugs
- Discuss architecture changes in team meetings

## Code of Conduct

- Be respectful and professional
- Provide constructive feedback
- Help others learn and grow
- Focus on the code, not the person
