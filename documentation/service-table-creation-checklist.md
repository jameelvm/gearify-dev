# Service/Table Creation Checklist

## DynamoDB Table Setup

| Step | Task | Location | Example |
|------|------|----------|---------|
| ☐ | Create table schema JSON file | `gearify-umbrella/localstack/dynamodb/tables/{table-name}.json` | `catalog.json` |
| ☐ | Add table creation to init script | `gearify-umbrella/localstack/init-aws.sh` (create-tables section) | Lines 139-157 |
| ☐ | Create seed data batch files | `gearify-umbrella/localstack/dynamodb/data/{table}-{tenant}-batch-{n}.json` | `catalog-default-tenant-batch-1.json` |
| ☐ | Add seed data execution to init script | `gearify-umbrella/localstack/init-aws.sh` (seed-data section) | Lines 202-219 |
| ☐ | Document table schema & access patterns | `documentation/{table}-dynamodb-table.md` | `catalog-dynamodb-table.md` |

## Backend Service Setup

| Step | Task | Location | Example |
|------|------|----------|---------|
| ☐ | Create domain entities | `{service}/Domain/Entities/{Entity}.cs` | `Category.cs`, `CategorySection.cs` |
| ☐ | Create repository interface | `{service}/Infrastructure/Repositories/I{Entity}Repository.cs` | `ICategoryRepository.cs` |
| ☐ | Create repository implementation | `{service}/Infrastructure/Repositories/DynamoDb{Entity}Repository.cs` | `DynamoDbCategoryRepository.cs` |
| ☐ | Create DTOs | `{service}/API/DTOs/{Entity}Dto.cs` | `CategoryDto.cs`, `CategoryWithDetailsDto.cs` |
| ☐ | Create query/command classes | `{service}/Application/Queries/{Operation}Query.cs` | `GetMegaMenuDataQuery.cs` |
| ☐ | Create query/command handlers | `{service}/Application/Queries/{Operation}QueryHandler.cs` | `GetMegaMenuDataQueryHandler.cs` |
| ☐ | Create controller | `{service}/API/Controllers/{Entity}Controller.cs` | `CategoriesController.cs` |
| ☐ | Register repository in DI | `{service}/Program.cs` or `Startup.cs` | `services.AddScoped<ICategoryRepository, DynamoDbCategoryRepository>()` |

## Frontend Integration

| Step | Task | Location | Example |
|------|------|----------|---------|
| ☐ | Create TypeScript interfaces | `gearify-web/src/app/core/services/{entity}.service.ts` | `CategoryDto`, `CategoryWithDetailsDto` |
| ☐ | Create service with HTTP methods | `gearify-web/src/app/core/services/{entity}.service.ts` | `CategoryService.getMegaMenuData()` |
| ☐ | Add API endpoint to constants | `gearify-web/src/app/shared/constants/api.constants.ts` | `API_CONFIG.ENDPOINTS.CATALOG` |
| ☐ | Create components (if needed) | `gearify-web/src/app/shared/components/{entity}/` | `category-nav.component.ts` |

## Testing & Verification

| Step | Task | Example |
|------|------|---------|
| ☐ | Test table creation | `docker-compose up` + check LocalStack logs |
| ☐ | Verify seed data loaded | `aws dynamodb scan --table-name {table}` |
| ☐ | Test API endpoints | Swagger UI or curl |
| ☐ | Test frontend integration | Browser DevTools Network tab |
| ☐ | Verify multi-tenancy isolation | Query with different tenant IDs |
