# SNS/SQS Event Communication Flows

This document shows how each microservice communicates via SNS/SQS events, with detailed box diagrams tracing the full journey from HTTP request to final event handler.

---

## Flow 1: Image Upload & Processing

**Services:** Catalog Service, Media Service

**Summary:** Catalog Service uploads images to Media Service via HTTP. Media Service stores the original, then asynchronously generates image variants (thumbnail, medium, large) via SNS/SQS self-subscription. Once processing is complete, it publishes back to Catalog Service to update the product thumbnail.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                              CATALOG SERVICE                                     │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ ProductsController                                                         │  │
│  │                                                                            │  │
│  │ POST /api/products/{id}/images                                             │  │
│  │ Method: UploadProductImages()                                              │  │
│  │                                                                            │  │
│  │ Accepts multipart/form-data with images                                    │  │
│  │ Sends UploadProductImagesCommand via MediatR                               │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ UploadProductImagesCommandHandler                                          │  │
│  │                                                                            │  │
│  │ 1. Validates product exists                                                │  │
│  │ 2. Validates image files                                                   │  │
│  │ 3. Calls MediaServiceClient.UploadProductImageAsync()  ──── HTTP POST ───────────┐
│  │    (synchronous call to Media Service)                                     │  │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │  │
│                                                                                  │  │
└──────────────────────────────────────────────────────────────────────────────────┘  │
                                                                                     │
                  ┌──────────────────────────────────────────────────────────────────┘
                  │  HTTP POST /api/media/upload
                  ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                               MEDIA SERVICE                                      │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ MediaController                                                            │  │
│  │                                                                            │  │
│  │ POST /api/media/upload                                                     │  │
│  │ Method: UploadImage()                                                      │  │
│  │                                                                            │  │
│  │ Receives image file + metadata (tenantId, entityType, entityId)            │  │
│  │ Sends UploadImageCommand via MediatR                                       │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ UploadImageCommandHandler                                                  │  │
│  │                                                                            │  │
│  │ 1. Validates image (size, content type, integrity)                         │  │
│  │ 2. Gets image dimensions                                                   │  │
│  │ 3. Uploads ORIGINAL image to S3                                            │  │
│  │ 4. Creates MediaMetadata in DynamoDB (status: Processing)                  │  │
│  │ 5. Publishes MediaUploadedEvent via ISnsEventPublisher                     │  │
│  │ 6. Returns media metadata (HTTP response to Catalog Service)               │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 │ Publishes                                      │
│                                 ▼                                                │
│              ┌──────────────────────────────────────────┐                        │
│              │ SnsEventPublisher (extends Base)          │                        │
│              │                                          │                        │
│              │ Event: MediaUploadedEvent                 │                        │
│              │ Payload:                                  │                        │
│              │   - MediaId                               │                        │
│              │   - TenantId                              │                        │
│              │   - EntityType (e.g. "Product")           │                        │
│              │   - EntityId (productId)                  │                        │
│              │   - OriginalKey (S3 key)                  │                        │
│              │   - ContentType, Width, Height            │                        │
│              └──────────────────┬───────────────────────┘                        │
│                                 │                                                │
└─────────────────────────────────┼────────────────────────────────────────────────┘
                                  │
                                  ▼
                 ┌─────────────────────────────────────┐
                 │ SNS Topic                            │
                 │ gearify-media-upload-events          │
                 │                                     │
                 │ ARN: arn:aws:sns:us-east-1:          │
                 │   000000000000:gearify-media-        │
                 │   upload-events                      │
                 └─────────────────┬───────────────────┘
                                   │
                                   │ Subscription (self-subscribe)
                                   ▼
                 ┌─────────────────────────────────────┐
                 │ SQS Queue                            │
                 │ gearify-image-processing-queue       │
                 │                                     │
                 │ URL: http://localstack:4566/         │
                 │   000000000000/gearify-image-        │
                 │   processing-queue                   │
                 └─────────────────┬───────────────────┘
                                   │
┌──────────────────────────────────┼───────────────────────────────────────────────┐
│                               MEDIA SERVICE (Consumer)                           │
│                                  │                                               │
│                                  ▼                                               │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ EventQueueProcessor<ImageProcessingEventMessage>  (BackgroundService)      │  │
│  │                                                                            │  │
│  │ Polls SQS → SqsEventQueue<ImageProcessingEventMessage>                    │  │
│  │ Filter: ["MediaUploadedEvent"]                                            │  │
│  │ Delegates to IEventHandler<ImageProcessingEventMessage>                   │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ ImageProcessingEventHandler                                                │  │
│  │                                                                            │  │
│  │ 1. Downloads original image from S3 using OriginalKey                      │  │
│  │ 2. Generates image variants:                                               │  │
│  │    - Thumbnail (small)                                                     │  │
│  │    - Medium                                                                │  │
│  │    - Large                                                                 │  │
│  │ 3. Uploads all variants to S3                                              │  │
│  │ 4. Updates MediaMetadata in DynamoDB (status: Ready)                       │  │
│  │ 5. Publishes ImageProcessingCompletedEvent via ISnsEventPublisher          │  │
│  │                                                                            │  │
│  │ Returns: true (delete from queue)                                          │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 │ Publishes                                      │
│                                 ▼                                                │
│              ┌──────────────────────────────────────────┐                        │
│              │ SnsEventPublisher (extends Base)          │                        │
│              │                                          │                        │
│              │ Event: ImageProcessingCompletedEvent      │                        │
│              │ Payload:                                  │                        │
│              │   - MediaId                               │                        │
│              │   - TenantId                              │                        │
│              │   - EntityType, EntityId                  │                        │
│              │   - ThumbnailUrl                          │                        │
│              │   - MediumUrl                             │                        │
│              │   - LargeUrl                              │                        │
│              │   - OriginalUrl                           │                        │
│              │   - DisplayOrder, AltText                 │                        │
│              └──────────────────┬───────────────────────┘                        │
│                                 │                                                │
└─────────────────────────────────┼────────────────────────────────────────────────┘
                                  │
                                  ▼
                 ┌─────────────────────────────────────┐
                 │ SNS Topic                            │
                 │ gearify-image-processing-completed   │
                 │                                     │
                 │ ARN: arn:aws:sns:us-east-1:          │
                 │   000000000000:gearify-image-        │
                 │   processing-completed               │
                 └─────────────────┬───────────────────┘
                                   │
                                   │ Subscription
                                   ▼
                 ┌─────────────────────────────────────┐
                 │ SQS Queue                            │
                 │ gearify-product-thumbnail-update-    │
                 │ queue                                │
                 │                                     │
                 │ URL: http://localstack:4566/         │
                 │   000000000000/gearify-product-      │
                 │   thumbnail-update-queue             │
                 └─────────────────┬───────────────────┘
                                   │
┌──────────────────────────────────┼───────────────────────────────────────────────┐
│                            CATALOG SERVICE (Consumer)                            │
│                                  │                                               │
│                                  ▼                                               │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ EventQueueProcessor<ImageProcessingCompletedEventMessage> (Background)     │  │
│  │                                                                            │  │
│  │ Polls SQS → SqsEventQueue<ImageProcessingCompletedEventMessage>           │  │
│  │ Filter: ["ImageProcessingCompletedEvent"]                                 │  │
│  │ Delegates to IEventHandler<ImageProcessingCompletedEventMessage>          │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ ImageProcessingCompletedEventHandler                                       │  │
│  │                                                                            │  │
│  │ 1. Checks EntityType is "Product" (skips others)                           │  │
│  │ 2. Retrieves Product from DynamoDB by EntityId                             │  │
│  │ 3. Updates Product.ThumbnailUrl if:                                        │  │
│  │    - DisplayOrder == 0 (first image), OR                                   │  │
│  │    - ThumbnailUrl is null                                                  │  │
│  │ 4. Saves Product back to DynamoDB                                          │  │
│  │                                                                            │  │
│  │ Returns: true (delete from queue)                                          │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## Flow 2: Order Creation & Payment Processing

**Services:** Order Service, Payment Service, Notification Service, Auth Service

**Summary:** When a customer places an order, Order Service creates the order and publishes an event. Payment Service picks it up, processes payment via Stripe, and publishes the result. Order Service updates the order status. If payment fails, Notification Service sends a failure email to the customer.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                              ORDER SERVICE                                       │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ OrdersController                                                           │  │
│  │                                                                            │  │
│  │ POST /api/orders                                                           │  │
│  │ Method: CreateOrder()                                                      │  │
│  │                                                                            │  │
│  │ Receives: userId, items, addresses, amounts                                │  │
│  │ Sends CreateOrderCommand via MediatR                                       │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ CreateOrderCommandHandler                                                  │  │
│  │                                                                            │  │
│  │ 1. Creates Order entity (status: Pending)                                  │  │
│  │ 2. Persists to PostgreSQL database                                         │  │
│  │ 3. Publishes OrderCreatedEvent via ISnsEventPublisher                      │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 │ Publishes                                      │
│                                 ▼                                                │
│              ┌──────────────────────────────────────────┐                        │
│              │ SnsEventPublisher (extends Base)          │                        │
│              │                                          │                        │
│              │ Event: OrderCreatedEvent                  │                        │
│              │ Payload:                                  │                        │
│              │   - OrderId, OrderNumber                  │                        │
│              │   - TenantId, UserId                      │                        │
│              │   - Items (list)                          │                        │
│              │   - TotalAmount, Currency                 │                        │
│              │   - ShippingAddress, BillingAddress       │                        │
│              └──────────────────┬───────────────────────┘                        │
│                                 │                                                │
└─────────────────────────────────┼────────────────────────────────────────────────┘
                                  │
                                  ▼
                 ┌─────────────────────────────────────┐
                 │ SNS Topic                            │
                 │ gearify-order-events                 │
                 │                                     │
                 │ ARN: arn:aws:sns:us-east-1:          │
                 │   000000000000:gearify-order-events  │
                 └─────────────────┬───────────────────┘
                                   │
                                   │ Subscription
                                   ▼
                 ┌─────────────────────────────────────┐
                 │ SQS Queue                            │
                 │ gearify-order-created-queue          │
                 │                                     │
                 │ URL: http://localstack:4566/         │
                 │   000000000000/gearify-order-        │
                 │   created-queue                      │
                 └─────────────────┬───────────────────┘
                                   │
┌──────────────────────────────────┼───────────────────────────────────────────────┐
│                            PAYMENT SERVICE                                       │
│                                  │                                               │
│                                  ▼                                               │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ EventQueueProcessor<OrderCreatedEventMessage>  (BackgroundService)         │  │
│  │                                                                            │  │
│  │ Polls SQS → SqsEventQueue<OrderCreatedEventMessage>                       │  │
│  │ Filter: ["OrderCreatedEvent"]                                             │  │
│  │ Delegates to IEventHandler<OrderCreatedEventMessage>                      │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ OrderCreatedEventHandler                                                   │  │
│  │                                                                            │  │
│  │ 1. Logs order details (OrderId, Amount, Currency)                          │  │
│  │ 2. Sends ProcessOrderPaymentCommand via MediatR                            │  │
│  │    (OrderId, OrderNumber, TenantId, UserId, Amount, Currency)              │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ ProcessOrderPaymentCommandHandler                                          │  │
│  │                                                                            │  │
│  │ 1. Creates PaymentTransaction record                                       │  │
│  │ 2. Calls Stripe payment provider                                           │  │
│  │ 3. Updates transaction status                                              │  │
│  │                                                                            │  │
│  │ ┌─────────────────────┐    ┌──────────────────────┐                        │  │
│  │ │  SUCCESS             │    │  FAILURE              │                        │  │
│  │ │                     │    │                      │                        │  │
│  │ │  Publishes:         │    │  Publishes:          │                        │  │
│  │ │  PaymentCompleted   │    │  PaymentFailed       │                        │  │
│  │ │  Event              │    │  Event               │                        │  │
│  │ │                     │    │                      │                        │  │
│  │ │  Payload:           │    │  Payload:            │                        │  │
│  │ │  - TransactionId    │    │  - TransactionId     │                        │  │
│  │ │  - OrderId          │    │  - OrderId           │                        │  │
│  │ │  - OrderNumber      │    │  - OrderNumber       │                        │  │
│  │ │  - TenantId         │    │  - TenantId          │                        │  │
│  │ │  - UserId           │    │  - UserId            │                        │  │
│  │ │  - Amount, Currency │    │  - Amount, Currency  │                        │  │
│  │ │  - ProviderTxnId    │    │  - ErrorCode         │                        │  │
│  │ │                     │    │  - ErrorMessage      │                        │  │
│  │ └─────────┬───────────┘    └──────────┬───────────┘                        │  │
│  │           │                           │                                    │  │
│  └───────────┼───────────────────────────┼────────────────────────────────────┘  │
│              │                           │                                       │
└──────────────┼───────────────────────────┼───────────────────────────────────────┘
               │                           │
               └─────────┬────────────────┘
                         │ Both publish to same topic
                         ▼
        ┌─────────────────────────────────────┐
        │ SNS Topic                            │
        │ gearify-payment-events               │
        │                                     │
        │ ARN: arn:aws:sns:us-east-1:          │
        │   000000000000:gearify-payment-      │
        │   events                             │
        └─────────────────┬───────────────────┘
                          │
            ┌─────────────┴──────────────────────┐
            │                                    │
            │ FAN-OUT (2 subscribers)             │
            ▼                                    ▼
┌──────────────────────────┐       ┌──────────────────────────────┐
│ SQS Queue                │       │ SQS Queue                    │
│ order-payment-events-    │       │ notification-payment-events- │
│ queue                    │       │ queue                        │
│                          │       │                              │
│ Events:                  │       │ Events:                      │
│ PaymentCompletedEvent    │       │ PaymentFailedEvent ONLY      │
│ PaymentFailedEvent       │       │                              │
└────────────┬─────────────┘       └──────────────┬───────────────┘
             │                                    │
┌────────────┼────────────────────┐  ┌────────────┼───────────────────────────────┐
│ ORDER SERVICE (Consumer)        │  │ NOTIFICATION SERVICE (Consumer)             │
│            │                    │  │            │                                │
│            ▼                    │  │            ▼                                │
│ ┌────────────────────────────┐  │  │ ┌──────────────────────────────────────┐   │
│ │ EventQueueProcessor        │  │  │ │ EventQueueProcessor                  │   │
│ │ <PaymentEventMessage>      │  │  │ │ <PaymentFailedEventMessage>          │   │
│ │                            │  │  │ │                                      │   │
│ │ Filter: Completed, Failed  │  │  │ │ Filter: PaymentFailedEvent           │   │
│ └──────────────┬─────────────┘  │  │ └──────────────────┬───────────────────┘   │
│                │                │  │                    │                        │
│                ▼                │  │                    ▼                        │
│ ┌────────────────────────────┐  │  │ ┌──────────────────────────────────────┐   │
│ │ PaymentEventHandler        │  │  │ │ PaymentFailedEventHandler            │   │
│ │                            │  │  │ │                                      │   │
│ │ Routes by EventType:       │  │  │ │ 1. Fetches user from Auth Service   │   │
│ │                            │  │  │ │    GET /api/users/{userId}           │   │
│ │ ┌────────────────────────┐ │  │  │ │    (includes X-Tenant-Id header)    │   │
│ │ │ PaymentCompletedEvent  │ │  │  │ │                                      │   │
│ │ │                        │ │  │  │ │ 2. Renders PaymentFailed email       │   │
│ │ │ Sends ConfirmOrder     │ │  │  │ │    template with:                    │   │
│ │ │ Command                │ │  │  │ │    - FirstName                       │   │
│ │ │                        │ │  │  │ │    - OrderNumber                     │   │
│ │ │ → Order status: Paid   │ │  │  │ │    - Amount, Currency                │   │
│ │ └────────────────────────┘ │  │  │ │    - ErrorMessage                    │   │
│ │                            │  │  │ │    - RetryLink                       │   │
│ │ ┌────────────────────────┐ │  │  │ │                                      │   │
│ │ │ PaymentFailedEvent     │ │  │  │ │ 3. Sends email to customer           │   │
│ │ │                        │ │  │  │ └──────────────────────────────────────┘   │
│ │ │ Sends UpdateOrder      │ │  │  │                    │                        │
│ │ │ StatusCommand          │ │  │  │                    │ HTTP GET                │
│ │ │                        │ │  │  │                    ▼                        │
│ │ │ → Order status:        │ │  │  │ ┌──────────────────────────────────────┐   │
│ │ │   PaymentFailed        │ │  │  │ │ AuthServiceClient                    │   │
│ │ └────────────────────────┘ │  │  │ │                                      │   │
│ └────────────────────────────┘  │  │ │ GET auth-svc/api/users/{userId}      │   │
│                                 │  │ │ Returns: email, firstName, lastName  │   │
└─────────────────────────────────┘  │ └──────────────────────────────────────┘   │
                                     │                                            │
                                     └────────────────────────────────────────────┘
```

---

## Flow 3: Product Catalog to Search Index

**Services:** Catalog Service, Search Service

**Summary:** When products are created, updated, or deleted in the Catalog Service, domain events are published. Search Service consumes these events and synchronizes the OpenSearch index for full-text product search.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                              CATALOG SERVICE                                     │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ ProductsController                                                         │  │
│  │                                                                            │  │
│  │ POST   /api/products        → CreateProductCommand                         │  │
│  │ PUT    /api/products/{id}   → UpdateProductCommand                         │  │
│  │ DELETE /api/products/{id}   → DeleteProductCommand                         │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│            ┌────────────────────┼────────────────────┐                           │
│            │                    │                    │                           │
│            ▼                    ▼                    ▼                           │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐                 │
│  │ CreateProduct     │ │ UpdateProduct     │ │ DeleteProduct     │                 │
│  │ CommandHandler    │ │ CommandHandler    │ │ CommandHandler    │                 │
│  │                  │ │                  │ │                  │                 │
│  │ 1. Validate      │ │ 1. Validate      │ │ 1. Find product  │                 │
│  │ 2. Save to DB    │ │ 2. Update in DB  │ │ 2. Delete from   │                 │
│  │ 3. Publish       │ │ 3. Publish       │ │    DB             │                 │
│  │    ProductCreated│ │    ProductUpdated│ │ 3. Publish       │                 │
│  │    Event         │ │    Event         │ │    ProductDeleted│                 │
│  └────────┬─────────┘ └────────┬─────────┘ │    Event         │                 │
│           │                    │            └────────┬─────────┘                 │
│           │                    │                     │                           │
│           └────────────────────┼─────────────────────┘                           │
│                                │                                                │
│                                │ All events publish via                          │
│                                ▼                                                │
│              ┌──────────────────────────────────────────┐                        │
│              │ SnsEventPublisher (extends Base)          │                        │
│              │                                          │                        │
│              │ All 3 event types route to same topic:   │                        │
│              │ → catalog-events-topic                   │                        │
│              │                                          │                        │
│              │ Payloads include full product data:      │                        │
│              │   ProductId, TenantId, Sku, Name,        │                        │
│              │   Description, Brand, Category, Price,   │                        │
│              │   ThumbnailUrl, Tags, IsActive, etc.     │                        │
│              └──────────────────┬───────────────────────┘                        │
│                                 │                                                │
└─────────────────────────────────┼────────────────────────────────────────────────┘
                                  │
                                  ▼
                 ┌─────────────────────────────────────┐
                 │ SNS Topic                            │
                 │ catalog-events-topic                 │
                 │                                     │
                 │ ARN: arn:aws:sns:us-east-1:          │
                 │   000000000000:catalog-events-topic  │
                 └─────────────────┬───────────────────┘
                                   │
                                   │ Subscription
                                   ▼
                 ┌─────────────────────────────────────┐
                 │ SQS Queue                            │
                 │ search-catalog-events-queue          │
                 │                                     │
                 │ URL: http://localstack:4566/         │
                 │   000000000000/search-catalog-       │
                 │   events-queue                       │
                 └─────────────────┬───────────────────┘
                                   │
┌──────────────────────────────────┼───────────────────────────────────────────────┐
│                            SEARCH SERVICE                                        │
│                                  │                                               │
│                                  ▼                                               │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ CatalogEventMessageHandler  (BackgroundService)                            │  │
│  │                                                                            │  │
│  │ Polls SQS for CatalogEvent messages                                        │  │
│  │ Unwraps SNS envelope → parses EventEnvelope                                │  │
│  │ Delegates to ICatalogEventHandler                                          │  │
│  └──────────────────────────────┬─────────────────────────────────────────────┘  │
│                                 │                                                │
│                                 ▼                                                │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │ CatalogEventHandler                                                        │  │
│  │                                                                            │  │
│  │ Routes by EventType:                                                       │  │
│  │                                                                            │  │
│  │ ┌──────────────────────────────────────────────────────────────────────┐   │  │
│  │ │ "ProductCreated"                                                     │   │  │
│  │ │                                                                     │   │  │
│  │ │ 1. Maps ProductPayload → ProductSearchDocument                      │   │  │
│  │ │ 2. ProductIndexService.IndexProductAsync()                          │   │  │
│  │ │ 3. Creates document in OpenSearch (tenant-specific index)           │   │  │
│  │ └──────────────────────────────────────────────────────────────────────┘   │  │
│  │                                                                            │  │
│  │ ┌──────────────────────────────────────────────────────────────────────┐   │  │
│  │ │ "ProductUpdated"                                                     │   │  │
│  │ │                                                                     │   │  │
│  │ │ 1. Maps ProductPayload → ProductSearchDocument                      │   │  │
│  │ │ 2. ProductIndexService.UpdateProductAsync()                         │   │  │
│  │ │ 3. Upserts document in OpenSearch                                   │   │  │
│  │ └──────────────────────────────────────────────────────────────────────┘   │  │
│  │                                                                            │  │
│  │ ┌──────────────────────────────────────────────────────────────────────┐   │  │
│  │ │ "ProductDeleted"                                                     │   │  │
│  │ │                                                                     │   │  │
│  │ │ 1. Extracts ProductId from payload                                  │   │  │
│  │ │ 2. ProductIndexService.DeleteProductAsync()                         │   │  │
│  │ │ 3. Removes document from OpenSearch                                 │   │  │
│  │ └──────────────────────────────────────────────────────────────────────┘   │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## Flow 4: Complete System Overview

**All service-to-service event communication in one view:**

```
┌───────────────┐  HTTP   ┌───────────────┐
│  CATALOG      │────────>│  MEDIA        │
│  SERVICE      │         │  SERVICE      │
│               │         │               │
│  Publishes:   │         │  Publishes:   │
│  ProductCrea- │         │  MediaUploaded│───► gearify-media-upload-events (SNS)
│  tedEvent     │         │  Event        │         │
│  ProductUpda- │         │               │         ▼ (self-subscribe)
│  tedEvent     │         │  ImageProcess-│    gearify-image-processing-queue (SQS)
│  ProductDele- │         │  ingCompleted │         │
│  tedEvent     │         │  Event        │───► gearify-image-processing-completed (SNS)
│               │         │               │         │
│               │         └───────────────┘         │
│               │◄──────────────────────────────────┘
│               │    gearify-product-thumbnail-update-queue (SQS)
│               │
│               │───► catalog-events-topic (SNS)
│               │         │
└───────────────┘         │
                          ▼
                   search-catalog-events-queue (SQS)
                          │
                          ▼
                   ┌───────────────┐
                   │  SEARCH       │
                   │  SERVICE      │
                   │               │
                   │  Consumes:    │
                   │  ProductCrea- │
                   │  tedEvent     │
                   │  ProductUpda- │
                   │  tedEvent     │
                   │  ProductDele- │
                   │  tedEvent     │
                   │               │
                   │  → OpenSearch │
                   │    Index      │
                   └───────────────┘


┌───────────────┐                              ┌───────────────┐
│  ORDER        │                              │  AUTH          │
│  SERVICE      │                              │  SERVICE      │
│               │                              │               │
│  Publishes:   │───► gearify-order-events     │  Provides:    │
│  OrderCreated │    (SNS)                     │  GET /api/    │
│  Event        │         │                    │  users/{id}   │
│               │         ▼                    │               │
│               │    gearify-order-created-    └──────▲────────┘
│               │    queue (SQS)                      │
│               │         │                           │ HTTP GET
│               │         ▼                           │ (fetch user email)
│               │    ┌───────────────┐                │
│               │    │  PAYMENT      │                │
│               │    │  SERVICE      │                │
│               │    │               │                │
│               │    │  Consumes:    │                │
│               │    │  OrderCreated │                │
│               │    │  Event        │                │
│               │    │               │                │
│               │    │  Publishes:   │                │
│               │    │  PaymentComp- │───► gearify-payment-events (SNS)
│               │    │  letedEvent   │         │
│               │    │  PaymentFail- │         ├──────────────────────┐
│               │    │  edEvent      │         │                      │
│               │    └───────────────┘         │ FAN-OUT              │
│               │                              ▼                      ▼
│               │◄──── order-payment-     notification-payment-
│               │      events-queue       events-queue (SQS)
│               │      (SQS)                   │
│               │                              ▼
│  Consumes:    │                         ┌───────────────┐
│  PaymentComp- │                         │ NOTIFICATION  │
│  letedEvent   │                         │ SERVICE       │──────┘
│  PaymentFail- │                         │               │
│  edEvent      │                         │ Consumes:     │
│               │                         │ PaymentFailed │
└───────────────┘                         │ Event         │
                                          │               │
                                          │ → Send email  │
                                          └───────────────┘
```

---

## Quick Reference: Communication Protocols

| From | To | Protocol | Purpose |
|------|----|----------|---------|
| Catalog Service | Media Service | **HTTP POST** | Upload image files (synchronous) |
| Media Service | Media Service | **SNS → SQS** | Trigger async image variant generation |
| Media Service | Catalog Service | **SNS → SQS** | Notify image processing completed |
| Catalog Service | Search Service | **SNS → SQS** | Sync product data to search index |
| Order Service | Payment Service | **SNS → SQS** | Trigger payment processing |
| Payment Service | Order Service | **SNS → SQS** | Notify payment result (completed/failed) |
| Payment Service | Notification Service | **SNS → SQS** | Notify payment failure (fan-out) |
| Notification Service | Auth Service | **HTTP GET** | Fetch user email for notifications |
