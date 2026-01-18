# Gearify Checkout System - Microservices Design & Implementation Guide

## Table of Contents

1. [Overview](#overview)
2. [Microservices Architecture](#microservices-architecture)
3. [Service Responsibilities](#service-responsibilities)
4. [Database Schema](#database-schema)
5. [Inter-Service Communication](#inter-service-communication)
6. [Distributed Transaction (Saga Pattern)](#distributed-transaction-saga-pattern)
7. [Payment Integration](#payment-integration)
8. [Implementation Phases](#implementation-phases)
9. [Testing Strategy](#testing-strategy)
10. [API Endpoints](#api-endpoints)
11. [Event Flows](#event-flows)

---

## Overview

The checkout system consists of three independent microservices:

| Service | Responsibility | Database |
|---------|----------------|----------|
| **gearify-order-svc** | Order lifecycle, orchestration | PostgreSQL |
| **gearify-payment-svc** | Payment processing, Stripe integration | PostgreSQL |
| **gearify-shipping-svc** | Shipping rates, carriers, tracking | PostgreSQL |

### Key Principles

- **Single Responsibility** - Each service owns its domain
- **Loose Coupling** - Services communicate via events/APIs
- **Data Ownership** - Each service owns its database
- **Saga Pattern** - Distributed transactions with compensating actions
- **Idempotency** - All operations are idempotent
- **Event-Driven** - Async communication where possible

---

## Microservices Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                 API Gateway                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
         │              │                │                │              │
         ▼              ▼                ▼                ▼              ▼
   ┌──────────┐  ┌──────────┐    ┌─────────────┐  ┌─────────────┐  ┌──────────┐
   │ Auth Svc │  │ Cart Svc │    │  Order Svc  │  │ Payment Svc │  │ Shipping │
   │          │  │          │    │             │  │             │  │   Svc    │
   │ DynamoDB │  │ DynamoDB │    │ PostgreSQL  │  │ PostgreSQL  │  │PostgreSQL│
   └──────────┘  └──────────┘    └─────────────┘  └─────────────┘  └──────────┘
                                        │                │              │
                                        │   HTTP (sync)  │              │
                                        │◀──────────────▶│              │
                                        │                │              │
                                        └────────┬───────┴──────────────┘
                                                 │ Events (async)
                                        ┌────────▼────────┐
                                        │    AWS SNS      │
                                        │  (Fan-out to    │
                                        │   SQS queues)   │
                                        └─────────────────┘
                                                 │
                              ┌──────────────────┼──────────────────┐
                              ▼                  ▼                  ▼
                       ┌────────────┐    ┌─────────────┐    ┌─────────────┐
                       │   Stripe   │    │   Carrier   │    │Notification │
                       │    API     │    │    APIs     │    │    Svc      │
                       └────────────┘    └─────────────┘    └─────────────┘
```

### Service Communication Matrix

| From → To | Order Svc | Payment Svc | Shipping Svc |
|-----------|-----------|-------------|--------------|
| **Order Svc** | - | HTTP (sync) | HTTP (sync) |
| **Payment Svc** | Events (async) | - | - |
| **Shipping Svc** | Events (async) | - | - |

### Detailed Architecture Diagram

```
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│                                        FRONTEND                                             │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
│  │                              Angular Web Application                                 │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                │   │
│  │  │  Checkout   │  │   Order     │  │  Payment    │  │  Stripe.js  │                │   │
│  │  │  Component  │  │  History    │  │  Methods    │  │  Elements   │                │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └──────┬──────┘                │   │
│  └───────────────────────────────────────────────────────────│────────────────────────┘   │
└──────────────────────────────────────────────────────────────│────────────────────────────┘
                                                               │ Card Tokenization
                                                               ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                      STRIPE                                               │
│                              (PCI Compliant Card Handling)                                │
└──────────────────────────────────────────────────────────────────────────────────────────┘
                                           │
                                           │ pm_xxx token
                                           ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                   API GATEWAY                                             │
│                              (Authentication, Routing)                                    │
└──────────────────────────────────────────────────────────────────────────────────────────┘
          │                    │                    │                    │
          ▼                    ▼                    ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│   AUTH SERVICE   │ │  ORDER SERVICE   │ │ PAYMENT SERVICE  │ │SHIPPING SERVICE  │
│                  │ │   (Orchestrator) │ │                  │ │                  │
│ ┌──────────────┐ │ │ ┌──────────────┐ │ │ ┌──────────────┐ │ │ ┌──────────────┐ │
│ │    Users     │ │ │ │    Orders    │ │ │ │   Payments   │ │ │ │  Shipments   │ │
│ │   Addresses  │ │ │ │ Order Items  │ │ │ │   Refunds    │ │ │ │   Tracking   │ │
│ │   Sessions   │ │ │ │    Sagas     │ │ │ │ Pay Methods  │ │ │ │    Rates     │ │
│ └──────────────┘ │ │ └──────────────┘ │ │ └──────────────┘ │ │ └──────────────┘ │
│        │         │ │        │         │ │        │         │ │        │         │
│        ▼         │ │        ▼         │ │        ▼         │ │        ▼         │
│   ┌────────┐     │ │   ┌────────┐     │ │   ┌────────┐     │ │   ┌────────┐     │
│   │DynamoDB│     │ │   │PostgreSQL│   │ │   │PostgreSQL│   │ │   │PostgreSQL│   │
│   └────────┘     │ │   └────────┘     │ │   └────────┘     │ │   └────────┘     │
└──────────────────┘ └────────┬─────────┘ └────────┬─────────┘ └────────┬─────────┘
                              │                    │                    │
                              │    HTTP (sync)     │                    │
                              │◀──────────────────▶│                    │
                              │◀───────────────────────────────────────▶│
                              │                    │                    │
                              │         Events (async via SNS/SQS)      │
                              └────────────────────┼────────────────────┘
                                                   │
                              ┌────────────────────▼────────────────────┐
                              │              AWS SNS Topics              │
                              │  ┌─────────────────────────────────┐    │
                              │  │ gearify-payment-events          │    │
                              │  │ gearify-shipping-events         │    │
                              │  │ gearify-order-events            │    │
                              │  └─────────────────────────────────┘    │
                              └────────────────────┬────────────────────┘
                                                   │
                    ┌──────────────────────────────┼──────────────────────────────┐
                    │                              │                              │
                    ▼                              ▼                              ▼
        ┌───────────────────┐          ┌───────────────────┐          ┌───────────────────┐
        │ order-svc-payment │          │ order-svc-shipping│          │notification-order │
        │    -events (SQS)  │          │    -events (SQS)  │          │    -events (SQS)  │
        └─────────┬─────────┘          └─────────┬─────────┘          └─────────┬─────────┘
                  │                              │                              │
                  ▼                              ▼                              ▼
        ┌──────────────────┐          ┌──────────────────┐          ┌──────────────────┐
        │  Order Service   │          │  Order Service   │          │Notification Svc  │
        │  (Consumer)      │          │  (Consumer)      │          │  (Consumer)      │
        └──────────────────┘          └──────────────────┘          └──────────────────┘
```

---

## Sequence Diagrams

### 1. Checkout Flow (Happy Path)

```
┌────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│Frontend│     │Order Svc│     │Payment  │     │ Stripe  │     │Shipping │     │   SNS   │
│        │     │         │     │   Svc   │     │   API   │     │   Svc   │     │         │
└───┬────┘     └────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
    │               │               │               │               │               │
    │ POST /checkout│               │               │               │               │
    │ {cartId,      │               │               │               │               │
    │  addressId,   │               │               │               │               │
    │  paymentMethodId}             │               │               │               │
    │──────────────▶│               │               │               │               │
    │               │               │               │               │               │
    │               │ Create Order  │               │               │               │
    │               │ (pending_payment)             │               │               │
    │               │───────┐       │               │               │               │
    │               │       │       │               │               │               │
    │               │◀──────┘       │               │               │               │
    │               │               │               │               │               │
    │               │ POST /payments│               │               │               │
    │               │ {orderId,     │               │               │               │
    │               │  amount,      │               │               │               │
    │               │  paymentMethodId}             │               │               │
    │               │──────────────▶│               │               │               │
    │               │               │               │               │               │
    │               │               │ Create        │               │               │
    │               │               │ PaymentIntent │               │               │
    │               │               │──────────────▶│               │               │
    │               │               │               │               │               │
    │               │               │ pi_xxx        │               │               │
    │               │               │ (succeeded)   │               │               │
    │               │               │◀──────────────│               │               │
    │               │               │               │               │               │
    │               │ {paymentId,   │               │               │               │
    │               │  status:      │               │               │               │
    │               │  succeeded}   │               │               │               │
    │               │◀──────────────│               │               │               │
    │               │               │               │               │               │
    │               │               │ Publish       │               │               │
    │               │               │ PaymentSucceededEvent         │               │
    │               │               │──────────────────────────────────────────────▶│
    │               │               │               │               │               │
    │               │ POST /shipments               │               │               │
    │               │ {orderId,     │               │               │               │
    │               │  address,     │               │               │               │
    │               │  items}       │               │               │               │
    │               │──────────────────────────────────────────────▶│               │
    │               │               │               │               │               │
    │               │               │               │               │ Create        │
    │               │               │               │               │ Shipment      │
    │               │               │               │               │───────┐       │
    │               │               │               │               │       │       │
    │               │               │               │               │◀──────┘       │
    │               │               │               │               │               │
    │               │ {shipmentId,  │               │               │               │
    │               │  trackingNum} │               │               │               │
    │               │◀─────────────────────────────────────────────│               │
    │               │               │               │               │               │
    │               │ Update Order  │               │               │               │
    │               │ (confirmed)   │               │               │               │
    │               │───────┐       │               │               │               │
    │               │       │       │               │               │               │
    │               │◀──────┘       │               │               │               │
    │               │               │               │               │               │
    │ {orderId,     │               │               │               │               │
    │  orderNumber, │               │               │               │               │
    │  status:      │               │               │               │               │
    │  confirmed}   │               │               │               │               │
    │◀──────────────│               │               │               │               │
    │               │               │               │               │               │
```

### 2. Checkout with 3D Secure Authentication

```
┌────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│Frontend│     │Order Svc│     │Payment  │     │ Stripe  │     │   SNS   │
│        │     │         │     │   Svc   │     │   API   │     │         │
└───┬────┘     └────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
    │               │               │               │               │
    │ POST /checkout│               │               │               │
    │──────────────▶│               │               │               │
    │               │               │               │               │
    │               │ Create Order  │               │               │
    │               │ (pending_payment)             │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │               │ POST /payments│               │               │
    │               │──────────────▶│               │               │
    │               │               │ Create        │               │
    │               │               │ PaymentIntent │               │
    │               │               │──────────────▶│               │
    │               │               │               │               │
    │               │               │ pi_xxx        │               │
    │               │               │ requires_action               │
    │               │               │ + client_secret               │
    │               │               │◀──────────────│               │
    │               │               │               │               │
    │               │ {requires_action: true,       │               │
    │               │  clientSecret}│               │               │
    │               │◀──────────────│               │               │
    │               │               │               │               │
    │ {requires_action: true,       │               │               │
    │  clientSecret}│               │               │               │
    │◀──────────────│               │               │               │
    │               │               │               │               │
    │ stripe.confirmCardPayment     │               │               │
    │ (3DS Modal)   │               │               │               │
    │──────────────────────────────────────────────▶│               │
    │               │               │               │               │
    │ User completes 3DS            │               │               │
    │◀─────────────────────────────────────────────│               │
    │               │               │               │               │
    │               │               │               │ Webhook:      │
    │               │               │               │ payment_intent│
    │               │               │               │ .succeeded    │
    │               │               │◀──────────────│               │
    │               │               │               │               │
    │               │               │ Update Payment│               │
    │               │               │ (succeeded)   │               │
    │               │               │───────┐       │               │
    │               │               │◀──────┘       │               │
    │               │               │               │               │
    │               │               │ Publish       │               │
    │               │               │ PaymentSucceededEvent         │
    │               │               │──────────────────────────────▶│
    │               │               │               │               │
    │               │ SQS: PaymentSucceededEvent    │               │
    │               │◀─────────────────────────────────────────────│
    │               │               │               │               │
    │               │ Continue Saga │               │               │
    │               │ (Create Shipment, etc.)       │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │ Poll: GET /orders/{id}        │               │               │
    │──────────────▶│               │               │               │
    │               │               │               │               │
    │ {status: confirmed}           │               │               │
    │◀──────────────│               │               │               │
```

### 3. Payment Failure and Compensation (Saga Rollback)

```
┌────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│Frontend│     │Order Svc│     │Payment  │     │ Stripe  │     │   SNS   │
│        │     │         │     │   Svc   │     │   API   │     │         │
└───┬────┘     └────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
    │               │               │               │               │
    │ POST /checkout│               │               │               │
    │──────────────▶│               │               │               │
    │               │               │               │               │
    │               │ [STEP 1]      │               │               │
    │               │ Create Order  │               │               │
    │               │ (pending_payment)             │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │               │ [STEP 2]      │               │               │
    │               │ POST /payments│               │               │
    │               │──────────────▶│               │               │
    │               │               │ Create        │               │
    │               │               │ PaymentIntent │               │
    │               │               │──────────────▶│               │
    │               │               │               │               │
    │               │               │ card_declined │               │
    │               │               │◀──────────────│               │
    │               │               │               │               │
    │               │ {success: false,              │               │
    │               │  error: "card_declined"}      │               │
    │               │◀──────────────│               │               │
    │               │               │               │               │
    │               │═══════════════════════════════════════════════│
    │               │       SAGA COMPENSATION STARTS                │
    │               │═══════════════════════════════════════════════│
    │               │               │               │               │
    │               │ [COMPENSATE STEP 1]           │               │
    │               │ Update Order  │               │               │
    │               │ (payment_failed)              │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │ {success: false,              │               │               │
    │  error: "Payment declined",   │               │               │
    │  orderId (for retry)}         │               │               │
    │◀──────────────│               │               │               │
    │               │               │               │               │
    │ Show error    │               │               │               │
    │ "Card declined, try another"  │               │               │
    │───────┐       │               │               │               │
    │◀──────┘       │               │               │               │
```

### 4. Refund Flow

```
┌────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│ Admin  │     │Order Svc│     │Payment  │     │ Stripe  │     │   SNS   │
│  UI    │     │         │     │   Svc   │     │   API   │     │         │
└───┬────┘     └────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
    │               │               │               │               │
    │ POST /orders/{id}/refund      │               │               │
    │ {amount, reason}              │               │               │
    │──────────────▶│               │               │               │
    │               │               │               │               │
    │               │ Validate Order│               │               │
    │               │ (can be refunded)             │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │               │ POST /payments/{paymentId}/refund             │
    │               │ {amount, reason}              │               │
    │               │──────────────▶│               │               │
    │               │               │               │               │
    │               │               │ Create Refund │               │
    │               │               │──────────────▶│               │
    │               │               │               │               │
    │               │               │ re_xxx        │               │
    │               │               │ (succeeded)   │               │
    │               │               │◀──────────────│               │
    │               │               │               │               │
    │               │ {refundId,    │               │               │
    │               │  status:      │               │               │
    │               │  succeeded}   │               │               │
    │               │◀──────────────│               │               │
    │               │               │               │               │
    │               │               │ Publish       │               │
    │               │               │ RefundCompletedEvent          │
    │               │               │──────────────────────────────▶│
    │               │               │               │               │
    │               │ SQS: RefundCompletedEvent     │               │
    │               │◀─────────────────────────────────────────────│
    │               │               │               │               │
    │               │ Update Order  │               │               │
    │               │ (refunded)    │               │               │
    │               │───────┐       │               │               │
    │               │◀──────┘       │               │               │
    │               │               │               │               │
    │ {success: true,               │               │               │
    │  refundId}    │               │               │               │
    │◀──────────────│               │               │               │
```

### 5. Order Status Lifecycle

```
                                    ┌─────────────────┐
                                    │      START      │
                                    └────────┬────────┘
                                             │
                                             ▼
                                    ┌─────────────────┐
                                    │ pending_payment │
                                    └────────┬────────┘
                                             │
                    ┌────────────────────────┼────────────────────────┐
                    │                        │                        │
                    ▼                        ▼                        ▼
           ┌───────────────┐        ┌───────────────┐        ┌───────────────┐
           │payment_failed │        │   confirmed   │        │   cancelled   │
           │               │        │               │        │  (by user)    │
           └───────┬───────┘        └───────┬───────┘        └───────────────┘
                   │                        │
                   │ Retry                  ▼
                   │ Payment       ┌───────────────┐
                   └──────────────▶│  processing   │
                                   │(being prepared)│
                                   └───────┬───────┘
                                           │
                                           ▼
                                   ┌───────────────┐
                                   │    shipped    │
                                   │               │
                                   └───────┬───────┘
                                           │
                                           ▼
                                   ┌───────────────┐
                                   │   delivered   │
                                   │               │
                                   └───────┬───────┘
                                           │
                              ┌────────────┴────────────┐
                              │                         │
                              ▼                         ▼
                     ┌───────────────┐         ┌───────────────┐
                     │   completed   │         │    refunded   │
                     │               │         │               │
                     └───────────────┘         └───────────────┘
```

---

## Service Responsibilities

### gearify-order-svc (Orchestrator)

**Owns:**
- Orders
- Order Items
- Order Status History
- Checkout orchestration

**Responsibilities:**
- Create and manage orders
- Coordinate checkout flow (saga orchestrator)
- Track order status
- Handle order cancellation
- Aggregate data for order details

**Does NOT own:**
- Payment processing (delegates to payment-svc)
- Shipping rates/tracking (delegates to shipping-svc)
- User data (fetches from auth-svc)

### gearify-payment-svc

**Owns:**
- Payments
- Payment Transactions (audit log)
- Saved Payment Methods
- Refunds
- Stripe customer mapping

**Responsibilities:**
- Stripe integration
- Create/confirm payment intents
- Manage saved payment methods
- Process refunds
- Handle Stripe webhooks
- PCI compliance scope

**Does NOT own:**
- Orders (receives order reference)
- User management

### gearify-shipping-svc

**Owns:**
- Shipments
- Shipping Methods
- Shipping Rates
- Carrier Configurations
- Tracking Events

**Responsibilities:**
- Calculate shipping rates
- Manage shipping methods (standard, express, etc.)
- Carrier integration (FedEx, UPS, USPS)
- Create shipment labels
- Track shipments
- Handle delivery webhooks

**Does NOT own:**
- Shipping addresses (stored in auth-svc)
- Orders

---

## Data Store Strategy

| Service | Database | Reason |
|---------|----------|--------|
| **Order Service** | PostgreSQL | ACID for orders, saga state |
| **Payment Service** | PostgreSQL | ACID for payments, audit trail |
| **Shipping Service** | PostgreSQL | ACID for shipments, tracking |
| Auth Service | DynamoDB | Read-heavy, user-scoped |
| Cart Service | DynamoDB | Ephemeral, TTL-based |
| Catalog Service | DynamoDB | Read-heavy, cached |

---

## Database Schema

### Schema by Service

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            gearify-order-svc (PostgreSQL)                        │
├─────────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────────────┐   │
│  │      orders      │    │   order_items    │    │   order_status_history   │   │
│  ├──────────────────┤    ├──────────────────┤    ├──────────────────────────┤   │
│  │ id               │───▶│ order_id (FK)    │    │ order_id (FK)            │   │
│  │ order_number     │    │ product_id       │    │ from_status              │   │
│  │ user_id          │    │ sku, name        │    │ to_status                │   │
│  │ status           │    │ quantity         │    │ changed_by               │   │
│  │ payment_id (ref) │    │ unit_price       │    │ created_at               │   │
│  │ shipment_id (ref)│    │ total_price      │    └──────────────────────────┘   │
│  │ shipping_address │    └──────────────────┘                                   │
│  │ amounts...       │    ┌──────────────────┐                                   │
│  └──────────────────┘    │   order_sagas    │  (Saga state tracking)            │
│                          ├──────────────────┤                                   │
│                          │ order_id         │                                   │
│                          │ saga_status      │                                   │
│                          │ current_step     │                                   │
│                          │ compensation_log │                                   │
│                          └──────────────────┘                                   │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                          gearify-payment-svc (PostgreSQL)                        │
├─────────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────────────┐   │
│  │     payments     │    │ payment_transactions│  │  saved_payment_methods   │   │
│  ├──────────────────┤    ├──────────────────┤    ├──────────────────────────┤   │
│  │ id               │───▶│ payment_id (FK)  │    │ id                       │   │
│  │ order_id (ref)   │    │ event_type       │    │ user_id                  │   │
│  │ user_id          │    │ amount           │    │ stripe_customer_id       │   │
│  │ stripe_payment_  │    │ status           │    │ stripe_payment_method_id │   │
│  │   intent_id      │    │ stripe_event_id  │    │ card_brand, card_last4   │   │
│  │ amount, currency │    │ error_details    │    │ exp_month, exp_year      │   │
│  │ status           │    └──────────────────┘    │ is_default               │   │
│  │ card_last4       │                            └──────────────────────────┘   │
│  └──────────────────┘    ┌──────────────────┐                                   │
│                          │     refunds      │                                   │
│  ┌──────────────────┐    ├──────────────────┤                                   │
│  │ stripe_customers │    │ payment_id (FK)  │                                   │
│  ├──────────────────┤    │ stripe_refund_id │                                   │
│  │ user_id          │    │ amount           │                                   │
│  │ tenant_id        │    │ reason, status   │                                   │
│  │ stripe_customer_ │    └──────────────────┘                                   │
│  │   id             │                                                           │
│  └──────────────────┘                                                           │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                         gearify-shipping-svc (PostgreSQL)                        │
├─────────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────────────┐   │
│  │    shipments     │    │ shipping_methods │    │   tracking_events        │   │
│  ├──────────────────┤    ├──────────────────┤    ├──────────────────────────┤   │
│  │ id               │    │ id               │    │ shipment_id (FK)         │   │
│  │ order_id (ref)   │    │ name             │    │ status                   │   │
│  │ carrier          │    │ carrier          │    │ location                 │   │
│  │ tracking_number  │    │ estimated_days   │    │ description              │   │
│  │ status           │    │ base_rate        │    │ timestamp                │   │
│  │ label_url        │    │ is_active        │    └──────────────────────────┘   │
│  │ estimated_       │    └──────────────────┘                                   │
│  │   delivery       │    ┌──────────────────┐                                   │
│  │ shipped_at       │    │ shipping_rates   │  (Cached rate calculations)       │
│  │ delivered_at     │    ├──────────────────┤                                   │
│  └──────────────────┘    │ origin_zip       │                                   │
│                          │ dest_zip         │                                   │
│                          │ weight           │                                   │
│                          │ rates_json       │                                   │
│                          │ expires_at       │                                   │
│                          └──────────────────┘                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Cross-Service References

Services reference each other's entities by ID only (no foreign keys across databases):

| Service | Stores Reference To | Reference Field |
|---------|---------------------|-----------------|
| Order | Payment | `payment_id` (UUID) |
| Order | Shipment | `shipment_id` (UUID) |
| Payment | Order | `order_id` (UUID) |
| Shipment | Order | `order_id` (UUID) |

### SQL Schema

```sql
-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================
-- ORDERS TABLE
-- ============================================
CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id VARCHAR(50) NOT NULL,
    user_id VARCHAR(50) NOT NULL,
    order_number VARCHAR(20) UNIQUE NOT NULL,

    -- Status: pending_payment, confirmed, processing, shipped, delivered, cancelled, refunded
    status VARCHAR(30) NOT NULL DEFAULT 'pending_payment',

    -- Shipping Information (stored as JSON for flexibility)
    shipping_address JSONB NOT NULL,
    -- Example: {"firstName": "John", "lastName": "Doe", "line1": "123 Main St",
    --           "line2": "Apt 4", "city": "NYC", "state": "NY", "zipCode": "10001",
    --           "country": "US", "phone": "+1234567890"}

    shipping_method VARCHAR(50),
    shipping_cost DECIMAL(10,2) NOT NULL DEFAULT 0,
    estimated_delivery_date DATE,

    -- Amounts
    subtotal DECIMAL(10,2) NOT NULL,
    tax_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    total_amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',

    -- Discount/Promo
    promo_code VARCHAR(50),

    -- Notes
    customer_notes TEXT,
    internal_notes TEXT,

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    confirmed_at TIMESTAMP WITH TIME ZONE,
    shipped_at TIMESTAMP WITH TIME ZONE,
    delivered_at TIMESTAMP WITH TIME ZONE,
    cancelled_at TIMESTAMP WITH TIME ZONE,

    -- Idempotency
    idempotency_key VARCHAR(100) UNIQUE
);

CREATE INDEX idx_orders_tenant_user ON orders(tenant_id, user_id);
CREATE INDEX idx_orders_status ON orders(tenant_id, status);
CREATE INDEX idx_orders_number ON orders(order_number);
CREATE INDEX idx_orders_created ON orders(created_at DESC);

-- ============================================
-- ORDER ITEMS TABLE
-- ============================================
CREATE TABLE order_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,

    -- Product snapshot (denormalized for historical accuracy)
    product_id VARCHAR(50) NOT NULL,
    sku VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    image_url VARCHAR(500),

    -- Pricing
    quantity INT NOT NULL CHECK (quantity > 0),
    unit_price DECIMAL(10,2) NOT NULL,
    discount_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    total_price DECIMAL(10,2) NOT NULL,

    -- Product attributes at time of purchase
    attributes JSONB,
    -- Example: {"size": "L", "color": "Blue"}

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE INDEX idx_order_items_product ON order_items(product_id);

-- ============================================
-- SAVED PAYMENT METHODS TABLE
-- ============================================
CREATE TABLE saved_payment_methods (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id VARCHAR(50) NOT NULL,
    user_id VARCHAR(50) NOT NULL,

    -- Stripe identifiers
    stripe_customer_id VARCHAR(100) NOT NULL,
    stripe_payment_method_id VARCHAR(100) NOT NULL UNIQUE,

    -- Card display info (non-sensitive)
    card_brand VARCHAR(20) NOT NULL,  -- visa, mastercard, amex, discover
    card_last4 VARCHAR(4) NOT NULL,
    exp_month INT NOT NULL,
    exp_year INT NOT NULL,
    cardholder_name VARCHAR(100),

    -- Billing address
    billing_address JSONB,

    -- Flags
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    UNIQUE(tenant_id, user_id, stripe_payment_method_id)
);

CREATE INDEX idx_saved_payment_methods_user ON saved_payment_methods(tenant_id, user_id);
CREATE INDEX idx_saved_payment_methods_stripe ON saved_payment_methods(stripe_customer_id);

-- ============================================
-- PAYMENTS TABLE
-- ============================================
CREATE TABLE payments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL REFERENCES orders(id),
    tenant_id VARCHAR(50) NOT NULL,
    user_id VARCHAR(50) NOT NULL,

    -- Stripe identifiers
    stripe_payment_intent_id VARCHAR(100) UNIQUE,
    stripe_customer_id VARCHAR(100),
    stripe_payment_method_id VARCHAR(100),

    -- Payment details
    amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',

    -- Status: pending, requires_action, processing, succeeded, failed, cancelled, refunded
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    failure_reason TEXT,

    -- Card info (non-sensitive, for display)
    card_brand VARCHAR(20),
    card_last4 VARCHAR(4),

    -- 3D Secure
    requires_action BOOLEAN DEFAULT false,
    client_secret VARCHAR(200),  -- For frontend to complete payment

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    succeeded_at TIMESTAMP WITH TIME ZONE,
    failed_at TIMESTAMP WITH TIME ZONE,

    -- Idempotency
    idempotency_key VARCHAR(100) UNIQUE
);

CREATE INDEX idx_payments_order ON payments(order_id);
CREATE INDEX idx_payments_stripe_pi ON payments(stripe_payment_intent_id);
CREATE INDEX idx_payments_status ON payments(tenant_id, status);
CREATE INDEX idx_payments_user ON payments(tenant_id, user_id);

-- ============================================
-- PAYMENT TRANSACTIONS TABLE (Audit Log)
-- ============================================
CREATE TABLE payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    payment_id UUID NOT NULL REFERENCES payments(id),

    -- Transaction type: payment_intent.created, payment_intent.succeeded,
    -- charge.succeeded, charge.refunded, etc.
    event_type VARCHAR(50) NOT NULL,

    -- Amount involved in this transaction
    amount DECIMAL(10,2) NOT NULL,

    -- Status after this transaction
    status VARCHAR(30) NOT NULL,

    -- Stripe event details
    stripe_event_id VARCHAR(100) UNIQUE,
    stripe_event_data JSONB,

    -- Error details if failed
    error_code VARCHAR(50),
    error_message TEXT,

    -- Metadata
    ip_address VARCHAR(45),
    user_agent TEXT,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_payment_transactions_payment ON payment_transactions(payment_id);
CREATE INDEX idx_payment_transactions_event ON payment_transactions(stripe_event_id);
CREATE INDEX idx_payment_transactions_type ON payment_transactions(event_type);

-- ============================================
-- REFUNDS TABLE
-- ============================================
CREATE TABLE refunds (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    payment_id UUID NOT NULL REFERENCES payments(id),
    order_id UUID NOT NULL REFERENCES orders(id),
    tenant_id VARCHAR(50) NOT NULL,

    -- Stripe identifiers
    stripe_refund_id VARCHAR(100) UNIQUE,

    -- Refund details
    amount DECIMAL(10,2) NOT NULL,
    reason VARCHAR(50),  -- duplicate, fraudulent, requested_by_customer, other
    description TEXT,

    -- Status: pending, succeeded, failed, cancelled
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    failure_reason TEXT,

    -- Who initiated
    initiated_by VARCHAR(50) NOT NULL,  -- customer, admin, system
    admin_user_id VARCHAR(50),

    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    processed_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_refunds_payment ON refunds(payment_id);
CREATE INDEX idx_refunds_order ON refunds(order_id);
CREATE INDEX idx_refunds_status ON refunds(tenant_id, status);

-- ============================================
-- ORDER STATUS HISTORY TABLE
-- ============================================
CREATE TABLE order_status_history (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,

    from_status VARCHAR(30),
    to_status VARCHAR(30) NOT NULL,

    changed_by VARCHAR(50),  -- user_id or 'system'
    reason TEXT,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_order_status_history_order ON order_status_history(order_id);

-- ============================================
-- HELPER FUNCTIONS
-- ============================================

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Apply to tables
CREATE TRIGGER update_orders_updated_at BEFORE UPDATE ON orders
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_payments_updated_at BEFORE UPDATE ON payments
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_saved_payment_methods_updated_at BEFORE UPDATE ON saved_payment_methods
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Function to generate order number
CREATE OR REPLACE FUNCTION generate_order_number()
RETURNS VARCHAR(20) AS $$
DECLARE
    new_number VARCHAR(20);
BEGIN
    new_number := 'ORD-' || TO_CHAR(NOW(), 'YYYYMMDD') || '-' ||
                  UPPER(SUBSTRING(MD5(RANDOM()::TEXT) FROM 1 FOR 6));
    RETURN new_number;
END;
$$ LANGUAGE plpgsql;
```

---

## Inter-Service Communication

### Communication Patterns

| Pattern | Use Case | Example |
|---------|----------|---------|
| **Sync HTTP** | Need immediate response | Order → Payment: Create payment intent |
| **Async Events** | Notify other services | Payment → Order: Payment succeeded |
| **Request/Reply** | Complex queries | Order → Shipping: Get rates |

### Service Clients

Each service exposes a typed HTTP client for other services:

```csharp
// In gearify-order-svc
public interface IPaymentServiceClient
{
    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request);
    Task<PaymentDetails> GetPaymentAsync(Guid paymentId);
    Task<RefundResult> CreateRefundAsync(Guid paymentId, decimal amount, string reason);
}

public interface IShippingServiceClient
{
    Task<IEnumerable<ShippingRate>> GetRatesAsync(GetRatesRequest request);
    Task<ShipmentResult> CreateShipmentAsync(CreateShipmentRequest request);
    Task<ShipmentDetails> GetShipmentAsync(Guid shipmentId);
}
```

### Event Contracts (Published Events)

```csharp
// Payment Service publishes:
public record PaymentSucceededEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    DateTime Timestamp
);

public record PaymentFailedEvent(
    Guid PaymentId,
    Guid OrderId,
    string ErrorCode,
    string ErrorMessage,
    DateTime Timestamp
);

public record RefundCompletedEvent(
    Guid RefundId,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    DateTime Timestamp
);

// Shipping Service publishes:
public record ShipmentCreatedEvent(
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    DateTime EstimatedDelivery
);

public record ShipmentDeliveredEvent(
    Guid ShipmentId,
    Guid OrderId,
    DateTime DeliveredAt
);

// Order Service publishes:
public record OrderCreatedEvent(
    Guid OrderId,
    string OrderNumber,
    string UserId,
    decimal TotalAmount
);

public record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    DateTime CancelledAt
);
```

### Message Broker Configuration (AWS SNS/SQS)

Using AWS SNS for publishing and SQS for consuming, consistent with existing Gearify services.

#### SNS Topics

| Topic | Publisher | Description |
|-------|-----------|-------------|
| `gearify-payment-events` | Payment Service | Payment succeeded, failed, refunded |
| `gearify-shipping-events` | Shipping Service | Shipment created, shipped, delivered |
| `gearify-order-events` | Order Service | Order created, cancelled, status changed |

#### SQS Queues

| Queue | Subscriber | Subscribes To |
|-------|------------|---------------|
| `order-svc-payment-events` | Order Service | gearify-payment-events |
| `order-svc-shipping-events` | Order Service | gearify-shipping-events |
| `notification-svc-order-events` | Notification Service | gearify-order-events |

#### Infrastructure Setup (LocalStack)

```bash
# Create SNS Topics
aws --endpoint-url=http://localhost:4566 sns create-topic --name gearify-payment-events
aws --endpoint-url=http://localhost:4566 sns create-topic --name gearify-shipping-events
aws --endpoint-url=http://localhost:4566 sns create-topic --name gearify-order-events

# Create SQS Queues
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name order-svc-payment-events
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name order-svc-shipping-events

# Subscribe Queues to Topics
aws --endpoint-url=http://localhost:4566 sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-payment-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:order-svc-payment-events
```

#### Publisher Implementation

```csharp
// Infrastructure/Messaging/SnsEventPublisher.cs
public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IOptions<MessagingConfiguration> _config;
    private readonly ILogger<SnsEventPublisher> _logger;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService sns,
        IOptions<MessagingConfiguration> config,
        ILogger<SnsEventPublisher> logger)
    {
        _sns = sns;
        _config = config;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event) where T : class, IIntegrationEvent
    {
        var topicArn = GetTopicArn<T>();
        var message = JsonSerializer.Serialize(@event);

        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = message,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = typeof(T).Name
                },
                ["Timestamp"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = DateTime.UtcNow.ToString("O")
                }
            }
        };

        try
        {
            await _sns.PublishAsync(request);
            _logger.LogInformation("Published {EventType} to {Topic}", typeof(T).Name, topicArn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType}", typeof(T).Name);
            throw;
        }
    }

    private string GetTopicArn<T>() => typeof(T).Name switch
    {
        nameof(PaymentSucceededEvent) => _config.Value.PaymentEventsTopicArn,
        nameof(PaymentFailedEvent) => _config.Value.PaymentEventsTopicArn,
        nameof(RefundCompletedEvent) => _config.Value.PaymentEventsTopicArn,
        nameof(ShipmentCreatedEvent) => _config.Value.ShippingEventsTopicArn,
        nameof(ShipmentDeliveredEvent) => _config.Value.ShippingEventsTopicArn,
        nameof(OrderCreatedEvent) => _config.Value.OrderEventsTopicArn,
        nameof(OrderCancelledEvent) => _config.Value.OrderEventsTopicArn,
        _ => throw new ArgumentException($"Unknown event type: {typeof(T).Name}")
    };
}
```

#### Consumer Implementation (Background Service)

```csharp
// Infrastructure/Messaging/SqsEventConsumer.cs
public class SqsEventConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<MessagingConfiguration> _config;
    private readonly ILogger<SqsEventConsumer> _logger;

    public SqsEventConsumer(
        IAmazonSQS sqs,
        IServiceProvider serviceProvider,
        IOptions<MessagingConfiguration> config,
        ILogger<SqsEventConsumer> logger)
    {
        _sqs = sqs;
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = _config.Value.PaymentEventsQueueUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20, // Long polling
                    MessageAttributeNames = new List<string> { "All" }
                }, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, queueUrl, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving messages from SQS");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, string queueUrl, CancellationToken ct)
    {
        try
        {
            // SNS wraps the message in an envelope
            var snsEnvelope = JsonSerializer.Deserialize<SnsEnvelope>(message.Body);
            var eventType = snsEnvelope?.MessageAttributes?["EventType"]?.Value;

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            switch (eventType)
            {
                case nameof(PaymentSucceededEvent):
                    var paymentSucceeded = JsonSerializer.Deserialize<PaymentSucceededEvent>(snsEnvelope.Message);
                    await mediator.Send(new HandlePaymentSucceededCommand(paymentSucceeded), ct);
                    break;

                case nameof(PaymentFailedEvent):
                    var paymentFailed = JsonSerializer.Deserialize<PaymentFailedEvent>(snsEnvelope.Message);
                    await mediator.Send(new HandlePaymentFailedCommand(paymentFailed), ct);
                    break;

                // ... other event types
            }

            // Delete message after successful processing
            await _sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
            _logger.LogInformation("Processed {EventType} message", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
            // Message will return to queue after visibility timeout
        }
    }
}

// SNS envelope structure
public record SnsEnvelope
{
    public string Message { get; init; }
    public Dictionary<string, SnsMessageAttribute> MessageAttributes { get; init; }
}

public record SnsMessageAttribute
{
    public string Type { get; init; }
    public string Value { get; init; }
}
```

#### Configuration

```json
// appsettings.json
{
  "Messaging": {
    "PaymentEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-payment-events",
    "ShippingEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-shipping-events",
    "OrderEventsTopicArn": "arn:aws:sns:us-east-1:000000000000:gearify-order-events",
    "PaymentEventsQueueUrl": "http://localhost:4566/000000000000/order-svc-payment-events",
    "ShippingEventsQueueUrl": "http://localhost:4566/000000000000/order-svc-shipping-events"
  }
}
```

#### DI Registration

```csharp
// Startup.cs or Program.cs
services.AddAWSService<IAmazonSimpleNotificationService>();
services.AddAWSService<IAmazonSQS>();

services.Configure<MessagingConfiguration>(Configuration.GetSection("Messaging"));
services.AddScoped<IEventPublisher, SnsEventPublisher>();
services.AddHostedService<SqsEventConsumer>();
```

---

## Distributed Transaction (Saga Pattern)

### Why Saga Pattern?

With separate databases for Order, Payment, and Shipping services, we cannot use traditional ACID transactions. The **Saga Pattern** handles distributed transactions through a sequence of local transactions with compensating actions for rollback.

### Checkout Saga (Orchestration)

The Order Service acts as the **Saga Orchestrator**:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           CHECKOUT SAGA FLOW                                     │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│   ┌─────────┐      ┌─────────┐      ┌─────────┐      ┌─────────┐               │
│   │ Step 1  │─────▶│ Step 2  │─────▶│ Step 3  │─────▶│ Step 4  │               │
│   │ Create  │      │ Reserve │      │ Process │      │ Create  │               │
│   │ Order   │      │Inventory│      │ Payment │      │Shipment │               │
│   │(pending)│      │         │      │         │      │         │               │
│   └────┬────┘      └────┬────┘      └────┬────┘      └────┬────┘               │
│        │                │                │                │                     │
│        │ Compensate     │ Compensate     │ Compensate     │                     │
│        ▼                ▼                ▼                ▼                     │
│   ┌─────────┐      ┌─────────┐      ┌─────────┐      ┌─────────┐               │
│   │ Cancel  │◀─────│ Release │◀─────│ Refund  │◀─────│ Cancel  │               │
│   │ Order   │      │Inventory│      │ Payment │      │Shipment │               │
│   └─────────┘      └─────────┘      └─────────┘      └─────────┘               │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Saga States

```csharp
public enum SagaStatus
{
    Started,
    OrderCreated,
    InventoryReserved,
    PaymentProcessing,
    PaymentCompleted,
    ShipmentCreated,
    Completed,

    // Compensation states
    Compensating,
    PaymentRefunding,
    InventoryReleasing,
    OrderCancelling,
    Failed
}
```

### Saga Implementation

```csharp
// Domain/Sagas/CheckoutSaga.cs
public class CheckoutSaga
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public SagaStatus Status { get; set; }
    public string CurrentStep { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Collected IDs for compensation
    public Guid? PaymentId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string? InventoryReservationId { get; set; }

    // Error tracking
    public string? FailureReason { get; set; }
    public string? FailedStep { get; set; }

    // Compensation log
    public List<CompensationEntry> CompensationLog { get; set; } = new();
}

public record CompensationEntry(
    string Step,
    string Action,
    bool Success,
    string? Error,
    DateTime Timestamp
);
```

### Saga Orchestrator

```csharp
// Application/Sagas/CheckoutSagaOrchestrator.cs
public class CheckoutSagaOrchestrator
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISagaRepository _sagaRepository;
    private readonly IPaymentServiceClient _paymentClient;
    private readonly IShippingServiceClient _shippingClient;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<CheckoutSagaOrchestrator> _logger;

    public async Task<CheckoutResult> ExecuteAsync(CheckoutRequest request)
    {
        var saga = new CheckoutSaga
        {
            Id = Guid.NewGuid(),
            Status = SagaStatus.Started,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Create Order
            saga.CurrentStep = "CreateOrder";
            var order = await CreateOrderAsync(request);
            saga.OrderId = order.Id;
            saga.Status = SagaStatus.OrderCreated;
            await _sagaRepository.SaveAsync(saga);

            // Step 2: Reserve Inventory (if applicable)
            saga.CurrentStep = "ReserveInventory";
            saga.InventoryReservationId = await ReserveInventoryAsync(order);
            saga.Status = SagaStatus.InventoryReserved;
            await _sagaRepository.SaveAsync(saga);

            // Step 3: Process Payment
            saga.CurrentStep = "ProcessPayment";
            saga.Status = SagaStatus.PaymentProcessing;
            await _sagaRepository.SaveAsync(saga);

            var paymentResult = await _paymentClient.CreatePaymentAsync(new CreatePaymentRequest
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Currency = order.Currency,
                UserId = request.UserId,
                PaymentMethodId = request.PaymentMethodId,
                IdempotencyKey = $"order-{order.Id}"
            });

            if (!paymentResult.Success)
            {
                throw new PaymentFailedException(paymentResult.ErrorMessage);
            }

            saga.PaymentId = paymentResult.PaymentId;
            saga.Status = SagaStatus.PaymentCompleted;
            await _sagaRepository.SaveAsync(saga);

            // Step 4: Create Shipment (or schedule for later)
            saga.CurrentStep = "CreateShipment";
            var shipmentResult = await _shippingClient.CreateShipmentAsync(new CreateShipmentRequest
            {
                OrderId = order.Id,
                ShippingAddress = request.ShippingAddress,
                ShippingMethodId = request.ShippingMethodId,
                Items = order.Items.Select(i => new ShipmentItem(i.ProductId, i.Quantity)).ToList()
            });

            saga.ShipmentId = shipmentResult.ShipmentId;
            saga.Status = SagaStatus.ShipmentCreated;
            await _sagaRepository.SaveAsync(saga);

            // Complete
            saga.Status = SagaStatus.Completed;
            saga.CompletedAt = DateTime.UtcNow;
            await _sagaRepository.SaveAsync(saga);

            // Update order status
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Confirmed);

            // Publish event
            await _eventPublisher.PublishAsync(new OrderCreatedEvent(
                order.Id, order.OrderNumber, order.UserId, order.TotalAmount
            ));

            return new CheckoutResult(true, order.Id, order.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed at step {Step} for order {OrderId}",
                saga.CurrentStep, saga.OrderId);

            saga.FailureReason = ex.Message;
            saga.FailedStep = saga.CurrentStep;

            // Execute compensation
            await CompensateAsync(saga);

            return new CheckoutResult(false, null, null, ex.Message);
        }
    }

    private async Task CompensateAsync(CheckoutSaga saga)
    {
        saga.Status = SagaStatus.Compensating;
        await _sagaRepository.SaveAsync(saga);

        // Compensate in reverse order

        // Compensation: Cancel Shipment
        if (saga.ShipmentId.HasValue)
        {
            try
            {
                await _shippingClient.CancelShipmentAsync(saga.ShipmentId.Value);
                saga.CompensationLog.Add(new CompensationEntry(
                    "CancelShipment", "Success", true, null, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                saga.CompensationLog.Add(new CompensationEntry(
                    "CancelShipment", "Failed", false, ex.Message, DateTime.UtcNow));
            }
        }

        // Compensation: Refund Payment
        if (saga.PaymentId.HasValue)
        {
            saga.Status = SagaStatus.PaymentRefunding;
            await _sagaRepository.SaveAsync(saga);

            try
            {
                await _paymentClient.CreateRefundAsync(
                    saga.PaymentId.Value,
                    0, // Full refund
                    "Order saga compensation"
                );
                saga.CompensationLog.Add(new CompensationEntry(
                    "RefundPayment", "Success", true, null, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                saga.CompensationLog.Add(new CompensationEntry(
                    "RefundPayment", "Failed", false, ex.Message, DateTime.UtcNow));
            }
        }

        // Compensation: Release Inventory
        if (!string.IsNullOrEmpty(saga.InventoryReservationId))
        {
            saga.Status = SagaStatus.InventoryReleasing;
            await _sagaRepository.SaveAsync(saga);

            try
            {
                await _catalogClient.ReleaseInventoryAsync(saga.InventoryReservationId);
                saga.CompensationLog.Add(new CompensationEntry(
                    "ReleaseInventory", "Success", true, null, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                saga.CompensationLog.Add(new CompensationEntry(
                    "ReleaseInventory", "Failed", false, ex.Message, DateTime.UtcNow));
            }
        }

        // Compensation: Cancel Order
        if (saga.OrderId != Guid.Empty)
        {
            saga.Status = SagaStatus.OrderCancelling;
            await _sagaRepository.SaveAsync(saga);

            try
            {
                await _orderRepository.UpdateStatusAsync(saga.OrderId, OrderStatus.Cancelled);
                saga.CompensationLog.Add(new CompensationEntry(
                    "CancelOrder", "Success", true, null, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                saga.CompensationLog.Add(new CompensationEntry(
                    "CancelOrder", "Failed", false, ex.Message, DateTime.UtcNow));
            }
        }

        saga.Status = SagaStatus.Failed;
        await _sagaRepository.SaveAsync(saga);
    }
}
```

### Handling Async Payment (3D Secure)

When payment requires 3D Secure authentication:

```csharp
public async Task<CheckoutResult> ExecuteAsync(CheckoutRequest request)
{
    // ... previous steps ...

    // Step 3: Process Payment
    var paymentResult = await _paymentClient.CreatePaymentAsync(...);

    if (paymentResult.RequiresAction)
    {
        // Payment needs 3DS - pause saga and return client_secret
        saga.Status = SagaStatus.PaymentProcessing;
        saga.PaymentId = paymentResult.PaymentId;
        await _sagaRepository.SaveAsync(saga);

        return new CheckoutResult(
            Success: false,
            OrderId: saga.OrderId,
            RequiresAction: true,
            ClientSecret: paymentResult.ClientSecret
        );
    }

    // ... continue with completed payment ...
}

// Called after 3DS completion via webhook
public async Task HandlePaymentSucceededAsync(PaymentSucceededEvent evt)
{
    var saga = await _sagaRepository.GetByOrderIdAsync(evt.OrderId);

    if (saga?.Status == SagaStatus.PaymentProcessing)
    {
        saga.Status = SagaStatus.PaymentCompleted;
        await _sagaRepository.SaveAsync(saga);

        // Continue saga - create shipment
        await ContinueSagaAsync(saga);
    }
}
```

### Idempotency

All saga operations must be idempotent:

```csharp
public async Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request)
{
    // Check if payment already exists for this idempotency key
    var existing = await _paymentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);

    if (existing != null)
    {
        return new PaymentResult(
            Success: existing.Status == PaymentStatus.Succeeded,
            PaymentId: existing.Id,
            Status: existing.Status.ToString()
        );
    }

    // Create new payment...
}
```

---

## Payment Integration

### Stripe Integration Overview

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Browser   │────▶│  Frontend   │────▶│   Backend   │────▶│   Stripe    │
│             │     │  (Angular)  │     │  Order Svc  │     │    API      │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
      │                    │                   │                   │
      │  1. Enter card     │                   │                   │
      │─────────────────▶  │                   │                   │
      │                    │                   │                   │
      │  2. Create PaymentMethod (Stripe.js)   │                   │
      │────────────────────────────────────────────────────────────▶
      │                    │                   │                   │
      │  3. pm_xxx token   │                   │                   │
      │◀───────────────────────────────────────────────────────────│
      │                    │                   │                   │
      │                    │  4. Create Order  │                   │
      │                    │     + pm_xxx      │                   │
      │                    │─────────────────▶ │                   │
      │                    │                   │                   │
      │                    │                   │  5. Create        │
      │                    │                   │  PaymentIntent    │
      │                    │                   │─────────────────▶ │
      │                    │                   │                   │
      │                    │                   │  6. pi_xxx +      │
      │                    │                   │  client_secret    │
      │                    │                   │◀─────────────────│
      │                    │                   │                   │
      │                    │  7. Return        │                   │
      │                    │  client_secret    │                   │
      │                    │  (if 3DS needed)  │                   │
      │                    │◀─────────────────│                   │
      │                    │                   │                   │
      │  8. Confirm with   │                   │                   │
      │  3DS (if needed)   │                   │                   │
      │────────────────────────────────────────────────────────────▶
      │                    │                   │                   │
      │                    │                   │  9. Webhook:      │
      │                    │                   │  payment_intent.  │
      │                    │                   │  succeeded        │
      │                    │                   │◀─────────────────│
      │                    │                   │                   │
      │                    │  10. Order        │                   │
      │                    │  Confirmed!       │                   │
      │                    │◀─────────────────│                   │
```

### Payment Gateway Abstraction

```csharp
// Interfaces/IPaymentGateway.cs
public interface IPaymentGateway
{
    // Customer management
    Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request);
    Task<string?> GetCustomerIdAsync(string userId, string tenantId);

    // Payment methods
    Task<SavePaymentMethodResult> SavePaymentMethodAsync(SavePaymentMethodRequest request);
    Task<IEnumerable<PaymentMethodInfo>> GetPaymentMethodsAsync(string customerId);
    Task DeletePaymentMethodAsync(string paymentMethodId);

    // Payments
    Task<CreatePaymentResult> CreatePaymentIntentAsync(CreatePaymentRequest request);
    Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId);
    Task<PaymentResult> CancelPaymentAsync(string paymentIntentId);

    // Refunds
    Task<RefundResult> CreateRefundAsync(CreateRefundRequest request);

    // Webhooks
    Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature);
}

// Models
public record CreateCustomerRequest(
    string UserId,
    string TenantId,
    string Email,
    string Name
);

public record CreateCustomerResult(
    bool Success,
    string? CustomerId,
    string? ErrorMessage
);

public record CreatePaymentRequest(
    string OrderId,
    decimal Amount,
    string Currency,
    string CustomerId,
    string PaymentMethodId,
    string Description,
    string IdempotencyKey,
    Dictionary<string, string>? Metadata = null
);

public record CreatePaymentResult(
    bool Success,
    string? PaymentIntentId,
    string? ClientSecret,
    string Status,
    bool RequiresAction,
    string? ErrorCode,
    string? ErrorMessage
);
```

### Stripe Implementation

```csharp
// Infrastructure/Payment/StripePaymentGateway.cs
public class StripePaymentGateway : IPaymentGateway
{
    private readonly IOptions<StripeSettings> _settings;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(
        IOptions<StripeSettings> settings,
        ILogger<StripePaymentGateway> logger)
    {
        _settings = settings;
        _logger = logger;
        StripeConfiguration.ApiKey = settings.Value.SecretKey;
    }

    public async Task<CreatePaymentResult> CreatePaymentIntentAsync(CreatePaymentRequest request)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(request.Amount * 100), // Convert to cents
                Currency = request.Currency.ToLower(),
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Description = request.Description,
                Confirm = true, // Attempt to confirm immediately
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                },
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", request.OrderId },
                    { "idempotency_key", request.IdempotencyKey }
                }
            };

            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata)
                {
                    options.Metadata[kvp.Key] = kvp.Value;
                }
            }

            var service = new PaymentIntentService();
            var requestOptions = new RequestOptions
            {
                IdempotencyKey = request.IdempotencyKey
            };

            var paymentIntent = await service.CreateAsync(options, requestOptions);

            return new CreatePaymentResult(
                Success: paymentIntent.Status == "succeeded",
                PaymentIntentId: paymentIntent.Id,
                ClientSecret: paymentIntent.ClientSecret,
                Status: paymentIntent.Status,
                RequiresAction: paymentIntent.Status == "requires_action",
                ErrorCode: null,
                ErrorMessage: null
            );
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe payment failed for order {OrderId}", request.OrderId);

            return new CreatePaymentResult(
                Success: false,
                PaymentIntentId: null,
                ClientSecret: null,
                Status: "failed",
                RequiresAction: false,
                ErrorCode: ex.StripeError?.Code,
                ErrorMessage: ex.StripeError?.Message ?? ex.Message
            );
        }
    }

    public async Task<WebhookEvent?> ParseWebhookAsync(string payload, string signature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                _settings.Value.WebhookSecret
            );

            return new WebhookEvent(
                Id: stripeEvent.Id,
                Type: stripeEvent.Type,
                Data: stripeEvent.Data.Object,
                Created: stripeEvent.Created
            );
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to parse Stripe webhook");
            return null;
        }
    }

    // ... other methods
}
```

### Webhook Handler

```csharp
// API/Controllers/WebhookController.cs
[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IMediator _mediator;
    private readonly ILogger<WebhookController> _logger;

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"];

        var webhookEvent = await _paymentGateway.ParseWebhookAsync(json, signature);

        if (webhookEvent == null)
        {
            return BadRequest("Invalid webhook signature");
        }

        // Idempotency check - prevent duplicate processing
        if (await _eventStore.EventExistsAsync(webhookEvent.Id))
        {
            return Ok(); // Already processed
        }

        switch (webhookEvent.Type)
        {
            case "payment_intent.succeeded":
                await _mediator.Send(new PaymentSucceededCommand(webhookEvent));
                break;

            case "payment_intent.payment_failed":
                await _mediator.Send(new PaymentFailedCommand(webhookEvent));
                break;

            case "charge.refunded":
                await _mediator.Send(new RefundProcessedCommand(webhookEvent));
                break;

            default:
                _logger.LogInformation("Unhandled webhook type: {Type}", webhookEvent.Type);
                break;
        }

        return Ok();
    }
}
```

---

## Implementation Phases

### Overview

Implementation is organized by service to allow parallel development:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          IMPLEMENTATION TIMELINE                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│ Week    │ 1-2      │ 3-4       │ 5-6       │ 7-8       │ 9-10      │ 11-12    │
├─────────┼──────────┼───────────┼───────────┼───────────┼───────────┼──────────┤
│ Order   │ Setup +  │ CRUD +    │ Saga      │ Events +  │           │          │
│ Svc     │ Schema   │ Status    │ Pattern   │ Consumers │           │          │
├─────────┼──────────┼───────────┼───────────┼───────────┼───────────┼──────────┤
│ Payment │ Setup +  │ Stripe    │ Webhooks  │ Refunds   │           │          │
│ Svc     │ Schema   │ Integ.    │           │           │           │          │
├─────────┼──────────┼───────────┼───────────┼───────────┼───────────┼──────────┤
│ Shipping│          │ Setup +   │ Rates +   │ Carriers  │ Tracking  │          │
│ Svc     │          │ Schema    │ Methods   │           │           │          │
├─────────┼──────────┼───────────┼───────────┼───────────┼───────────┼──────────┤
│ Frontend│          │           │           │           │ Checkout  │ Polish   │
│         │          │           │           │           │ Flow      │ + Test   │
└─────────┴──────────┴───────────┴───────────┴───────────┴───────────┴──────────┘
```

---

### Phase 1: Infrastructure & Foundation (Week 1-2)

#### 1.1 Shared Infrastructure
- [ ] Setup PostgreSQL (single instance, multiple databases)
- [ ] Setup SNS Topics and SQS Queues (LocalStack)
- [ ] Update Docker Compose
- [ ] Setup shared contracts library

```yaml
# docker-compose.yml additions
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./init-databases.sql:/docker-entrypoint-initdb.d/init.sql
    ports:
      - "5432:5432"

  # LocalStack already exists - add SNS/SQS resources to init script
```

```sql
-- init-databases.sql
CREATE DATABASE gearify_orders;
CREATE DATABASE gearify_payments;
CREATE DATABASE gearify_shipping;
```

```bash
# Add to LocalStack init script (localstack-init.sh)
# SNS Topics
awslocal sns create-topic --name gearify-payment-events
awslocal sns create-topic --name gearify-shipping-events
awslocal sns create-topic --name gearify-order-events

# SQS Queues
awslocal sqs create-queue --queue-name order-svc-payment-events
awslocal sqs create-queue --queue-name order-svc-shipping-events
awslocal sqs create-queue --queue-name notification-svc-order-events

# Subscribe queues to topics
awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-payment-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:order-svc-payment-events

awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-shipping-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:order-svc-shipping-events

awslocal sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-order-events \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:notification-svc-order-events
```

#### 1.2 Create gearify-order-svc
- [ ] Create project with Clean Architecture
- [ ] Configure EF Core with PostgreSQL
- [ ] Setup MediatR, FluentValidation
- [ ] Configure MassTransit for events
- [ ] Health checks

```
gearify-order-svc/
├── API/
│   ├── Controllers/
│   ├── DTOs/
│   └── Consumers/          # Event consumers
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Sagas/              # Saga orchestrators
│   └── Services/
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Events/
├── Infrastructure/
│   ├── Data/
│   ├── Clients/            # HTTP clients for other services
│   └── Messaging/
└── appsettings.json
```

#### 1.3 Create gearify-payment-svc
- [ ] Create project with Clean Architecture
- [ ] Configure EF Core with PostgreSQL
- [ ] Setup Stripe SDK
- [ ] Configure MassTransit for events
- [ ] Health checks

```
gearify-payment-svc/
├── API/
│   ├── Controllers/
│   ├── DTOs/
│   └── Webhooks/           # Stripe webhook handlers
├── Application/
│   ├── Commands/
│   ├── Queries/
│   └── Services/
├── Domain/
│   ├── Entities/
│   └── Enums/
├── Infrastructure/
│   ├── Data/
│   ├── Stripe/             # Stripe implementation
│   └── Messaging/
└── appsettings.json
```

**Deliverables:**
- Two services running with database connectivity
- SNS/SQS topics and queues configured (LocalStack)
- Basic health endpoints
- Docker Compose updated

---

### Phase 2: Order Service - Core (Week 2-3)

#### 2.1 Database Schema
- [ ] Orders table
- [ ] Order items table
- [ ] Order status history table
- [ ] Order sagas table
- [ ] EF Core migrations

#### 2.2 Order CRUD
- [ ] CreateOrderCommand (initial, pending_payment status)
- [ ] GetOrderByIdQuery
- [ ] GetUserOrdersQuery (paginated)
- [ ] GetOrderByNumberQuery

#### 2.3 Order Status Management
- [ ] OrderStatus enum (pending_payment, confirmed, processing, shipped, delivered, cancelled)
- [ ] Status transition validation
- [ ] Status history tracking

#### 2.4 Service Clients
- [ ] IPaymentServiceClient interface
- [ ] IShippingServiceClient interface
- [ ] HTTP client implementations with Polly retry

#### 2.5 API Endpoints
```
POST   /api/orders              - Create order (initiates checkout saga)
GET    /api/orders              - List user orders
GET    /api/orders/{id}         - Get order details
GET    /api/orders/number/{num} - Get by order number
POST   /api/orders/{id}/cancel  - Cancel order (triggers compensation)
```

**Deliverables:**
- Order CRUD operations
- Service client abstractions
- Status workflow

---

### Phase 3: Payment Service - Core (Week 3-4)

#### 3.1 Database Schema
- [ ] Stripe customers table
- [ ] Saved payment methods table
- [ ] Payments table
- [ ] Payment transactions table (audit)
- [ ] Refunds table
- [ ] EF Core migrations

#### 3.2 Stripe Integration
- [ ] IPaymentGateway interface
- [ ] StripePaymentGateway implementation
- [ ] Customer management (create/get Stripe customer)

#### 3.3 Payment Methods
- [ ] SavePaymentMethodCommand
- [ ] GetUserPaymentMethodsQuery
- [ ] DeletePaymentMethodCommand
- [ ] SetDefaultPaymentMethodCommand

#### 3.4 Payment Processing
- [ ] CreatePaymentCommand (creates PaymentIntent)
- [ ] Handle requires_action (3DS)
- [ ] ConfirmPaymentCommand

#### 3.5 API Endpoints
```
# Payment Methods
POST   /api/payment-methods              - Save payment method
GET    /api/payment-methods              - List user's saved methods
DELETE /api/payment-methods/{id}         - Delete payment method
PUT    /api/payment-methods/{id}/default - Set as default

# Payments
POST   /api/payments                     - Create payment
GET    /api/payments/{id}                - Get payment details
GET    /api/payments/order/{orderId}     - Get payment by order
```

**Deliverables:**
- Stripe integration working
- Payment method management
- Payment creation with 3DS support

---

### Phase 4: Payment Webhooks & Events (Week 4-5)

#### 4.1 Stripe Webhook Handler
- [ ] Webhook endpoint
- [ ] Signature verification
- [ ] Idempotent event processing

#### 4.2 Webhook Events
- [ ] payment_intent.succeeded → Publish PaymentSucceededEvent
- [ ] payment_intent.payment_failed → Publish PaymentFailedEvent
- [ ] charge.refunded → Publish RefundCompletedEvent

#### 4.3 Event Publishing
- [ ] Configure MassTransit publishers
- [ ] PaymentSucceededEvent
- [ ] PaymentFailedEvent
- [ ] RefundCompletedEvent

#### 4.4 Refunds
- [ ] CreateRefundCommand
- [ ] Full and partial refunds
- [ ] Refund status tracking

#### 4.5 API Endpoints
```
POST /api/webhooks/stripe           - Stripe webhook receiver
POST /api/payments/{id}/refund      - Create refund
GET  /api/payments/{id}/refunds     - Get refund history
```

**Deliverables:**
- Webhook processing
- Event publishing
- Refund capability

---

### Phase 5: Shipping Service (Week 5-7)

#### 5.1 Project Setup (Week 5)
- [ ] Create gearify-shipping-svc
- [ ] Configure EF Core with PostgreSQL
- [ ] Setup MassTransit

```
gearify-shipping-svc/
├── API/
│   ├── Controllers/
│   └── Webhooks/           # Carrier webhook handlers
├── Application/
│   ├── Commands/
│   ├── Queries/
│   └── Services/
├── Domain/
│   ├── Entities/
│   └── Enums/
├── Infrastructure/
│   ├── Data/
│   ├── Carriers/           # Carrier integrations
│   └── Messaging/
└── appsettings.json
```

#### 5.2 Database Schema
- [ ] Shipping methods table
- [ ] Shipments table
- [ ] Tracking events table
- [ ] Shipping rates cache table

#### 5.3 Shipping Methods & Rates (Week 5-6)
- [ ] GetShippingMethodsQuery
- [ ] CalculateShippingRatesCommand
- [ ] Rate caching

#### 5.4 Shipment Management (Week 6-7)
- [ ] CreateShipmentCommand
- [ ] UpdateShipmentStatusCommand
- [ ] CancelShipmentCommand

#### 5.5 Carrier Integration (Optional/Future)
- [ ] ICarrierService interface
- [ ] Mock carrier implementation
- [ ] (Future) FedEx, UPS, USPS integrations

#### 5.6 Event Publishing
- [ ] ShipmentCreatedEvent
- [ ] ShipmentShippedEvent
- [ ] ShipmentDeliveredEvent

#### 5.7 API Endpoints
```
# Shipping Methods & Rates
GET  /api/shipping/methods                    - List shipping methods
POST /api/shipping/rates                      - Calculate rates for address

# Shipments
POST /api/shipments                           - Create shipment
GET  /api/shipments/{id}                      - Get shipment details
GET  /api/shipments/order/{orderId}           - Get by order
GET  /api/shipments/{id}/tracking             - Get tracking events
POST /api/shipments/{id}/cancel               - Cancel shipment
```

**Deliverables:**
- Shipping rates calculation
- Shipment creation and tracking
- Event publishing

---

### Phase 6: Order Service - Saga Integration (Week 6-7)

#### 6.1 Checkout Saga
- [ ] CheckoutSagaOrchestrator
- [ ] Saga state persistence
- [ ] Step execution with compensation

#### 6.2 Event Consumers
- [ ] PaymentSucceededConsumer → Continue saga
- [ ] PaymentFailedConsumer → Compensate saga
- [ ] ShipmentCreatedConsumer → Update order

#### 6.3 Saga Steps
1. Create Order (pending_payment)
2. Reserve Inventory (optional)
3. Process Payment → Call Payment Service
4. Create Shipment → Call Shipping Service
5. Confirm Order

#### 6.4 Compensation Logic
- [ ] Refund payment on failure
- [ ] Release inventory on failure
- [ ] Cancel shipment on failure
- [ ] Update order to cancelled

#### 6.5 API Updates
```
POST /api/checkout    - Initiate checkout saga (orchestrates full flow)
GET  /api/checkout/{orderId}/status - Get saga status
```

**Deliverables:**
- Complete checkout saga
- Event-driven order updates
- Compensation handling

---

### Phase 7: Frontend Integration (Week 8-10)

#### 7.1 Services & Models
- [ ] OrderService (Angular)
- [ ] PaymentService (Angular)
- [ ] ShippingService (Angular)
- [ ] TypeScript models

#### 7.2 Stripe.js Setup
- [ ] Install @stripe/stripe-js
- [ ] PaymentElement component
- [ ] Card input handling

#### 7.3 Checkout Flow
- [ ] Shipping method selection
- [ ] Payment method selection (saved cards)
- [ ] New card entry with Stripe Elements
- [ ] 3D Secure modal handling
- [ ] Order confirmation page

#### 7.4 Order Management UI
- [ ] Order history list
- [ ] Order detail page
- [ ] Order tracking
- [ ] Cancel order

#### 7.5 Payment Methods UI
- [ ] Saved cards list
- [ ] Add new card
- [ ] Remove card
- [ ] Set default card

**Deliverables:**
- Complete checkout flow
- Order history
- Payment method management

---

### Phase 8: Testing & Hardening (Week 10-12)

#### 8.1 Unit Tests
- [ ] Order service commands/queries
- [ ] Payment service commands/queries
- [ ] Shipping service commands/queries
- [ ] Saga orchestrator

#### 8.2 Integration Tests
- [ ] API endpoint tests
- [ ] Database integration
- [ ] Service-to-service calls
- [ ] Event publishing/consuming

#### 8.3 E2E Tests
- [ ] Complete checkout flow
- [ ] Payment failure scenarios
- [ ] 3D Secure flow
- [ ] Refund flow

#### 8.4 Stripe Testing
- [ ] Test cards for all scenarios
- [ ] Webhook testing with Stripe CLI
- [ ] Mock payment gateway for unit tests

#### 8.5 Error Handling
- [ ] Network timeout handling
- [ ] Retry policies (Polly)
- [ ] Circuit breaker pattern
- [ ] Dead letter queues

#### 8.6 Security
- [ ] Webhook signature verification
- [ ] Rate limiting
- [ ] Input validation
- [ ] Audit logging

#### 8.7 Monitoring
- [ ] Health checks
- [ ] Payment success/failure metrics
- [ ] Saga completion metrics
- [ ] Alerting setup

**Deliverables:**
- Test coverage > 80%
- Production-ready error handling
- Monitoring and alerting

---

## Testing Strategy

### Stripe Test Mode

Stripe provides a complete test environment. **No real money is ever charged.**

#### Configuration

```json
// appsettings.Development.json
{
  "Stripe": {
    "PublishableKey": "pk_test_...",
    "SecretKey": "sk_test_...",
    "WebhookSecret": "whsec_..."
  }
}
```

### Test Card Numbers

| Scenario | Card Number | CVC | Expiry |
|----------|-------------|-----|--------|
| **Success** | 4242 4242 4242 4242 | Any 3 digits | Any future date |
| **Requires Auth (3DS)** | 4000 0025 0000 3155 | Any 3 digits | Any future date |
| **Declined** | 4000 0000 0000 0002 | Any 3 digits | Any future date |
| **Insufficient Funds** | 4000 0000 0000 9995 | Any 3 digits | Any future date |
| **Expired Card** | 4000 0000 0000 0069 | Any 3 digits | Any future date |
| **Processing Error** | 4000 0000 0000 0119 | Any 3 digits | Any future date |
| **Incorrect CVC** | 4000 0000 0000 0127 | Any 3 digits | Any future date |

### Testing Scenarios

#### 1. Successful Payment

```typescript
// Frontend test
it('should complete payment successfully', async () => {
  // Use test card
  await cardElement.update({
    value: { number: '4242424242424242', exp: '12/25', cvc: '123' }
  });

  await submitPayment();

  expect(orderStatus).toBe('confirmed');
});
```

#### 2. 3D Secure Authentication

```typescript
it('should handle 3D Secure', async () => {
  // Use 3DS test card
  await cardElement.update({
    value: { number: '4000002500003155', exp: '12/25', cvc: '123' }
  });

  await submitPayment();

  // Should show authentication modal
  expect(stripe.confirmCardPayment).toHaveBeenCalled();
});
```

#### 3. Payment Failure

```typescript
it('should handle declined card', async () => {
  // Use declined test card
  await cardElement.update({
    value: { number: '4000000000000002', exp: '12/25', cvc: '123' }
  });

  await submitPayment();

  expect(errorMessage).toContain('declined');
  expect(orderStatus).toBe('payment_failed');
});
```

### Webhook Testing

#### Local Development with Stripe CLI

```bash
# Install Stripe CLI
# Windows (scoop)
scoop install stripe

# Login
stripe login

# Forward webhooks to local server
stripe listen --forward-to localhost:5020/api/webhooks/stripe

# In another terminal, trigger test events
stripe trigger payment_intent.succeeded
stripe trigger payment_intent.payment_failed
stripe trigger charge.refunded
```

#### Integration Test

```csharp
[Fact]
public async Task Webhook_PaymentSucceeded_UpdatesOrderStatus()
{
    // Arrange
    var order = await CreateTestOrder();
    var payment = await CreateTestPayment(order.Id);

    var webhookPayload = CreateWebhookPayload("payment_intent.succeeded", new
    {
        id = payment.StripePaymentIntentId,
        metadata = new { order_id = order.Id.ToString() }
    });

    // Act
    var response = await _client.PostAsync("/api/webhooks/stripe",
        new StringContent(webhookPayload));

    // Assert
    response.EnsureSuccessStatusCode();

    var updatedOrder = await _orderRepository.GetByIdAsync(order.Id);
    Assert.Equal(OrderStatus.Confirmed, updatedOrder.Status);
}
```

### Test Environment Checklist

| Item | Status |
|------|--------|
| Stripe test API keys configured | [ ] |
| Webhook endpoint accessible | [ ] |
| Stripe CLI installed | [ ] |
| Test database setup | [ ] |
| Mock payment gateway for unit tests | [ ] |

### Mock Payment Gateway for Unit Tests

```csharp
public class MockPaymentGateway : IPaymentGateway
{
    private readonly bool _shouldSucceed;
    private readonly bool _requires3DS;

    public MockPaymentGateway(bool shouldSucceed = true, bool requires3DS = false)
    {
        _shouldSucceed = shouldSucceed;
        _requires3DS = requires3DS;
    }

    public Task<CreatePaymentResult> CreatePaymentIntentAsync(CreatePaymentRequest request)
    {
        if (!_shouldSucceed)
        {
            return Task.FromResult(new CreatePaymentResult(
                Success: false,
                PaymentIntentId: null,
                ClientSecret: null,
                Status: "failed",
                RequiresAction: false,
                ErrorCode: "card_declined",
                ErrorMessage: "Your card was declined"
            ));
        }

        if (_requires3DS)
        {
            return Task.FromResult(new CreatePaymentResult(
                Success: false,
                PaymentIntentId: $"pi_test_{Guid.NewGuid():N}",
                ClientSecret: $"pi_test_secret_{Guid.NewGuid():N}",
                Status: "requires_action",
                RequiresAction: true,
                ErrorCode: null,
                ErrorMessage: null
            ));
        }

        return Task.FromResult(new CreatePaymentResult(
            Success: true,
            PaymentIntentId: $"pi_test_{Guid.NewGuid():N}",
            ClientSecret: null,
            Status: "succeeded",
            RequiresAction: false,
            ErrorCode: null,
            ErrorMessage: null
        ));
    }
}
```

---

## API Endpoints

### Orders

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/orders` | Create new order | Required |
| GET | `/api/orders` | List user orders | Required |
| GET | `/api/orders/{id}` | Get order by ID | Required |
| GET | `/api/orders/number/{orderNumber}` | Get by order number | Required |
| POST | `/api/orders/{id}/cancel` | Cancel order | Required |
| PATCH | `/api/orders/{id}/status` | Update status | Admin |

### Payments

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/payments/create-intent` | Create payment intent | Required |
| POST | `/api/payments/confirm` | Confirm payment | Required |
| GET | `/api/payments/{orderId}` | Get payment details | Required |

### Payment Methods

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/payment-methods` | Save payment method | Required |
| GET | `/api/payment-methods` | List saved methods | Required |
| DELETE | `/api/payment-methods/{id}` | Remove method | Required |
| PUT | `/api/payment-methods/{id}/default` | Set as default | Required |

### Refunds

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/orders/{id}/refund` | Request refund | Required |
| GET | `/api/orders/{id}/refunds` | Get refund history | Required |

### Webhooks

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/webhooks/stripe` | Stripe webhook | Signature |

---

## Event Flows

### Order Creation Flow

```
1. User submits checkout
   │
2. Frontend creates PaymentMethod via Stripe.js
   │ (Card details never touch our server)
   │
3. Frontend sends to backend:
   │ - Cart ID
   │ - Shipping address
   │ - Payment method ID (pm_xxx)
   │
4. Backend (in transaction):
   │ ├── Validate cart items
   │ ├── Check inventory
   │ ├── Create Order (status: pending_payment)
   │ ├── Create OrderItems
   │ ├── Create Payment record (status: pending)
   │ └── Call Stripe CreatePaymentIntent
   │
5. Stripe processes payment
   │
6. If requires 3DS:
   │ ├── Return client_secret to frontend
   │ ├── Frontend shows 3DS modal
   │ └── User completes authentication
   │
7. Stripe sends webhook: payment_intent.succeeded
   │
8. Backend (in transaction):
   │ ├── Verify webhook signature
   │ ├── Update Payment (status: succeeded)
   │ ├── Update Order (status: confirmed)
   │ ├── Log transaction
   │ └── Clear cart
   │
9. Send confirmation email
```

### Refund Flow

```
1. User/Admin requests refund
   │
2. Backend validates:
   │ ├── Order exists and belongs to user
   │ ├── Payment was successful
   │ ├── Refund amount <= payment amount
   │ └── Order not already refunded
   │
3. Backend (in transaction):
   │ ├── Create Refund record (status: pending)
   │ └── Call Stripe CreateRefund
   │
4. Stripe processes refund
   │
5. Stripe sends webhook: charge.refunded
   │
6. Backend (in transaction):
   │ ├── Update Refund (status: succeeded)
   │ ├── Update Payment (status: refunded/partially_refunded)
   │ ├── Update Order (status: refunded if full refund)
   │ └── Log transaction
   │
7. Send refund confirmation email
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "OrderDb": "Host=localhost;Database=gearify_orders;Username=postgres;Password=postgres"
  },
  "Stripe": {
    "PublishableKey": "pk_test_xxx",
    "SecretKey": "sk_test_xxx",
    "WebhookSecret": "whsec_xxx"
  },
  "Services": {
    "CartService": "http://localhost:5002",
    "CatalogService": "http://localhost:5001",
    "NotificationService": "http://localhost:5010"
  }
}
```

### Docker Compose Addition

```yaml
services:
  order-db:
    image: postgres:15
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: gearify_orders
    ports:
      - "5433:5432"
    volumes:
      - order-db-data:/var/lib/postgresql/data

  order-svc:
    build: ./gearify-order-svc
    ports:
      - "5020:80"
    environment:
      - ConnectionStrings__OrderDb=Host=order-db;Database=gearify_orders;Username=postgres;Password=postgres
      - Stripe__SecretKey=${STRIPE_SECRET_KEY}
      - Stripe__WebhookSecret=${STRIPE_WEBHOOK_SECRET}
    depends_on:
      - order-db

volumes:
  order-db-data:
```

---

## Security Considerations

1. **Never log full card numbers** - Only last 4 digits
2. **Webhook signature verification** - Always verify Stripe signatures
3. **Idempotency keys** - Prevent duplicate charges
4. **Rate limiting** - Protect payment endpoints
5. **PCI compliance** - Use Stripe.js, never handle raw card data
6. **Audit logging** - Log all payment events
7. **HTTPS only** - All payment communication over TLS

---

## Next Steps

1. Review this design document
2. Set up Stripe test account
3. Create `gearify-order-svc` project
4. Begin Phase 1 implementation

Would you like me to proceed with any specific phase?
