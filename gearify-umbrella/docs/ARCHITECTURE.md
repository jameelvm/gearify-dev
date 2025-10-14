# Gearify Architecture

## Overview
Microservices-based e-commerce platform for cricket equipment with multi-tenancy support.

## Services

### API Gateway
- Technology: YARP-based routing
- Port: 8080
- Responsibilities: JWT validation, rate limiting, service routing

### Tenant Service
- Port: 5008
- Database: DynamoDB
- Responsibilities: Multi-tenant config, theme management, feature flags

### Catalog Service
- Port: 5001
- Database: DynamoDB + Redis cache
- Storage: S3 for product images
- Responsibilities: Product CRUD, inventory management

### Order Service
- Port: 5004
- Database: DynamoDB + Redis
- Messaging: SNS/SQS
- Responsibilities: Cart management, order processing, fulfillment

### Payment Service
- Port: 5005
- Database: PostgreSQL
- Integrations: Stripe, PayPal
- Responsibilities: Payment processing, reconciliation

### Shipping Service
- Port: 5006
- Database: DynamoDB
- Responsibilities: Shipping rates, carrier integration, tracking

### Notification Service
- Port: 5010
- Messaging: SQS consumer
- Email: MailHog (local), SES (production)
- Responsibilities: Email notifications, event processing

## Data Stores
- **DynamoDB**: Products, orders, tenants, feature flags
- **PostgreSQL**: Payment transactions, reconciliation
- **Redis**: Cache, sessions
- **S3**: Product images, assets

## Communication Patterns
- **Synchronous**: HTTP/REST via API Gateway
- **Asynchronous**: SNS/SQS for domain events

## Security
- Cognito for authentication (LocalStack Pro locally)
- JWT tokens with tenant claims
- Service-to-service via API keys stored in Secrets Manager

## Observability
- Logs: Seq (local), CloudWatch (production)
- Traces: Jaeger (local), X-Ray (production)
- Metrics: Prometheus + Grafana
