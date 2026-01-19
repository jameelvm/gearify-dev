# Phase 1: Infrastructure & Foundation

**Duration:** Week 1-2
**Goal:** Setup PostgreSQL, SNS/SQS, and create the base projects for Order and Payment services

---

## Overview

```
Phase 1 Tasks
├── 1.1 PostgreSQL Setup
├── 1.2 SNS/SQS Setup (LocalStack)
├── 1.3 Shared Contracts Library
├── 1.4 Create gearify-order-svc
├── 1.5 Create gearify-payment-svc
└── 1.6 API Gateway Configuration
```

---

## 1.1 PostgreSQL Setup

### Tasks

- [ ] **1.1.1** Add PostgreSQL to docker-compose.yml
- [ ] **1.1.2** Create init-databases.sql script
- [ ] **1.1.3** Verify databases are created
- [ ] **1.1.4** Test connection from host

### 1.1.1 Add PostgreSQL to docker-compose.yml

```yaml
# Add to docker-compose.yml
services:
  postgres:
    image: postgres:15
    container_name: gearify-postgres
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./scripts/init-databases.sql:/docker-entrypoint-initdb.d/init.sql
    ports:
      - "5432:5432"
    networks:
      - gearify-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
```

### 1.1.2 Create init-databases.sql

Create file: `scripts/init-databases.sql`

```sql
-- Create databases for each service
CREATE DATABASE gearify_orders;
CREATE DATABASE gearify_payments;
CREATE DATABASE gearify_shipping;

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE gearify_orders TO postgres;
GRANT ALL PRIVILEGES ON DATABASE gearify_payments TO postgres;
GRANT ALL PRIVILEGES ON DATABASE gearify_shipping TO postgres;
```

### 1.1.3 Verify databases

```bash
# Start PostgreSQL
docker-compose up -d postgres

# Connect and list databases
docker exec -it gearify-postgres psql -U postgres -c "\l"

# Expected output should show:
# - gearify_orders
# - gearify_payments
# - gearify_shipping
```

### 1.1.4 Test connection

```bash
# Connection strings for services
# Order Service:    Host=localhost;Port=5432;Database=gearify_orders;Username=postgres;Password=postgres
# Payment Service:  Host=localhost;Port=5432;Database=gearify_payments;Username=postgres;Password=postgres
# Shipping Service: Host=localhost;Port=5432;Database=gearify_shipping;Username=postgres;Password=postgres
```

---

## 1.2 SNS/SQS Setup (LocalStack)

### Tasks

- [ ] **1.2.1** Update LocalStack init script with SNS topics
- [ ] **1.2.2** Add SQS queues
- [ ] **1.2.3** Subscribe queues to topics
- [ ] **1.2.4** Verify setup

### 1.2.1 Update LocalStack init script

Add to your LocalStack init script (e.g., `scripts/localstack-init.sh`):

```bash
#!/bin/bash

echo "Creating SNS Topics..."

# Payment Events Topic
awslocal sns create-topic --name gearify-payment-events
echo "Created: gearify-payment-events"

# Shipping Events Topic
awslocal sns create-topic --name gearify-shipping-events
echo "Created: gearify-shipping-events"

# Order Events Topic
awslocal sns create-topic --name gearify-order-events
echo "Created: gearify-order-events"
```

### 1.2.2 Add SQS queues

```bash
echo "Creating SQS Queues..."

# Order service queues (consumes payment and shipping events)
awslocal sqs create-queue --queue-name order-svc-payment-events
awslocal sqs create-queue --queue-name order-svc-shipping-events

# Notification service queue (consumes order events)
awslocal sqs create-queue --queue-name notification-svc-order-events

# Dead letter queues (for failed messages)
awslocal sqs create-queue --queue-name order-svc-payment-events-dlq
awslocal sqs create-queue --queue-name order-svc-shipping-events-dlq

echo "Created all SQS queues"
```

### 1.2.3 Subscribe queues to topics

```bash
echo "Subscribing queues to topics..."

# Subscribe order-svc-payment-events to gearify-payment-events
awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-payment-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:order-svc-payment-events

# Subscribe order-svc-shipping-events to gearify-shipping-events
awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-shipping-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:order-svc-shipping-events

# Subscribe notification-svc-order-events to gearify-order-events
awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-order-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:notification-svc-order-events

echo "All subscriptions created"
```

### 1.2.4 Verify setup

```bash
# List all topics
awslocal sns list-topics

# List all queues
awslocal sqs list-queues

# List subscriptions
awslocal sns list-subscriptions

# Test publish (optional)
awslocal sns publish \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-payment-events \
    --message '{"test": "message"}'

# Check if message arrived in queue
awslocal sqs receive-message \
    --queue-url http://localhost:4566/000000000000/order-svc-payment-events
```

---

## 1.3 Shared Contracts Library

### Tasks

- [ ] **1.3.1** Create gearify-shared-contracts project
- [ ] **1.3.2** Define integration events
- [ ] **1.3.3** Define common interfaces
- [ ] **1.3.4** Add NuGet package config (optional)

### 1.3.1 Create project

```bash
cd C:/Gearify
mkdir gearify-shared-contracts
cd gearify-shared-contracts
dotnet new classlib -n Gearify.SharedContracts
```

### 1.3.2 Define integration events

Create `Events/PaymentEvents.cs`:

```csharp
namespace Gearify.SharedContracts.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

// Payment Events
public record PaymentSucceededEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid PaymentId,
    Guid OrderId,
    string UserId,
    decimal Amount,
    string Currency
) : IIntegrationEvent;

public record PaymentFailedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid PaymentId,
    Guid OrderId,
    string UserId,
    string ErrorCode,
    string ErrorMessage
) : IIntegrationEvent;

public record RefundCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Reason
) : IIntegrationEvent;
```

Create `Events/ShippingEvents.cs`:

```csharp
namespace Gearify.SharedContracts.Events;

// Shipping Events
public record ShipmentCreatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    DateTime EstimatedDelivery
) : IIntegrationEvent;

public record ShipmentShippedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    DateTime ShippedAt
) : IIntegrationEvent;

public record ShipmentDeliveredEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ShipmentId,
    Guid OrderId,
    DateTime DeliveredAt
) : IIntegrationEvent;
```

Create `Events/OrderEvents.cs`:

```csharp
namespace Gearify.SharedContracts.Events;

// Order Events
public record OrderCreatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid OrderId,
    string OrderNumber,
    string UserId,
    decimal TotalAmount,
    string Currency
) : IIntegrationEvent;

public record OrderCancelledEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid OrderId,
    string Reason,
    string CancelledBy
) : IIntegrationEvent;

public record OrderStatusChangedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid OrderId,
    string FromStatus,
    string ToStatus
) : IIntegrationEvent;
```

### 1.3.3 Define common interfaces

Create `Messaging/IEventPublisher.cs`:

```csharp
namespace Gearify.SharedContracts.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default)
        where T : class, IIntegrationEvent;
}
```

Create `Messaging/MessagingConfiguration.cs`:

```csharp
namespace Gearify.SharedContracts.Messaging;

public class MessagingConfiguration
{
    public string PaymentEventsTopicArn { get; set; } = string.Empty;
    public string ShippingEventsTopicArn { get; set; } = string.Empty;
    public string OrderEventsTopicArn { get; set; } = string.Empty;

    public string PaymentEventsQueueUrl { get; set; } = string.Empty;
    public string ShippingEventsQueueUrl { get; set; } = string.Empty;
    public string OrderEventsQueueUrl { get; set; } = string.Empty;
}
```

---

## 1.4 Create gearify-order-svc

### Tasks

- [ ] **1.4.1** Create project structure
- [ ] **1.4.2** Add NuGet packages
- [ ] **1.4.3** Configure Entity Framework Core
- [ ] **1.4.4** Add health checks
- [ ] **1.4.5** Configure DI and startup
- [ ] **1.4.6** Add to docker-compose
- [ ] **1.4.7** Test service starts

### 1.4.1 Create project structure

```bash
cd C:/Gearify
mkdir gearify-order-svc
cd gearify-order-svc

# Create solution and projects
dotnet new sln -n Gearify.OrderService
dotnet new webapi -n Gearify.OrderService.API
dotnet new classlib -n Gearify.OrderService.Application
dotnet new classlib -n Gearify.OrderService.Domain
dotnet new classlib -n Gearify.OrderService.Infrastructure

# Add projects to solution
dotnet sln add Gearify.OrderService.API
dotnet sln add Gearify.OrderService.Application
dotnet sln add Gearify.OrderService.Domain
dotnet sln add Gearify.OrderService.Infrastructure

# Add project references
cd Gearify.OrderService.API
dotnet add reference ../Gearify.OrderService.Application
dotnet add reference ../Gearify.OrderService.Infrastructure

cd ../Gearify.OrderService.Application
dotnet add reference ../Gearify.OrderService.Domain

cd ../Gearify.OrderService.Infrastructure
dotnet add reference ../Gearify.OrderService.Application
dotnet add reference ../Gearify.OrderService.Domain

# Add shared contracts reference
cd ../Gearify.OrderService.Application
dotnet add reference ../../gearify-shared-contracts/Gearify.SharedContracts
```

### 1.4.2 Add NuGet packages

```bash
# API Project
cd ../Gearify.OrderService.API
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package AspNetCore.HealthChecks.NpgSql

# Application Project
cd ../Gearify.OrderService.Application
dotnet add package MediatR
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions

# Infrastructure Project
cd ../Gearify.OrderService.Infrastructure
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package AWSSDK.SimpleNotificationService
dotnet add package AWSSDK.SQS
dotnet add package Polly
dotnet add package Microsoft.Extensions.Http.Polly
```

### 1.4.3 Configure Entity Framework Core

Create `Infrastructure/Data/OrderDbContext.cs`:

```csharp
using Gearify.OrderService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gearify.OrderService.Infrastructure.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<CheckoutSaga> CheckoutSagas => Set<CheckoutSaga>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

Create basic domain entities in `Domain/Entities/`:

```csharp
// Order.cs
namespace Gearify.OrderService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "pending_payment";

    public string ShippingAddressJson { get; set; } = string.Empty;
    public string? ShippingMethod { get; set; }
    public decimal ShippingCost { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";

    public Guid? PaymentId { get; set; }
    public Guid? ShipmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

// OrderItem.cs
public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public Order Order { get; set; } = null!;
}
```

### 1.4.4 Add health checks

In `API/Program.cs`:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("OrderDb")!,
        name: "postgresql",
        tags: new[] { "db", "postgres" });

// In app configuration
app.MapHealthChecks("/health");
```

### 1.4.5 Configure DI and startup

Create `API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "OrderDb": "Host=localhost;Port=5432;Database=gearify_orders;Username=postgres;Password=postgres"
  },
  "Messaging": {
    "PaymentEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-payment-events",
    "ShippingEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-shipping-events",
    "OrderEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-order-events",
    "PaymentEventsQueueUrl": "http://localstack:4566/000000000000/order-svc-payment-events",
    "ShippingEventsQueueUrl": "http://localstack:4566/000000000000/order-svc-shipping-events"
  },
  "Services": {
    "PaymentServiceUrl": "http://payment-svc:80",
    "ShippingServiceUrl": "http://shipping-svc:80"
  },
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localstack:4566"
  }
}
```

### 1.4.6 Add to docker-compose

```yaml
  order-svc:
    build:
      context: ./gearify-order-svc
      dockerfile: Gearify.OrderService.API/Dockerfile
    container_name: gearify-order-svc
    ports:
      - "5020:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__OrderDb=Host=postgres;Port=5432;Database=gearify_orders;Username=postgres;Password=postgres
      - AWS__ServiceURL=http://localstack:4566
    depends_on:
      postgres:
        condition: service_healthy
      localstack:
        condition: service_started
    networks:
      - gearify-network
```

### 1.4.7 Test service starts

```bash
cd gearify-order-svc
dotnet build
dotnet run --project Gearify.OrderService.API

# Test health endpoint
curl http://localhost:5020/health
```

---

## 1.5 Create gearify-payment-svc

### Tasks

- [ ] **1.5.1** Create project structure (same as order-svc)
- [ ] **1.5.2** Add NuGet packages (+ Stripe.net)
- [ ] **1.5.3** Configure Entity Framework Core
- [ ] **1.5.4** Add Stripe configuration
- [ ] **1.5.5** Add health checks
- [ ] **1.5.6** Configure DI and startup
- [ ] **1.5.7** Add to docker-compose
- [ ] **1.5.8** Test service starts

### 1.5.1 Create project structure

```bash
cd C:/Gearify
mkdir gearify-payment-svc
cd gearify-payment-svc

dotnet new sln -n Gearify.PaymentService
dotnet new webapi -n Gearify.PaymentService.API
dotnet new classlib -n Gearify.PaymentService.Application
dotnet new classlib -n Gearify.PaymentService.Domain
dotnet new classlib -n Gearify.PaymentService.Infrastructure

# Add to solution and references (same pattern as order-svc)
```

### 1.5.2 Add NuGet packages

```bash
# Additional package for Payment Service
cd Gearify.PaymentService.Infrastructure
dotnet add package Stripe.net
```

### 1.5.4 Add Stripe configuration

Create `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PaymentDb": "Host=localhost;Port=5432;Database=gearify_payments;Username=postgres;Password=postgres"
  },
  "Stripe": {
    "SecretKey": "sk_test_xxx",
    "PublishableKey": "pk_test_xxx",
    "WebhookSecret": "whsec_xxx"
  },
  "Messaging": {
    "PaymentEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-payment-events"
  },
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localstack:4566"
  }
}
```

### 1.5.7 Add to docker-compose

```yaml
  payment-svc:
    build:
      context: ./gearify-payment-svc
      dockerfile: Gearify.PaymentService.API/Dockerfile
    container_name: gearify-payment-svc
    ports:
      - "5021:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__PaymentDb=Host=postgres;Port=5432;Database=gearify_payments;Username=postgres;Password=postgres
      - Stripe__SecretKey=${STRIPE_SECRET_KEY}
      - Stripe__WebhookSecret=${STRIPE_WEBHOOK_SECRET}
      - AWS__ServiceURL=http://localstack:4566
    depends_on:
      postgres:
        condition: service_healthy
      localstack:
        condition: service_started
    networks:
      - gearify-network
```

---

## 1.6 API Gateway Configuration

### Tasks

- [ ] **1.6.1** Add routes for order-svc
- [ ] **1.6.2** Add routes for payment-svc
- [ ] **1.6.3** Test routing

### 1.6.1 Add routes for order-svc

Add to API Gateway configuration (e.g., `appsettings.json` or Ocelot config):

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/orders/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE", "PATCH"],
      "DownstreamPathTemplate": "/api/orders/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "order-svc", "Port": 80 }
      ]
    },
    {
      "UpstreamPathTemplate": "/api/checkout/{everything}",
      "UpstreamHttpMethod": ["GET", "POST"],
      "DownstreamPathTemplate": "/api/checkout/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "order-svc", "Port": 80 }
      ]
    }
  ]
}
```

### 1.6.2 Add routes for payment-svc

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/payments/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "DownstreamPathTemplate": "/api/payments/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "payment-svc", "Port": 80 }
      ]
    },
    {
      "UpstreamPathTemplate": "/api/payment-methods/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "DownstreamPathTemplate": "/api/payment-methods/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "payment-svc", "Port": 80 }
      ]
    },
    {
      "UpstreamPathTemplate": "/api/webhooks/stripe",
      "UpstreamHttpMethod": ["POST"],
      "DownstreamPathTemplate": "/api/webhooks/stripe",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "payment-svc", "Port": 80 }
      ]
    }
  ]
}
```

---

## Checklist Summary

### 1.1 PostgreSQL Setup
- [ ] 1.1.1 Add PostgreSQL to docker-compose.yml
- [ ] 1.1.2 Create init-databases.sql script
- [ ] 1.1.3 Verify databases are created
- [ ] 1.1.4 Test connection from host

### 1.2 SNS/SQS Setup
- [ ] 1.2.1 Update LocalStack init script with SNS topics
- [ ] 1.2.2 Add SQS queues
- [ ] 1.2.3 Subscribe queues to topics
- [ ] 1.2.4 Verify setup

### 1.3 Shared Contracts
- [ ] 1.3.1 Create gearify-shared-contracts project
- [ ] 1.3.2 Define integration events
- [ ] 1.3.3 Define common interfaces
- [ ] 1.3.4 Add NuGet package config

### 1.4 Order Service
- [ ] 1.4.1 Create project structure
- [ ] 1.4.2 Add NuGet packages
- [ ] 1.4.3 Configure Entity Framework Core
- [ ] 1.4.4 Add health checks
- [ ] 1.4.5 Configure DI and startup
- [ ] 1.4.6 Add to docker-compose
- [ ] 1.4.7 Test service starts

### 1.5 Payment Service
- [ ] 1.5.1 Create project structure
- [ ] 1.5.2 Add NuGet packages
- [ ] 1.5.3 Configure Entity Framework Core
- [ ] 1.5.4 Add Stripe configuration
- [ ] 1.5.5 Add health checks
- [ ] 1.5.6 Configure DI and startup
- [ ] 1.5.7 Add to docker-compose
- [ ] 1.5.8 Test service starts

### 1.6 API Gateway
- [ ] 1.6.1 Add routes for order-svc
- [ ] 1.6.2 Add routes for payment-svc
- [ ] 1.6.3 Test routing

---

## Definition of Done

Phase 1 is complete when:

1. ✅ `docker-compose up` starts PostgreSQL with 3 databases
2. ✅ LocalStack has SNS topics and SQS queues configured
3. ✅ `gearify-shared-contracts` project exists with event definitions
4. ✅ `gearify-order-svc` starts and `/health` returns healthy
5. ✅ `gearify-payment-svc` starts and `/health` returns healthy
6. ✅ API Gateway routes requests to both services

---

## Next Phase

Once Phase 1 is complete, proceed to:
- **[Phase 2: Order Service - Core](./PHASE-2-ORDER-SERVICE.md)**
