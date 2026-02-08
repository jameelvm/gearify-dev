#!/bin/bash

# SQS Queues Initialization
# Architecture: One Queue Per Event Type
# This script creates all SQS queues including DLQs

set -e

echo "=========================================="
echo "Creating SQS queues..."
echo "=========================================="

# ==========================================
# Dead Letter Queues (DLQs)
# ==========================================
echo "  - Creating dead letter queues..."

awslocal sqs create-queue \
  --queue-name gearify-order-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-events-dlq already exists"

awslocal sqs create-queue \
  --queue-name gearify-payment-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-payment-events-dlq already exists"

awslocal sqs create-queue \
  --queue-name gearify-shipping-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-events-dlq already exists"

# Get DLQ ARNs for redrive policy
ORDER_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
PAYMENT_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-payment-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
SHIPPING_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")

# ==========================================
# Order Events -> Payment Service
# One queue per event type
# ==========================================
echo "  - Creating order event queues (Payment Service consumers)..."

# OrderCreatedEvent -> Payment Service processes payment
awslocal sqs create-queue \
  --queue-name gearify-order-created-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$ORDER_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-created-queue already exists"

# OrderCancelledEvent -> Payment Service processes refund
awslocal sqs create-queue \
  --queue-name gearify-order-cancelled-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$ORDER_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-cancelled-queue already exists"

# ==========================================
# Payment Events -> Order Service
# One queue per event type
# ==========================================
echo "  - Creating payment event queues (Order Service consumers)..."

# PaymentCompletedEvent -> Order Service confirms order
awslocal sqs create-queue \
  --queue-name gearify-payment-completed-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-payment-completed-queue already exists"

# PaymentFailedEvent -> Order Service marks order as failed
awslocal sqs create-queue \
  --queue-name gearify-payment-failed-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-payment-failed-queue already exists"

# RefundCompletedEvent -> Order Service marks order as refunded
awslocal sqs create-queue \
  --queue-name gearify-refund-completed-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-refund-completed-queue already exists"

# ==========================================
# Payment Events -> Notification Service
# ==========================================
echo "  - Creating notification event queues..."

# Notification Service queue for payment events
awslocal sqs create-queue \
  --queue-name gearify-notification-payment-events-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-notification-payment-events-queue already exists"

# Notification Service queue for refund events
awslocal sqs create-queue \
  --queue-name gearify-notification-refund-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-notification-refund-queue already exists"

# ==========================================
# Order Events -> Shipping Service
# ==========================================
echo "  - Creating order event queues (Shipping Service consumers)..."

# OrderConfirmedEvent -> Shipping Service creates shipment
awslocal sqs create-queue \
  --queue-name gearify-order-confirmed-shipping-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$ORDER_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-confirmed-shipping-queue already exists"

# ==========================================
# Shipping Events -> Order Service
# ==========================================
echo "  - Creating shipping event queues..."

# ShippingShippedEvent -> Order Service (marks order as Shipped)
awslocal sqs create-queue \
  --queue-name gearify-shipping-shipped-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$SHIPPING_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-shipped-queue already exists"

# ShippingDeliveredEvent -> Order Service (marks order as Delivered)
awslocal sqs create-queue \
  --queue-name gearify-shipping-delivered-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$SHIPPING_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-delivered-queue already exists"

# ==========================================
# Other Service Queues
# ==========================================
echo "  - Creating other service queues..."

# Search Service queue for catalog events
awslocal sqs create-queue \
  --queue-name gearify-search-catalog-events-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-search-catalog-events-queue already exists"

# Image processing queue
awslocal sqs create-queue \
  --queue-name gearify-image-processing-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-image-processing-queue already exists"

# Product thumbnail update queue
awslocal sqs create-queue \
  --queue-name gearify-product-thumbnail-update-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-product-thumbnail-update-queue already exists"

echo "SQS queues created successfully!"
echo ""
