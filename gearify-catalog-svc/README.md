# Gearify Catalog Service

Production-ready .NET 8 microservice for managing product catalogs in the Gearify e-commerce platform.

## Architecture

- **Clean Architecture** with CQRS pattern using MediatR
- **Domain Layer**: Entities, Value Objects, Domain Events
- **Application Layer**: Commands, Queries, Handlers, Validators
- **Infrastructure Layer**: DynamoDB Repository, S3 Integration
- **API Layer**: REST Controllers with Swagger

## Features

- ✅ Product CRUD operations
- ✅ Multi-tenant support via X-Tenant-Id header
- ✅ DynamoDB with GSI for category queries
- ✅ S3 integration for product images
- ✅ FluentValidation for input validation
- ✅ Serilog JSON logging to Seq
- ✅ OpenTelemetry distributed tracing
- ✅ Health check endpoint
- ✅ Swagger API documentation

## Prerequisites

- .NET 8 SDK
- Docker & Docker Compose (for infrastructure)
- LocalStack Pro (DynamoDB, S3)

## Running Locally

```bash
# Restore packages
dotnet restore

# Run the service
dotnet run

# Service will be available at http://localhost:5001
```

## Environment Variables

```bash
DYNAMODB_ENDPOINT=http://localhost:4566
S3_ENDPOINT=http://localhost:4566
SEQ_URL=http://localhost:5341
OTLP_ENDPOINT=http://otel-collector:4318
```

## API Endpoints

### Products

- `GET /api/catalog/products` - Get all products (with optional ?category filter)
- `GET /api/catalog/products/{id}` - Get product by ID
- `POST /api/catalog/products` - Create new product
- `PUT /api/catalog/products/{id}` - Update product

### Health

- `GET /health` - Health check

## Example Request

```bash
curl -X POST http://localhost:5001/api/catalog/products \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tenant-123" \
  -d '{
    "tenantId": "tenant-123",
    "sku": "BAT-001",
    "name": "CA Plus 15000 Bat",
    "description": "Premium English willow cricket bat",
    "category": "bat",
    "brand": "CA",
    "price": 299.99,
    "compareAtPrice": 349.99,
    "tags": ["cricket", "bat", "professional"],
    "attributes": {
      "weight": "1200g",
      "material": "English Willow"
    }
  }'
```

## Testing

```bash
# Run unit tests
dotnet test Tests/Gearify.CatalogService.UnitTests

# Run integration tests
dotnet test Tests/Gearify.CatalogService.IntegrationTests
```

## DynamoDB Schema

**Table**: `gearify-products`

**Primary Key**:
- PK: `TENANT#{tenantId}`
- SK: `PRODUCT#{productId}`

**GSI1** (Category Index):
- GSI1PK: `TENANT#{tenantId}#CATEGORY#{category}`
- GSI1SK: `PRODUCT#{productId}`

## Docker Build

```bash
docker build -t gearify-catalog-svc .
docker run -p 5001:80 gearify-catalog-svc
```

## Monitoring

- **Logs**: Seq UI at http://localhost:5341
- **Traces**: Jaeger UI at http://localhost:16686
- **Metrics**: Prometheus/Grafana at http://localhost:3000

## License

Proprietary - Gearify Platform
