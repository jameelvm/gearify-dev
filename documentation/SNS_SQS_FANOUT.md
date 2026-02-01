# SNS/SQS Event Communication Flows

This document shows how each microservice communicates via SNS/SQS events, with detailed box diagrams tracing the full journey from HTTP request to final event handler.

---

## Organization Chart: SNS Topics & SQS Queues

```
                            GEARIFY EVENT ARCHITECTURE
                            ==========================

SNS Topics                          SQS Queues                          Consuming Services
----------                          ----------                          ------------------

gearify-order-events
    |
    +---> gearify-order-created-queue -----------------------> Payment Service
    |         Filter: [OrderCreatedEvent]                      (ProcessOrderPaymentCommand)
    |
    +---> gearify-order-refund-queue ------------------------> Payment Service
              Filter: [OrderCancelledEvent]                    (RefundPaymentCommand)


gearify-payment-events
    |
    +---> gearify-order-payment-events-queue ----------------> Order Service
    |         Filter: [PaymentCompletedEvent,                  (PaymentEventHandler)
    |                  PaymentFailedEvent,
    |                  RefundCompletedEvent]
    |
    +---> gearify-notification-payment-events-queue ---------> Notification Service
    |         Filter: [PaymentCompletedEvent,                  (PaymentEventHandler)
    |                  PaymentFailedEvent]
    |
    +---> gearify-notification-refund-queue -----------------> Notification Service
              Filter: [RefundCompletedEvent,                   (RefundEventHandler)
                       RefundFailedEvent]


gearify-shipping-events
    |
    +---> gearify-shipping-created-queue --------------------> Order Service
    |         Filter: [ShipmentCreated]                        (ShippingEventHandler)
    |
    +---> gearify-shipping-status-queue ---------------------> Order Service
              Filter: [ShipmentStatusUpdated,                  (ShippingEventHandler)
                       ShipmentDelivered]


gearify-media-upload-events
    |
    +---> gearify-image-processing-queue --------------------> Media Service
              Filter: [MediaUploadedEvent]                     (ImageProcessingEventHandler)


gearify-image-processing-completed
    |
    +---> gearify-product-thumbnail-update-queue ------------> Catalog Service
              Filter: [ImageProcessingCompletedEvent]          (ImageProcessingCompletedEventHandler)


catalog-events-topic
    |
    +---> gearify-search-catalog-events-queue ---------------> Search Service
              Filter: [ProductCreated,                         (CatalogEventHandler)
                       ProductUpdated,
                       ProductDeleted]


gearify-checkout-events
    |
    +---> gearify-checkout-initiated-queue ------------------> Order Service
              Filter: [CheckoutInitiatedEvent]                 (CheckoutEventHandler)
```

---

## Dead Letter Queues (DLQs)

```
DLQ Structure
=============

gearify-checkout-events-dlq
    |
    +---> Failed messages from: gearify-checkout-initiated-queue

gearify-order-events-dlq
    |
    +---> Failed messages from: gearify-order-created-queue
    |                           gearify-order-refund-queue

gearify-payment-events-dlq
    |
    +---> Failed messages from: gearify-order-payment-events-queue
    |                           gearify-notification-payment-events-queue
    |                           gearify-notification-refund-queue
    |                           gearify-payment-failed-queue

gearify-shipping-events-dlq
    |
    +---> Failed messages from: gearify-shipping-created-queue
                                gearify-shipping-status-queue
```

---

## Flow 1: Image Upload & Processing

**Services:** Catalog Service, Media Service

**Summary:** Catalog Service uploads images to Media Service via HTTP. Media Service stores the original, then asynchronously generates image variants (thumbnail, medium, large) via SNS/SQS self-subscription. Once processing is complete, it publishes back to Catalog Service to update the product thumbnail.

```
+----------------------------------------------------------------------------------+
|                              CATALOG SERVICE                                     |
|                                                                                  |
|  +--------------------------------------------------------------------------+   |
|  | ProductsController                                                        |   |
|  |                                                                           |   |
|  | POST /api/products/{id}/images                                            |   |
|  | Method: UploadProductImages()                                             |   |
|  |                                                                           |   |
|  | Accepts multipart/form-data with images                                   |   |
|  | Sends UploadProductImagesCommand via MediatR                              |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | UploadProductImagesCommandHandler                                         |   |
|  |                                                                           |   |
|  | 1. Validates product exists                                               |   |
|  | 2. Validates image files                                                  |   |
|  | 3. Calls MediaServiceClient.UploadProductImageAsync()  ---- HTTP POST --------+
|  |    (synchronous call to Media Service)                                    |   |
|  +--------------------------------------------------------------------------+   |
|                                                                                  |
+----------------------------------------------------------------------------------+
                                                                                   |
                  +----------------------------------------------------------------+
                  |  HTTP POST /api/media/upload
                  v
+----------------------------------------------------------------------------------+
|                               MEDIA SERVICE                                      |
|                                                                                  |
|  +--------------------------------------------------------------------------+   |
|  | MediaController                                                           |   |
|  |                                                                           |   |
|  | POST /api/media/upload                                                    |   |
|  | Method: UploadImage()                                                     |   |
|  |                                                                           |   |
|  | Receives image file + metadata (tenantId, entityType, entityId)           |   |
|  | Sends UploadImageCommand via MediatR                                      |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | UploadImageCommandHandler                                                 |   |
|  |                                                                           |   |
|  | 1. Validates image (size, content type, integrity)                        |   |
|  | 2. Gets image dimensions                                                  |   |
|  | 3. Uploads ORIGINAL image to S3                                           |   |
|  | 4. Creates MediaMetadata in DynamoDB (status: Processing)                 |   |
|  | 5. Publishes MediaUploadedEvent via ISnsEventPublisher                    |   |
|  | 6. Returns media metadata (HTTP response to Catalog Service)              |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 | Publishes                                      |
|                                 v                                                |
|              +----------------------------------------------+                    |
|              | SnsEventPublisher                            |                    |
|              |                                              |                    |
|              | Event: MediaUploadedEvent                    |                    |
|              | Topic: gearify-media-upload-events           |                    |
|              +---------------------+------------------------+                    |
|                                    |                                             |
+------------------------------------|---------------------------------------------+
                                     |
                                     v
                 +-------------------------------------+
                 | SNS Topic                          |
                 | gearify-media-upload-events        |
                 +------------------+-----------------+
                                    |
                                    | Subscription (self-subscribe)
                                    v
                 +-------------------------------------+
                 | SQS Queue                          |
                 | gearify-image-processing-queue     |
                 +------------------+-----------------+
                                    |
+------------------------------------|---------------------------------------------+
|                               MEDIA SERVICE (Consumer)                           |
|                                   |                                              |
|                                   v                                              |
|  +--------------------------------------------------------------------------+   |
|  | EventQueueProcessor<ImageProcessingEventMessage>  (BackgroundService)     |   |
|  |                                                                           |   |
|  | Polls SQS -> SqsEventQueue<ImageProcessingEventMessage>                   |   |
|  | Filter: ["MediaUploadedEvent"]                                            |   |
|  | Delegates to IEventHandler<ImageProcessingEventMessage>                   |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | ImageProcessingEventHandler                                               |   |
|  |                                                                           |   |
|  | 1. Downloads original image from S3 using OriginalKey                     |   |
|  | 2. Generates image variants (Thumbnail, Medium, Large)                    |   |
|  | 3. Uploads all variants to S3                                             |   |
|  | 4. Updates MediaMetadata in DynamoDB (status: Ready)                      |   |
|  | 5. Publishes ImageProcessingCompletedEvent via ISnsEventPublisher         |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 | Publishes                                      |
|                                 v                                                |
|              +----------------------------------------------+                    |
|              | SnsEventPublisher                            |                    |
|              |                                              |                    |
|              | Event: ImageProcessingCompletedEvent         |                    |
|              | Topic: gearify-image-processing-completed    |                    |
|              +---------------------+------------------------+                    |
|                                    |                                             |
+------------------------------------|---------------------------------------------+
                                     |
                                     v
                 +-------------------------------------+
                 | SNS Topic                          |
                 | gearify-image-processing-completed |
                 +------------------+-----------------+
                                    |
                                    | Subscription
                                    v
                 +-------------------------------------+
                 | SQS Queue                          |
                 | gearify-product-thumbnail-update-  |
                 | queue                              |
                 +------------------+-----------------+
                                    |
+------------------------------------|---------------------------------------------+
|                            CATALOG SERVICE (Consumer)                            |
|                                   |                                              |
|                                   v                                              |
|  +--------------------------------------------------------------------------+   |
|  | EventQueueProcessor<ImageProcessingCompletedEventMessage> (Background)    |   |
|  |                                                                           |   |
|  | Polls SQS -> SqsEventQueue<ImageProcessingCompletedEventMessage>          |   |
|  | Filter: ["ImageProcessingCompletedEvent"]                                 |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | ImageProcessingCompletedEventHandler                                      |   |
|  |                                                                           |   |
|  | 1. Checks EntityType is "Product" (skips others)                          |   |
|  | 2. Retrieves Product from DynamoDB by EntityId                            |   |
|  | 3. Updates Product.ThumbnailUrl if DisplayOrder == 0 or ThumbnailUrl null |   |
|  | 4. Saves Product back to DynamoDB                                         |   |
|  +--------------------------------------------------------------------------+   |
|                                                                                  |
+----------------------------------------------------------------------------------+
```

---

## Flow 2: Order Creation & Payment Processing

**Services:** Order Service, Payment Service, Notification Service, Auth Service

**Summary:** When a customer places an order, Order Service creates the order and publishes an event. Payment Service picks it up, processes payment via Stripe, and publishes the result. Order Service updates the order status. Notification Service sends emails for payment success/failure.

```
+----------------------------------------------------------------------------------+
|                              ORDER SERVICE                                       |
|                                                                                  |
|  +--------------------------------------------------------------------------+   |
|  | OrdersController                                                          |   |
|  |                                                                           |   |
|  | POST /api/orders                                                          |   |
|  | Method: CreateOrder()                                                     |   |
|  |                                                                           |   |
|  | Receives: userId, items, addresses, amounts                               |   |
|  | Sends CreateOrderCommand via MediatR                                      |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | CreateOrderCommandHandler                                                 |   |
|  |                                                                           |   |
|  | 1. Creates Order entity (status: Pending)                                 |   |
|  | 2. Persists to PostgreSQL database                                        |   |
|  | 3. Publishes OrderCreatedEvent via ISnsEventPublisher                     |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 | Publishes                                      |
|                                 v                                                |
|              +----------------------------------------------+                    |
|              | SnsEventPublisher                            |                    |
|              |                                              |                    |
|              | Event: OrderCreatedEvent                     |                    |
|              | Topic: gearify-order-events                  |                    |
|              +---------------------+------------------------+                    |
|                                    |                                             |
+------------------------------------|---------------------------------------------+
                                     |
                                     v
                 +-------------------------------------+
                 | SNS Topic                          |
                 | gearify-order-events               |
                 +------------------+-----------------+
                                    |
                                    | Subscription
                                    v
                 +-------------------------------------+
                 | SQS Queue                          |
                 | gearify-order-created-queue        |
                 +------------------+-----------------+
                                    |
+------------------------------------|---------------------------------------------+
|                            PAYMENT SERVICE                                       |
|                                   |                                              |
|                                   v                                              |
|  +--------------------------------------------------------------------------+   |
|  | EventQueueProcessor<OrderCreatedEventMessage>  (BackgroundService)        |   |
|  |                                                                           |   |
|  | Polls SQS -> SqsEventQueue<OrderCreatedEventMessage>                      |   |
|  | Filter: ["OrderCreatedEvent"]                                             |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | OrderCreatedEventHandler                                                  |   |
|  |                                                                           |   |
|  | 1. Logs order details (OrderId, Amount, Currency)                         |   |
|  | 2. Sends ProcessOrderPaymentCommand via MediatR                           |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | ProcessOrderPaymentCommandHandler                                         |   |
|  |                                                                           |   |
|  | 1. Creates PaymentTransaction record                                      |   |
|  | 2. Calls Stripe payment provider                                          |   |
|  | 3. Updates transaction status                                             |   |
|  |                                                                           |   |
|  | +---------------------+    +----------------------+                       |   |
|  | |  SUCCESS            |    |  FAILURE             |                       |   |
|  | |                     |    |                      |                       |   |
|  | |  Publishes:         |    |  Publishes:          |                       |   |
|  | |  PaymentCompleted   |    |  PaymentFailed       |                       |   |
|  | |  Event              |    |  Event               |                       |   |
|  | +----------+----------+    +----------+-----------+                       |   |
|  |            |                          |                                   |   |
|  +------------+--------------------------+-----------------------------------+   |
|               |                          |                                       |
+---------------+--------------------------+---------------------------------------+
                |                          |
                +------------+-------------+
                             | Both publish to same topic
                             v
        +-------------------------------------+
        | SNS Topic                          |
        | gearify-payment-events             |
        +------------------+-----------------+
                           |
             +-------------+---------------+
             |                             |
             | FAN-OUT (2 subscribers)     |
             v                             v
+--------------------------+   +------------------------------+
| SQS Queue                |   | SQS Queue                    |
| gearify-order-payment-   |   | gearify-notification-        |
| events-queue             |   | payment-events-queue         |
|                          |   |                              |
| Events:                  |   | Events:                      |
| PaymentCompletedEvent    |   | PaymentCompletedEvent        |
| PaymentFailedEvent       |   | PaymentFailedEvent           |
| RefundCompletedEvent     |   |                              |
+------------+-------------+   +--------------+---------------+
             |                                |
+------------+----------------+  +------------+-------------------------------+
| ORDER SERVICE (Consumer)    |  | NOTIFICATION SERVICE (Consumer)            |
|            |                |  |            |                               |
|            v                |  |            v                               |
| +------------------------+  |  | +--------------------------------------+  |
| | PaymentEventHandler    |  |  | | PaymentEventHandler                  |  |
| |                        |  |  | |                                      |  |
| | Routes by EventType:   |  |  | | 1. Fetches user from Auth Service    |  |
| |                        |  |  | |    GET /api/users/{userId}           |  |
| | PaymentCompletedEvent  |  |  | |                                      |  |
| | -> ConfirmOrderCommand |  |  | | 2. Renders email template            |  |
| | -> Order status: Paid  |  |  | |    (PaymentConfirmation or           |  |
| |                        |  |  | |     PaymentFailed)                   |  |
| | PaymentFailedEvent     |  |  | |                                      |  |
| | -> Order status:       |  |  | | 3. Sends email to customer           |  |
| |    PaymentFailed       |  |  | +--------------------------------------+  |
| +------------------------+  |  |                                           |
|                             |  +-------------------------------------------+
+-----------------------------+
```

---

## Flow 3: Cancel Order & Refund Processing

**Services:** Order Service, Payment Service, Notification Service, Auth Service

**Summary:** When an order is cancelled, Order Service publishes OrderCancelledEvent. If the order was paid, Payment Service processes a refund via Stripe and publishes RefundCompletedEvent. Order Service updates the order to Refunded status. Notification Service sends cancellation/refund emails.

```
+----------------------------------------------------------------------------------+
|                              ORDER SERVICE                                       |
|                                                                                  |
|  +--------------------------------------------------------------------------+   |
|  | OrdersController                                                          |   |
|  |                                                                           |   |
|  | POST /api/orders/{id}/cancel                                              |   |
|  | Method: CancelOrder()                                                     |   |
|  |                                                                           |   |
|  | Receives: reason, cancelledBy                                             |   |
|  | Sends CancelOrderCommand via MediatR                                      |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | CancelOrderCommandHandler                                                 |   |
|  |                                                                           |   |
|  | 1. Validates order exists and is cancellable                              |   |
|  | 2. Checks if order is in PaymentProcessing (race condition)               |   |
|  |                                                                           |   |
|  | +---------------------------+  +-------------------------------+          |   |
|  | | NORMAL CANCELLATION       |  | DEFERRED CANCELLATION         |          |   |
|  | | (Order not processing)    |  | (PaymentProcessing status)    |          |   |
|  | |                           |  |                               |          |   |
|  | | 1. Set status: Cancelled  |  | 1. Set SagaState: Compensating|          |   |
|  | | 2. Publish OrderCancelled |  | 2. Store cancellation reason  |          |   |
|  | |    Event                  |  | 3. Return "Pending" response  |          |   |
|  | |    - PaymentId (if paid)  |  |                               |          |   |
|  | |    - PaidAmount           |  | (Cancellation happens when    |          |   |
|  | |    - Reason               |  |  payment completes/fails)     |          |   |
|  | +-------------+-------------+  +-------------------------------+          |   |
|  |               |                                                           |   |
|  +---------------+-----------------------------------------------------------+   |
|                  |                                                               |
|                  | Publishes (if not deferred)                                   |
|                  v                                                               |
|              +----------------------------------------------+                    |
|              | SnsEventPublisher                            |                    |
|              |                                              |                    |
|              | Event: OrderCancelledEvent                   |                    |
|              | Topic: gearify-order-events                  |                    |
|              | Payload:                                     |                    |
|              |   - OrderId, OrderNumber, TenantId, UserId   |                    |
|              |   - Reason, CancelledBy                      |                    |
|              |   - PaymentId (if was paid)                  |                    |
|              |   - PaidAmount, Currency                     |                    |
|              +---------------------+------------------------+                    |
|                                    |                                             |
+------------------------------------|---------------------------------------------+
                                     |
                                     v
                 +-------------------------------------+
                 | SNS Topic                          |
                 | gearify-order-events               |
                 +------------------+-----------------+
                                    |
                                    | Subscription (Filter: OrderCancelledEvent)
                                    v
                 +-------------------------------------+
                 | SQS Queue                          |
                 | gearify-order-refund-queue         |
                 +------------------+-----------------+
                                    |
+------------------------------------|---------------------------------------------+
|                            PAYMENT SERVICE                                       |
|                                   |                                              |
|                                   v                                              |
|  +--------------------------------------------------------------------------+   |
|  | EventQueueProcessor<OrderCancelledEventMessage>  (BackgroundService)      |   |
|  |                                                                           |   |
|  | Polls SQS -> SqsEventQueue<OrderCancelledEventMessage>                    |   |
|  | Filter: ["OrderCancelledEvent"]                                           |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | OrderCancelledEventHandler                                                |   |
|  |                                                                           |   |
|  | 1. Checks if PaymentId is present (order was paid)                        |   |
|  | 2. If no payment -> return true (no refund needed)                        |   |
|  | 3. If payment exists -> send RefundPaymentCommand                         |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | RefundPaymentCommandHandler                                               |   |
|  |                                                                           |   |
|  | 1. Finds original PaymentTransaction                                      |   |
|  | 2. Calls Stripe refund API                                                |   |
|  | 3. Creates Refund record in database                                      |   |
|  |                                                                           |   |
|  | +---------------------+    +----------------------+                       |   |
|  | |  SUCCESS            |    |  FAILURE             |                       |   |
|  | |                     |    |                      |                       |   |
|  | |  Publishes:         |    |  Publishes:          |                       |   |
|  | |  RefundCompleted    |    |  RefundFailed        |                       |   |
|  | |  Event              |    |  Event               |                       |   |
|  | +----------+----------+    +----------+-----------+                       |   |
|  |            |                          |                                   |   |
|  +------------+--------------------------+-----------------------------------+   |
|               |                          |                                       |
+---------------+--------------------------+---------------------------------------+
                |                          |
                +------------+-------------+
                             | Both publish to same topic
                             v
        +-------------------------------------+
        | SNS Topic                          |
        | gearify-payment-events             |
        +------------------+-----------------+
                           |
             +-------------+------------------+
             |                                |
             | FAN-OUT (2 subscribers)        |
             v                                v
+--------------------------+   +--------------------------------+
| SQS Queue                |   | SQS Queue                      |
| gearify-order-payment-   |   | gearify-notification-refund-   |
| events-queue             |   | queue                          |
|                          |   |                                |
| Events:                  |   | Events:                        |
| RefundCompletedEvent     |   | RefundCompletedEvent           |
|                          |   | RefundFailedEvent              |
+------------+-------------+   +--------------+-----------------+
             |                                |
+------------+----------------+  +------------+-------------------------------+
| ORDER SERVICE (Consumer)    |  | NOTIFICATION SERVICE (Consumer)            |
|            |                |  |            |                               |
|            v                |  |            v                               |
| +------------------------+  |  | +--------------------------------------+  |
| | PaymentEventHandler    |  |  | | RefundEventHandler                   |  |
| |                        |  |  | |                                      |  |
| | RefundCompletedEvent:  |  |  | | RefundCompletedEvent:                |  |
| | -> Order status:       |  |  | | -> Send OrderCancelledRefunded email |  |
| |    Refunded            |  |  | |                                      |  |
| | -> SagaState:          |  |  | | RefundFailedEvent:                   |  |
| |    Completed           |  |  | | -> Send RefundFailed email           |  |
| |                        |  |  | | -> Alert admin                       |  |
| +------------------------+  |  | +--------------------------------------+  |
|                             |  |                                           |
+-----------------------------+  +-------------------------------------------+
```

---

## Flow 4: Product Catalog to Search Index

**Services:** Catalog Service, Search Service

**Summary:** When products are created, updated, or deleted in the Catalog Service, domain events are published. Search Service consumes these events and synchronizes the OpenSearch index for full-text product search.

```
+----------------------------------------------------------------------------------+
|                              CATALOG SERVICE                                     |
|                                                                                  |
|  +--------------------------------------------------------------------------+   |
|  | ProductsController                                                        |   |
|  |                                                                           |   |
|  | POST   /api/products        -> CreateProductCommand                       |   |
|  | PUT    /api/products/{id}   -> UpdateProductCommand                       |   |
|  | DELETE /api/products/{id}   -> DeleteProductCommand                       |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|            +--------------------+--------------------+                           |
|            |                    |                    |                           |
|            v                    v                    v                           |
|  +------------------+ +------------------+ +------------------+                  |
|  | CreateProduct    | | UpdateProduct    | | DeleteProduct    |                  |
|  | CommandHandler   | | CommandHandler   | | CommandHandler   |                  |
|  |                  | |                  | |                  |                  |
|  | 1. Validate      | | 1. Validate      | | 1. Find product  |                  |
|  | 2. Save to DB    | | 2. Update in DB  | | 2. Delete from   |                  |
|  | 3. Publish       | | 3. Publish       | |    DB            |                  |
|  |    ProductCreated| |    ProductUpdated| | 3. Publish       |                  |
|  |    Event         | |    Event         | |    ProductDeleted|                  |
|  +--------+---------+ +--------+---------+ |    Event         |                  |
|           |                    |           +--------+---------+                  |
|           |                    |                    |                            |
|           +--------------------+--------------------+                            |
|                                |                                                 |
|                                | All events publish via                          |
|                                v                                                 |
|              +----------------------------------------------+                    |
|              | SnsEventPublisher                            |                    |
|              |                                              |                    |
|              | All 3 event types route to same topic:       |                    |
|              | -> catalog-events-topic                      |                    |
|              +---------------------+------------------------+                    |
|                                    |                                             |
+------------------------------------|---------------------------------------------+
                                     |
                                     v
                 +-------------------------------------+
                 | SNS Topic                          |
                 | catalog-events-topic               |
                 +------------------+-----------------+
                                    |
                                    | Subscription
                                    v
                 +-------------------------------------+
                 | SQS Queue                          |
                 | gearify-search-catalog-events-queue|
                 +------------------+-----------------+
                                    |
+------------------------------------|---------------------------------------------+
|                            SEARCH SERVICE                                        |
|                                   |                                              |
|                                   v                                              |
|  +--------------------------------------------------------------------------+   |
|  | CatalogEventMessageHandler  (BackgroundService)                           |   |
|  |                                                                           |   |
|  | Polls SQS for CatalogEvent messages                                       |   |
|  | Unwraps SNS envelope -> parses EventEnvelope                              |   |
|  +------------------------------+--------------------------------------------+   |
|                                 |                                                |
|                                 v                                                |
|  +--------------------------------------------------------------------------+   |
|  | CatalogEventHandler                                                       |   |
|  |                                                                           |   |
|  | Routes by EventType:                                                      |   |
|  |                                                                           |   |
|  | +----------------------------------------------------------------------+  |   |
|  | | "ProductCreated" -> ProductIndexService.IndexProductAsync()          |  |   |
|  | +----------------------------------------------------------------------+  |   |
|  |                                                                           |   |
|  | +----------------------------------------------------------------------+  |   |
|  | | "ProductUpdated" -> ProductIndexService.UpdateProductAsync()         |  |   |
|  | +----------------------------------------------------------------------+  |   |
|  |                                                                           |   |
|  | +----------------------------------------------------------------------+  |   |
|  | | "ProductDeleted" -> ProductIndexService.DeleteProductAsync()         |  |   |
|  | +----------------------------------------------------------------------+  |   |
|  +--------------------------------------------------------------------------+   |
|                                                                                  |
+----------------------------------------------------------------------------------+
```

---

## Flow 5: Complete System Overview

**All service-to-service event communication in one view:**

```
+---------------+  HTTP   +---------------+
|  CATALOG      |-------->|  MEDIA        |
|  SERVICE      |         |  SERVICE      |
|               |         |               |
|  Publishes:   |         |  Publishes:   |
|  ProductCrea- |         |  MediaUploaded|---> gearify-media-upload-events (SNS)
|  tedEvent     |         |  Event        |         |
|  ProductUpda- |         |               |         v (self-subscribe)
|  tedEvent     |         |  ImageProcess-|    gearify-image-processing-queue (SQS)
|  ProductDele- |         |  ingCompleted |         |
|  tedEvent     |         |  Event        |---> gearify-image-processing-completed (SNS)
|               |         |               |         |
|               |         +---------------+         |
|               |<----------------------------------+
|               |    gearify-product-thumbnail-update-queue (SQS)
|               |
|               |---> catalog-events-topic (SNS)
|               |         |
+---------------+         |
                          v
                   gearify-search-catalog-events-queue (SQS)
                          |
                          v
                   +---------------+
                   |  SEARCH       |
                   |  SERVICE      |
                   |               |
                   |  Consumes:    |
                   |  ProductCrea- |
                   |  tedEvent     |
                   |  ProductUpda- |
                   |  tedEvent     |
                   |  ProductDele- |
                   |  tedEvent     |
                   |               |
                   |  -> OpenSearch|
                   |     Index     |
                   +---------------+


+---------------+                              +---------------+
|  ORDER        |                              |  AUTH         |
|  SERVICE      |                              |  SERVICE      |
|               |                              |               |
|  Publishes:   |---> gearify-order-events     |  Provides:    |
|  OrderCreated |    (SNS)                     |  GET /api/    |
|  Event        |         |                    |  users/{id}   |
|  OrderCancel- |         +---+                |               |
|  ledEvent     |             |                +-------^-------+
|               |             |                        |
|               |             v                        | HTTP GET
|               |    gearify-order-created-queue       | (fetch user email)
|               |    (SQS)                             |
|               |             |                        |
|               |             v                        |
|               |    +---------------+                 |
|               |    |  PAYMENT      |                 |
|               |    |  SERVICE      |                 |
|               |    |               |                 |
|               |    |  Consumes:    |                 |
|               |    |  OrderCreated |                 |
|  Consumes:    |    |  Event        |                 |
|  PaymentComp- |    |  OrderCancel- |                 |
|  letedEvent   |    |  ledEvent     |                 |
|  PaymentFail- |    |               |                 |
|  edEvent      |    |  Publishes:   |                 |
|  RefundComp-  |    |  PaymentComp- |---> gearify-payment-events (SNS)
|  letedEvent   |    |  letedEvent   |         |
|               |    |  PaymentFail- |         +---> gearify-order-payment-events-queue
|               |    |  edEvent      |         |         (Order Service)
|               |<---+  RefundComp-  |         |
|  gearify-     |    |  letedEvent   |         +---> gearify-notification-payment-events-queue
|  order-       |    |  RefundFailed |         |         (Notification Service)
|  payment-     |    |  Event        |         |
|  events-queue |    +---------------+         +---> gearify-notification-refund-queue
|               |                                       (Notification Service)
|               |<--- gearify-order-refund-queue
|  (gearify-    |         (Payment Service)
|  order-events)|
+---------------+
                                             +---------------+
                                             | NOTIFICATION  |
                                             | SERVICE       |
                                             |               |
                                             | Consumes:     |
                                             | PaymentCompl- |
                                             | etedEvent     |
                                             | PaymentFailed |
                                             | Event         |
                                             | RefundCompl-  |
                                             | etedEvent     |
                                             | RefundFailed  |
                                             | Event         |
                                             |               |
                                             | -> Send emails|
                                             +---------------+
```

---

## Quick Reference: SNS Topics & Subscribers

| SNS Topic | SQS Queue | Consumer Service | Filter |
|-----------|-----------|------------------|--------|
| `gearify-order-events` | `gearify-order-created-queue` | Payment Service | `OrderCreatedEvent` |
| `gearify-order-events` | `gearify-order-refund-queue` | Payment Service | `OrderCancelledEvent` |
| `gearify-payment-events` | `gearify-order-payment-events-queue` | Order Service | `PaymentCompletedEvent`, `PaymentFailedEvent`, `RefundCompletedEvent` |
| `gearify-payment-events` | `gearify-notification-payment-events-queue` | Notification Service | `PaymentCompletedEvent`, `PaymentFailedEvent` |
| `gearify-payment-events` | `gearify-notification-refund-queue` | Notification Service | `RefundCompletedEvent`, `RefundFailedEvent` |
| `gearify-shipping-events` | `gearify-shipping-created-queue` | Order Service | `ShipmentCreated` |
| `gearify-shipping-events` | `gearify-shipping-status-queue` | Order Service | `ShipmentStatusUpdated`, `ShipmentDelivered` |
| `gearify-media-upload-events` | `gearify-image-processing-queue` | Media Service | `MediaUploadedEvent` |
| `gearify-image-processing-completed` | `gearify-product-thumbnail-update-queue` | Catalog Service | `ImageProcessingCompletedEvent` |
| `catalog-events-topic` | `gearify-search-catalog-events-queue` | Search Service | `ProductCreated`, `ProductUpdated`, `ProductDeleted` |
| `gearify-checkout-events` | `gearify-checkout-initiated-queue` | Order Service | `CheckoutInitiatedEvent` |

---

## Quick Reference: Communication Protocols

| From | To | Protocol | Purpose |
|------|----|----------|---------|
| Catalog Service | Media Service | **HTTP POST** | Upload image files (synchronous) |
| Media Service | Media Service | **SNS -> SQS** | Trigger async image variant generation |
| Media Service | Catalog Service | **SNS -> SQS** | Notify image processing completed |
| Catalog Service | Search Service | **SNS -> SQS** | Sync product data to search index |
| Order Service | Payment Service | **SNS -> SQS** | Trigger payment processing |
| Order Service | Payment Service | **SNS -> SQS** | Trigger refund on cancellation |
| Payment Service | Order Service | **SNS -> SQS** | Notify payment result (completed/failed/refunded) |
| Payment Service | Notification Service | **SNS -> SQS** | Notify payment/refund events (fan-out) |
| Notification Service | Auth Service | **HTTP GET** | Fetch user email for notifications |
