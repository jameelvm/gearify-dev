#!/bin/bash

# SNS Topics and Subscriptions Initialization
# Architecture: One Queue Per Event Type with SNS Filter Policies

set -e

echo "=========================================="
echo "Creating SNS topics..."
echo "=========================================="

ORDER_TOPIC_ARN=$(awslocal sns create-topic --name gearify-order-events --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-order-events"

PAYMENT_TOPIC_ARN=$(awslocal sns create-topic --name gearify-payment-events --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-payment-events"

SHIPPING_TOPIC_ARN=$(awslocal sns create-topic --name gearify-shipping-events --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-shipping-events"

MEDIA_TOPIC_ARN=$(awslocal sns create-topic --name gearify-media-upload-events --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-media-upload-events"

IMAGE_PROCESSING_COMPLETED_TOPIC_ARN=$(awslocal sns create-topic --name gearify-image-processing-completed --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-image-processing-completed"

CATALOG_EVENTS_TOPIC_ARN=$(awslocal sns create-topic --name catalog-events-topic --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: catalog-events-topic"

echo "SNS topics created successfully!"

echo ""
echo "=========================================="
echo "Creating SNS subscriptions..."
echo "=========================================="

# ==========================================
# Order Events -> Payment Service
# Pattern: One queue per event type
# ==========================================
echo "  - Creating order event subscriptions (Payment Service)..."

# OrderCreatedEvent -> gearify-order-created-queue (Payment Service)
if [ ! -z "$ORDER_TOPIC_ARN" ]; then
  ORDER_CREATED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-created-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$ORDER_CREATED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $ORDER_TOPIC_ARN --protocol sqs --notification-endpoint $ORDER_CREATED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"OrderCreatedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-order-created-queue (Filter: OrderCreatedEvent)"
  fi
fi

# OrderCancelledEvent -> gearify-order-cancelled-queue (Payment Service)
if [ ! -z "$ORDER_TOPIC_ARN" ]; then
  ORDER_CANCELLED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-cancelled-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$ORDER_CANCELLED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $ORDER_TOPIC_ARN --protocol sqs --notification-endpoint $ORDER_CANCELLED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"OrderCancelledEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-order-cancelled-queue (Filter: OrderCancelledEvent)"
  fi
fi

# ==========================================
# Payment Events -> Order Service
# Pattern: One queue per event type
# ==========================================
echo "  - Creating payment event subscriptions (Order Service)..."

# PaymentCompletedEvent -> gearify-payment-completed-queue (Order Service)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  PAYMENT_COMPLETED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-payment-completed-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$PAYMENT_COMPLETED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $PAYMENT_COMPLETED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentCompletedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-payment-completed-queue (Filter: PaymentCompletedEvent)"
  fi
fi

# PaymentFailedEvent -> gearify-payment-failed-queue (Order Service)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  PAYMENT_FAILED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-payment-failed-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$PAYMENT_FAILED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $PAYMENT_FAILED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentFailedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-payment-failed-queue (Filter: PaymentFailedEvent)"
  fi
fi

# RefundCompletedEvent -> gearify-refund-completed-queue (Order Service)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  REFUND_COMPLETED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-refund-completed-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$REFUND_COMPLETED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $REFUND_COMPLETED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"RefundCompletedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-refund-completed-queue (Filter: RefundCompletedEvent)"
  fi
fi

# ==========================================
# Payment Events -> Notification Service
# ==========================================
echo "  - Creating notification event subscriptions..."

# PaymentCompleted + PaymentFailed -> gearify-notification-payment-events-queue
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  NOTIFICATION_PAYMENT_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-notification-payment-events-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$NOTIFICATION_PAYMENT_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $NOTIFICATION_PAYMENT_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentCompletedEvent\",\"PaymentFailedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-notification-payment-events-queue (Filter: PaymentCompleted + PaymentFailed)"
  fi
fi

# RefundCompleted + RefundFailed -> gearify-notification-refund-queue
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  NOTIFICATION_REFUND_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-notification-refund-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$NOTIFICATION_REFUND_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $NOTIFICATION_REFUND_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"RefundCompletedEvent\",\"RefundFailedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-notification-refund-queue (Filter: RefundCompleted + RefundFailed)"
  fi
fi

# ==========================================
# Shipping Events -> Order Service
# ==========================================
echo "  - Creating shipping event subscriptions..."

# ShipmentCreated -> gearify-shipping-created-queue
if [ ! -z "$SHIPPING_TOPIC_ARN" ]; then
  SHIPPING_CREATED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-created-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SHIPPING_CREATED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $SHIPPING_TOPIC_ARN --protocol sqs --notification-endpoint $SHIPPING_CREATED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"ShipmentCreated\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-shipping-created-queue (Filter: ShipmentCreated)"
  fi
fi

# ShipmentStatusUpdated + ShipmentDelivered -> gearify-shipping-status-queue
if [ ! -z "$SHIPPING_TOPIC_ARN" ]; then
  SHIPPING_STATUS_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-status-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SHIPPING_STATUS_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $SHIPPING_TOPIC_ARN --protocol sqs --notification-endpoint $SHIPPING_STATUS_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"ShipmentStatusUpdated\",\"ShipmentDelivered\"]}"}' --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-shipping-status-queue (Filter: ShipmentStatusUpdated + ShipmentDelivered)"
  fi
fi

# ==========================================
# Media Processing Subscriptions
# ==========================================
echo "  - Creating media processing subscriptions..."

# MediaUploadedEvent -> gearify-image-processing-queue
if [ ! -z "$MEDIA_TOPIC_ARN" ]; then
  MEDIA_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-image-processing-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$MEDIA_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $MEDIA_TOPIC_ARN --protocol sqs --notification-endpoint $MEDIA_QUEUE_ARN --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-image-processing-queue to gearify-media-upload-events"
  fi
fi

# ImageProcessingCompletedEvent -> gearify-product-thumbnail-update-queue
if [ ! -z "$IMAGE_PROCESSING_COMPLETED_TOPIC_ARN" ]; then
  PRODUCT_THUMBNAIL_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-product-thumbnail-update-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$PRODUCT_THUMBNAIL_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $IMAGE_PROCESSING_COMPLETED_TOPIC_ARN --protocol sqs --notification-endpoint $PRODUCT_THUMBNAIL_QUEUE_ARN --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-product-thumbnail-update-queue to gearify-image-processing-completed"
  fi
fi

# ==========================================
# Catalog Events -> Search Service
# ==========================================
echo "  - Creating catalog event subscriptions..."

if [ ! -z "$CATALOG_EVENTS_TOPIC_ARN" ]; then
  SEARCH_CATALOG_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-search-catalog-events-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SEARCH_CATALOG_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $CATALOG_EVENTS_TOPIC_ARN --protocol sqs --notification-endpoint $SEARCH_CATALOG_QUEUE_ARN --region us-east-1 2>/dev/null || echo "    Failed to subscribe"
    echo "    Subscribed gearify-search-catalog-events-queue to catalog-events-topic"
  fi
fi

echo "SNS subscriptions created successfully!"
echo ""
