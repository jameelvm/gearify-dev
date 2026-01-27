# Task 4: AsyncAPI Event Documentation

**Priority:** Medium
**Effort:** Low
**Risk if skipped:** No single source of truth for event contracts, new developers struggle to understand inter-service communication

---

## Problem

As the number of events, topics, and queues grows, it becomes increasingly difficult to answer basic questions:

- What events does the Payment Service publish?
- What is the schema of `OrderCreatedEvent`?
- Which services consume `PaymentCompletedEvent`?
- What changed between v1 and v2 of `ShippingStatusUpdatedEvent`?

Currently, this information is spread across:
- C# event classes (the actual contracts)
- `SNS_SQS_MESSAGING_PATTERN.md` (communication tables)
- `SNS_SQS_FANOUT.md` (flow diagrams)
- `init-aws.sh` (queue/topic/subscription definitions)
- Individual service `Startup.cs` files (consumer registrations)

No single, machine-readable, standardized document describes the entire event-driven architecture.

## Solution: AsyncAPI Specification

[AsyncAPI](https://www.asyncapi.com/) is the OpenAPI equivalent for event-driven architectures. It provides a standard way to document:

- **Channels** (SNS topics / SQS queues)
- **Messages** (event schemas with JSON Schema)
- **Operations** (publish / subscribe)
- **Servers** (LocalStack, AWS)

### Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  AsyncAPI Specification (asyncapi.yaml)                                   │
│                                                                          │
│  Single YAML file describing:                                            │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │ Servers:    LocalStack (dev), AWS (prod)                           │  │
│  │ Channels:   gearify-order-events, gearify-payment-events, ...     │  │
│  │ Messages:   OrderCreatedEvent, PaymentCompletedEvent, ...         │  │
│  │ Operations: Order Service publishes OrderCreated to order-events  │  │
│  │             Payment Service subscribes to order-events            │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  Generates:                                                              │
│  • Interactive HTML documentation (AsyncAPI Studio)                      │
│  • Channel/message diagrams                                              │
│  • Code stubs (optional)                                                 │
│  • Schema validation                                                     │
└──────────────────────────────────────────────────────────────────────────┘
```

### Implementation Plan

#### Step 1: Install AsyncAPI CLI

```bash
npm install -g @asyncapi/cli
# or
brew install asyncapi
```

#### Step 2: Create AsyncAPI Specification

```yaml
# documentation/asyncapi.yaml
asyncapi: '3.0.0'
info:
  title: Gearify Event-Driven Architecture
  version: '1.0.0'
  description: |
    Event-driven communication between Gearify microservices using
    AWS SNS (publish) and SQS (subscribe) with fan-out pattern.
  contact:
    name: Gearify Team

servers:
  localstack:
    host: localhost:4566
    protocol: sns
    description: LocalStack for local development
  production:
    host: sns.us-east-1.amazonaws.com
    protocol: sns
    description: AWS SNS production

channels:
  # ─── ORDER EVENTS ─────────────────────────────────────────
  gearify-order-events:
    address: arn:aws:sns:us-east-1:000000000000:gearify-order-events
    description: Order lifecycle events
    messages:
      OrderCreated:
        $ref: '#/components/messages/OrderCreatedEvent'
      OrderConfirmed:
        $ref: '#/components/messages/OrderConfirmedEvent'
      OrderCancelled:
        $ref: '#/components/messages/OrderCancelledEvent'
      OrderCompleted:
        $ref: '#/components/messages/OrderCompletedEvent'

  # ─── PAYMENT EVENTS ───────────────────────────────────────
  gearify-payment-events:
    address: arn:aws:sns:us-east-1:000000000000:gearify-payment-events
    description: Payment processing events
    messages:
      PaymentCompleted:
        $ref: '#/components/messages/PaymentCompletedEvent'
      PaymentFailed:
        $ref: '#/components/messages/PaymentFailedEvent'

  # ─── SHIPPING EVENTS ──────────────────────────────────────
  gearify-shipping-events:
    address: arn:aws:sns:us-east-1:000000000000:gearify-shipping-events
    description: Shipping and delivery events
    messages:
      ShipmentCreated:
        $ref: '#/components/messages/ShippingCreatedEvent'
      ShipmentStatusUpdated:
        $ref: '#/components/messages/ShippingStatusUpdatedEvent'
      ShipmentDelivered:
        $ref: '#/components/messages/ShippingDeliveredEvent'

  # ─── CATALOG EVENTS ───────────────────────────────────────
  gearify-catalog-events:
    address: arn:aws:sns:us-east-1:000000000000:gearify-catalog-events
    description: Product catalog changes
    messages:
      ProductCreated:
        $ref: '#/components/messages/ProductCreatedEvent'
      ProductUpdated:
        $ref: '#/components/messages/ProductUpdatedEvent'
      ProductDeleted:
        $ref: '#/components/messages/ProductDeletedEvent'

  # ─── MEDIA EVENTS ─────────────────────────────────────────
  gearify-media-uploaded:
    address: arn:aws:sns:us-east-1:000000000000:gearify-media-uploaded
    description: Media upload events
    messages:
      MediaUploaded:
        $ref: '#/components/messages/MediaUploadedEvent'

  gearify-image-processing-completed:
    address: arn:aws:sns:us-east-1:000000000000:gearify-image-processing-completed
    description: Image processing completion events
    messages:
      ImageProcessingCompleted:
        $ref: '#/components/messages/ImageProcessingCompletedEvent'

  # ─── SQS QUEUES (Subscriptions) ───────────────────────────
  gearify-order-created-queue:
    address: http://sqs.us-east-1.amazonaws.com/000000000000/gearify-order-created-queue
    description: |
      Subscribes to: gearify-order-events
      Filter: eventType = "OrderCreated"
      Consumer: Payment Service

  order-payment-events-queue:
    address: http://sqs.us-east-1.amazonaws.com/000000000000/order-payment-events-queue
    description: |
      Subscribes to: gearify-payment-events
      Filter: eventType IN ["PaymentCompleted", "PaymentFailed"]
      Consumer: Order Service

  notification-payment-events-queue:
    address: http://sqs.us-east-1.amazonaws.com/000000000000/notification-payment-events-queue
    description: |
      Subscribes to: gearify-payment-events
      Filter: eventType = "PaymentFailed"
      Consumer: Notification Service

operations:
  # ─── ORDER SERVICE ─────────────────────────────────────────
  orderServicePublish:
    action: send
    channel:
      $ref: '#/channels/gearify-order-events'
    summary: Order Service publishes order lifecycle events
    description: |
      Published by CreateOrderCommandHandler, ConfirmOrderCommandHandler,
      CancelOrderCommandHandler after successful database operations.

  orderServiceConsumePayment:
    action: receive
    channel:
      $ref: '#/channels/order-payment-events-queue'
    summary: Order Service consumes payment results
    description: |
      PaymentEventHandler processes PaymentCompleted (confirm order)
      and PaymentFailed (cancel order) events.

  # ─── PAYMENT SERVICE ──────────────────────────────────────
  paymentServiceConsumeOrder:
    action: receive
    channel:
      $ref: '#/channels/gearify-order-created-queue'
    summary: Payment Service consumes new orders
    description: |
      OrderCreatedEventHandler triggers payment processing via Stripe
      when a new order is created.

  paymentServicePublish:
    action: send
    channel:
      $ref: '#/channels/gearify-payment-events'
    summary: Payment Service publishes payment results
    description: |
      ProcessOrderPaymentCommandHandler publishes PaymentCompleted
      or PaymentFailed after Stripe processing.

  # ─── NOTIFICATION SERVICE ─────────────────────────────────
  notificationServiceConsumePayment:
    action: receive
    channel:
      $ref: '#/channels/notification-payment-events-queue'
    summary: Notification Service consumes payment failures
    description: |
      PaymentFailedEventHandler sends failure notification emails.

  # ─── CATALOG SERVICE ──────────────────────────────────────
  catalogServicePublish:
    action: send
    channel:
      $ref: '#/channels/gearify-catalog-events'
    summary: Catalog Service publishes product changes
    description: |
      Product CRUD operations trigger events for search indexing
      and downstream consumers.

  # ─── MEDIA SERVICE ────────────────────────────────────────
  mediaServicePublish:
    action: send
    channel:
      $ref: '#/channels/gearify-image-processing-completed'
    summary: Media Service publishes processing results
    description: |
      After image variants are generated, publishes completion event
      so Catalog Service can update product thumbnails.

components:
  messages:
    OrderCreatedEvent:
      name: OrderCreatedEvent
      title: Order Created
      summary: Fired when a new order is successfully persisted
      contentType: application/json
      payload:
        $ref: '#/components/schemas/OrderCreatedPayload'

    OrderConfirmedEvent:
      name: OrderConfirmedEvent
      title: Order Confirmed
      summary: Fired when payment is confirmed and order is active
      contentType: application/json
      payload:
        $ref: '#/components/schemas/OrderConfirmedPayload'

    OrderCancelledEvent:
      name: OrderCancelledEvent
      title: Order Cancelled
      summary: Fired when order is cancelled (payment failed or user cancelled)
      contentType: application/json
      payload:
        $ref: '#/components/schemas/OrderCancelledPayload'

    OrderCompletedEvent:
      name: OrderCompletedEvent
      title: Order Completed
      summary: Fired when order is fully delivered
      contentType: application/json
      payload:
        $ref: '#/components/schemas/OrderCompletedPayload'

    PaymentCompletedEvent:
      name: PaymentCompletedEvent
      title: Payment Completed
      summary: Fired when Stripe payment intent succeeds
      contentType: application/json
      payload:
        $ref: '#/components/schemas/PaymentCompletedPayload'

    PaymentFailedEvent:
      name: PaymentFailedEvent
      title: Payment Failed
      summary: Fired when Stripe payment intent fails
      contentType: application/json
      payload:
        $ref: '#/components/schemas/PaymentFailedPayload'

    ShippingCreatedEvent:
      name: ShippingCreatedEvent
      title: Shipment Created
      summary: Fired when a shipment record is created
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ShippingCreatedPayload'

    ShippingStatusUpdatedEvent:
      name: ShippingStatusUpdatedEvent
      title: Shipping Status Updated
      summary: Fired when shipment status changes
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ShippingStatusUpdatedPayload'

    ShippingDeliveredEvent:
      name: ShippingDeliveredEvent
      title: Shipment Delivered
      summary: Fired when shipment is marked as delivered
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ShippingDeliveredPayload'

    ProductCreatedEvent:
      name: ProductCreatedEvent
      title: Product Created
      summary: Fired when a new product is added to catalog
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ProductEventPayload'

    ProductUpdatedEvent:
      name: ProductUpdatedEvent
      title: Product Updated
      summary: Fired when product details are modified
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ProductEventPayload'

    ProductDeletedEvent:
      name: ProductDeletedEvent
      title: Product Deleted
      summary: Fired when a product is removed from catalog
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ProductDeletedPayload'

    MediaUploadedEvent:
      name: MediaUploadedEvent
      title: Media Uploaded
      summary: Fired when product images are uploaded to S3
      contentType: application/json
      payload:
        $ref: '#/components/schemas/MediaUploadedPayload'

    ImageProcessingCompletedEvent:
      name: ImageProcessingCompletedEvent
      title: Image Processing Completed
      summary: Fired when image variants (thumbnails, resized) are generated
      contentType: application/json
      payload:
        $ref: '#/components/schemas/ImageProcessingCompletedPayload'

  schemas:
    # ─── ENVELOPE ────────────────────────────────────────────
    EventEnvelope:
      type: object
      description: Standard wrapper for all domain events
      required: [eventId, eventType, tenantId, timestamp, payload]
      properties:
        eventId:
          type: string
          format: uuid
          description: Unique identifier for this event instance
        eventType:
          type: string
          description: "Type discriminator (e.g., OrderCreated, PaymentCompleted)"
        tenantId:
          type: string
          description: Tenant identifier for multi-tenancy
        correlationId:
          type: string
          format: uuid
          description: Correlation ID for distributed tracing (future)
        timestamp:
          type: string
          format: date-time
        payload:
          type: object
          description: The actual domain event data

    # ─── ORDER SCHEMAS ───────────────────────────────────────
    OrderCreatedPayload:
      type: object
      required: [orderId, userId, totalAmount, currency]
      properties:
        orderId:
          type: string
          format: uuid
        userId:
          type: string
        totalAmount:
          type: number
          format: decimal
        currency:
          type: string
          example: "USD"
        items:
          type: array
          items:
            $ref: '#/components/schemas/OrderItemPayload'

    OrderConfirmedPayload:
      type: object
      required: [orderId, paymentId]
      properties:
        orderId:
          type: string
          format: uuid
        paymentId:
          type: string

    OrderCancelledPayload:
      type: object
      required: [orderId, reason]
      properties:
        orderId:
          type: string
          format: uuid
        reason:
          type: string

    OrderCompletedPayload:
      type: object
      required: [orderId]
      properties:
        orderId:
          type: string
          format: uuid

    OrderItemPayload:
      type: object
      properties:
        productId:
          type: string
        productName:
          type: string
        quantity:
          type: integer
        unitPrice:
          type: number
          format: decimal

    # ─── PAYMENT SCHEMAS ─────────────────────────────────────
    PaymentCompletedPayload:
      type: object
      required: [orderId, paymentId, amount]
      properties:
        orderId:
          type: string
          format: uuid
        paymentId:
          type: string
        paymentIntentId:
          type: string
          description: Stripe PaymentIntent ID
        amount:
          type: number
          format: decimal
        currency:
          type: string

    PaymentFailedPayload:
      type: object
      required: [orderId, reason]
      properties:
        orderId:
          type: string
          format: uuid
        paymentId:
          type: string
        reason:
          type: string
        errorCode:
          type: string

    # ─── SHIPPING SCHEMAS ────────────────────────────────────
    ShippingCreatedPayload:
      type: object
      required: [shipmentId, orderId]
      properties:
        shipmentId:
          type: string
          format: uuid
        orderId:
          type: string
          format: uuid
        carrier:
          type: string
        trackingNumber:
          type: string

    ShippingStatusUpdatedPayload:
      type: object
      required: [shipmentId, status]
      properties:
        shipmentId:
          type: string
          format: uuid
        orderId:
          type: string
          format: uuid
        status:
          type: string
          enum: [Created, PickedUp, InTransit, OutForDelivery, Delivered]
        location:
          type: string

    ShippingDeliveredPayload:
      type: object
      required: [shipmentId, orderId]
      properties:
        shipmentId:
          type: string
          format: uuid
        orderId:
          type: string
          format: uuid
        deliveredAt:
          type: string
          format: date-time

    # ─── PRODUCT SCHEMAS ─────────────────────────────────────
    ProductEventPayload:
      type: object
      required: [productId, tenantId]
      properties:
        productId:
          type: string
        tenantId:
          type: string
        name:
          type: string
        brand:
          type: string
        price:
          type: number
          format: decimal
        category:
          type: string

    ProductDeletedPayload:
      type: object
      required: [productId, tenantId]
      properties:
        productId:
          type: string
        tenantId:
          type: string

    # ─── MEDIA SCHEMAS ───────────────────────────────────────
    MediaUploadedPayload:
      type: object
      required: [productId, tenantId, s3Keys]
      properties:
        productId:
          type: string
        tenantId:
          type: string
        s3Keys:
          type: array
          items:
            type: string

    ImageProcessingCompletedPayload:
      type: object
      required: [productId, tenantId, thumbnailUrl]
      properties:
        productId:
          type: string
        tenantId:
          type: string
        thumbnailUrl:
          type: string
        variants:
          type: array
          items:
            type: object
            properties:
              size:
                type: string
              url:
                type: string
```

#### Step 3: Generate Documentation

```bash
# Generate interactive HTML docs
asyncapi generate fromTemplate documentation/asyncapi.yaml @asyncapi/html-template -o documentation/asyncapi-docs

# Validate the spec
asyncapi validate documentation/asyncapi.yaml

# Open in AsyncAPI Studio (browser-based editor)
asyncapi start studio
```

#### Step 4: Optional - EventCatalog Integration

[EventCatalog](https://www.eventcatalog.dev/) provides a richer documentation experience:

```bash
npx @eventcatalog/create-eventcatalog@latest my-catalog
```

EventCatalog supports:
- Service dependency visualization
- Event versioning history
- OpenAPI + AsyncAPI integration
- Mermaid diagrams
- Markdown documentation per event

### Files to Create

| Action | File |
|--------|------|
| Create | `documentation/asyncapi.yaml` |
| Create | `documentation/asyncapi-docs/` (generated HTML) |
| Optional | `.github/workflows/asyncapi-validate.yml` (CI validation) |

### Acceptance Criteria

- [ ] `asyncapi.yaml` documents all SNS topics, SQS queues, and event schemas
- [ ] Schemas match the actual C# event contracts
- [ ] `asyncapi validate` passes without errors
- [ ] Generated HTML documentation is accessible to the team
- [ ] New events/channels are added to the spec as part of the development workflow
- [ ] Optional: CI pipeline validates the spec on every PR
