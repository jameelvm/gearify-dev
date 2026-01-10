# Gearify Search Service Documentation

This directory contains comprehensive documentation for the Gearify Search Service, a dedicated microservice providing fast, full-text product search capabilities using AWS OpenSearch.

## Documents

### 1. [ARCHITECTURE.md](./ARCHITECTURE.md)
**Purpose**: Complete technical architecture and design

**Contents**:
- High-level architecture diagrams
- Technology stack and rationale
- OpenSearch index schema and mappings
- Data flow diagrams (indexing and querying)
- Event schema definitions
- API endpoint specifications
- Multi-tenant isolation strategy
- Caching strategy (Redis + OpenSearch)
- Performance targets and SLAs
- Security considerations
- Monitoring and observability
- Cost estimation
- Future enhancements roadmap

**Target Audience**: Architects, senior developers, DevOps engineers

---

### 2. [SEQUENCE-DIAGRAMS.md](./SEQUENCE-DIAGRAMS.md)
**Purpose**: Visual flow diagrams for key operations

**Contents**:
- Event-driven synchronization flow (Catalog → Search)
- Product search query flow
- Index creation flow
- Product indexing flow
- Bulk indexing flow
- Autocomplete flow
- Delete product flow

**Target Audience**: Developers, architects

---

### 3. [CLASS-RESPONSIBILITIES.md](./CLASS-RESPONSIBILITIES.md)
**Purpose**: Detailed class documentation by architectural layer

**Contents**:
- API Layer classes (Controllers)
- Application Layer classes (Handlers, Events, Mappers, DTOs)
- Domain Layer classes (Entities, Events)
- Infrastructure Layer classes (OpenSearch, Messaging, Configuration)
- Dependency graph
- Interface summary

**Target Audience**: Developers implementing or maintaining the service

---

### 4. [IMPLEMENTATION-PLAN.md](./IMPLEMENTATION-PLAN.md)
**Purpose**: Step-by-step implementation guide with module-wise breakdown

**Contents**:
- 6-phase development plan (6 weeks, 1 developer)
- Module-wise task breakdown
- Code examples and templates
- Testing strategies for each module
- Commands and scripts for setup/verification
- Development best practices
- Getting started guide

**Target Audience**: Developers implementing the service

**Development Phases**:
1. **Phase 1** (Week 1): Foundation & Infrastructure
   - Project setup, OpenSearch client, domain models
2. **Phase 2** (Week 2): Core Search Functionality
   - Product indexing, search queries, faceted search
3. **Phase 3** (Week 3): Event-Driven Sync
   - SQS consumer, SNS publisher, real-time synchronization
4. **Phase 4** (Week 4): Advanced Features
   - Autocomplete, suggestions, Redis caching, bulk sync
5. **Phase 5** (Week 5): Testing & Documentation
   - Unit tests, integration tests, performance testing
6. **Phase 6** (Week 6): Deployment
   - Docker setup, monitoring, production readiness

---

## Quick Links

### Architecture Decisions
- **Search Engine**: AWS OpenSearch (Elasticsearch-compatible)
- **Why OpenSearch?**
  - LocalStack support for local development
  - AWS ecosystem integration
  - Scalable and feature-rich
  - Cost-effective compared to Algolia/Typesense
  - Full-text search with fuzzy matching
  - Faceted search and aggregations
  - Autocomplete with edge n-grams

### Key Features
- **Full-Text Search**: Multi-field search with relevance scoring
- **Fuzzy Matching**: Handles typos and misspellings
- **Autocomplete**: Edge n-gram based typeahead
- **Faceted Search**: Aggregations for brands, categories, price ranges
- **Real-Time Sync**: Event-driven updates via SNS/SQS (< 2s latency)
- **Multi-Tenant**: Isolated indexes per tenant
- **Caching**: Redis cache with 5-minute TTL (70%+ hit rate target)
- **High Performance**: < 100ms P95 search latency, 500 qps throughput

### Technology Stack
- **.NET 8.0**: Runtime and web framework
- **NEST**: Elasticsearch .NET client
- **AWS OpenSearch**: Search engine
- **AWS SQS/SNS**: Event messaging
- **Redis**: Search result caching
- **LocalStack**: Local development environment
- **MediatR**: CQRS pattern implementation
- **Serilog**: Structured logging

---

## Development Workflow

### Initial Setup
```bash
# 1. Create project structure
cd C:/Gearify/gearify-search-svc
dotnet new sln -n Gearify.SearchService

# 2. Follow steps in IMPLEMENTATION-PLAN.md > Getting Started

# 3. Start LocalStack
cd C:/Gearify/gearify-umbrella
docker-compose up -d

# 4. Run Search Service
cd C:/Gearify/gearify-search-svc/API
dotnet run
```

### Testing Endpoints
```bash
# Search products
curl "http://localhost:5004/api/search/products?q=nike&tenantId=default"

# Autocomplete
curl "http://localhost:5004/api/search/autocomplete?q=nik&tenantId=default"

# Search with filters
curl "http://localhost:5004/api/search/products?q=shoes&tenantId=default&category=running-shoes&minPrice=50&maxPrice=200"
```

---

## Integration with Existing Services

### Catalog Service Integration
- **Event Publishing**: Catalog Service publishes ProductCreated/Updated/Deleted events to SNS
- **Event Flow**: Catalog → SNS → SQS → Search Service Background Worker
- **Sync Latency**: < 2 seconds (eventual consistency)

### API Gateway Integration
- **Routing**: API Gateway routes `/api/search/*` to Search Service
- **Authentication**: JWT validation at gateway level
- **Tenant Isolation**: X-Tenant-Id header required for all requests

### Media Service Integration
- **Thumbnail Updates**: MediaProcessed events trigger search index updates
- **Ensures**: Product thumbnails stay in sync with media processing

---

## Performance Targets

| Metric                    | Target      | Measurement |
|---------------------------|-------------|-------------|
| Search Query Latency      | < 100ms     | P95         |
| Autocomplete Latency      | < 50ms      | P95         |
| Index Update Latency      | < 2s        | P95         |
| Search Throughput         | 500 qps     | Sustained   |
| Index Sync Success Rate   | > 99.9%     | Daily       |
| Cache Hit Rate            | > 70%       | Hourly      |

---

## Cost Estimation (AWS - Monthly)

| Service                   | Cost (USD) |
|---------------------------|------------|
| OpenSearch (2 x t3.medium)| ~$140      |
| ElastiCache (t3.micro)    | ~$15       |
| ECS Fargate (3 tasks)     | ~$45       |
| SQS + SNS                 | ~$1        |
| Data Transfer             | ~$9        |
| **Total**                 | **~$210**  |

*Note: Assumes moderate traffic (10K searches/day). Can optimize with reserved instances (-30%).*

---

## Next Steps

1. **Review Architecture**: Read ARCHITECTURE.md thoroughly
2. **Plan Sprints**: Break down IMPLEMENTATION-PLAN.md into sprints
3. **Setup Environment**: Follow "Getting Started" in IMPLEMENTATION-PLAN.md
4. **Start Development**: Begin with Phase 1, Module 1.1
5. **Test Incrementally**: Test each module before moving to next

---

## Questions & Support

For questions about the Search Service architecture or implementation:
- Check ARCHITECTURE.md for design decisions
- Check IMPLEMENTATION-PLAN.md for implementation guidance
- Review code examples in IMPLEMENTATION-PLAN.md
- Refer to OpenSearch documentation: https://opensearch.org/docs/

---

**Last Updated**: 2026-01-09
**Status**: Implementation Complete
**Documentation**: Sequence diagrams and class responsibilities added
