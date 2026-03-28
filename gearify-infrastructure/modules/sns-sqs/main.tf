# SNS/SQS Module for Gearify
# Creates event-driven messaging infrastructure

locals {
  # SNS Topics configuration
  sns_topics = {
    "order-events"                 = "Order events (created, cancelled, confirmed)"
    "payment-events"               = "Payment events (completed, failed, refunded)"
    "shipping-events"              = "Shipping events (shipped, delivered)"
    "media-upload-events"          = "Media upload events"
    "image-processing-completed"   = "Image processing completion events"
    "catalog-events"               = "Catalog events (product updates)"
  }

  # SQS Queues with their topic subscriptions and filter policies
  sqs_queues = {
    # Order Events -> Payment Service
    "order-created-queue" = {
      topic        = "order-events"
      filter_policy = jsonencode({ eventType = ["OrderCreatedEvent"] })
      dlq          = "order-events-dlq"
    }
    "order-cancelled-queue" = {
      topic        = "order-events"
      filter_policy = jsonencode({ eventType = ["OrderCancelledEvent"] })
      dlq          = "order-events-dlq"
    }
    "order-confirmed-shipping-queue" = {
      topic        = "order-events"
      filter_policy = jsonencode({ eventType = ["OrderConfirmedEvent"] })
      dlq          = "order-events-dlq"
    }

    # Payment Events -> Order Service
    "payment-completed-queue" = {
      topic        = "payment-events"
      filter_policy = jsonencode({ eventType = ["PaymentCompletedEvent"] })
      dlq          = "payment-events-dlq"
    }
    "payment-failed-queue" = {
      topic        = "payment-events"
      filter_policy = jsonencode({ eventType = ["PaymentFailedEvent"] })
      dlq          = "payment-events-dlq"
    }
    "refund-completed-queue" = {
      topic        = "payment-events"
      filter_policy = jsonencode({ eventType = ["RefundCompletedEvent"] })
      dlq          = "payment-events-dlq"
    }

    # Notification queues
    "notification-payment-events-queue" = {
      topic        = "payment-events"
      filter_policy = jsonencode({ eventType = ["PaymentCompletedEvent", "PaymentFailedEvent"] })
      dlq          = "payment-events-dlq"
    }
    "notification-refund-queue" = {
      topic        = "payment-events"
      filter_policy = jsonencode({ eventType = ["RefundCompletedEvent"] })
      dlq          = "payment-events-dlq"
    }

    # Shipping Events -> Order Service
    "shipping-shipped-queue" = {
      topic        = "shipping-events"
      filter_policy = jsonencode({ eventType = ["ShippingShippedEvent"] })
      dlq          = "shipping-events-dlq"
    }
    "shipping-delivered-queue" = {
      topic        = "shipping-events"
      filter_policy = jsonencode({ eventType = ["ShippingDeliveredEvent"] })
      dlq          = "shipping-events-dlq"
    }

    # Media & Search
    "image-processing-queue" = {
      topic        = "media-upload-events"
      filter_policy = null
      dlq          = null
    }
    "product-thumbnail-update-queue" = {
      topic        = "image-processing-completed"
      filter_policy = null
      dlq          = null
    }
    "search-catalog-events-queue" = {
      topic        = "catalog-events"
      filter_policy = null
      dlq          = null
    }
  }

  # Dead Letter Queues
  dlqs = toset(["order-events-dlq", "payment-events-dlq", "shipping-events-dlq"])
}

# Dead Letter Queues
resource "aws_sqs_queue" "dlq" {
  for_each = local.dlqs

  name                       = "gearify-${each.key}-${var.environment}"
  message_retention_seconds  = 1209600  # 14 days
  visibility_timeout_seconds = 300

  tags = {
    Name        = "gearify-${each.key}-${var.environment}"
    Environment = var.environment
    Type        = "dlq"
  }
}

# SNS Topics
resource "aws_sns_topic" "topics" {
  for_each = local.sns_topics

  name         = "gearify-${each.key}-${var.environment}"
  display_name = each.value

  tags = {
    Name        = "gearify-${each.key}-${var.environment}"
    Environment = var.environment
  }
}

# SQS Queues
resource "aws_sqs_queue" "queues" {
  for_each = local.sqs_queues

  name                       = "gearify-${each.key}-${var.environment}"
  visibility_timeout_seconds = 300
  message_retention_seconds  = 1209600  # 14 days
  receive_wait_time_seconds  = 20       # Long polling

  redrive_policy = each.value.dlq != null ? jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq[each.value.dlq].arn
    maxReceiveCount     = 3
  }) : null

  tags = {
    Name        = "gearify-${each.key}-${var.environment}"
    Environment = var.environment
    Topic       = each.value.topic
  }
}

# SQS Queue Policies - Allow SNS to send messages
resource "aws_sqs_queue_policy" "queue_policies" {
  for_each = local.sqs_queues

  queue_url = aws_sqs_queue.queues[each.key].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowSNSMessages"
        Effect    = "Allow"
        Principal = {
          Service = "sns.amazonaws.com"
        }
        Action   = "sqs:SendMessage"
        Resource = aws_sqs_queue.queues[each.key].arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = aws_sns_topic.topics[each.value.topic].arn
          }
        }
      }
    ]
  })
}

# SNS Subscriptions
resource "aws_sns_topic_subscription" "subscriptions" {
  for_each = local.sqs_queues

  topic_arn            = aws_sns_topic.topics[each.value.topic].arn
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.queues[each.key].arn
  raw_message_delivery = true
  filter_policy        = each.value.filter_policy
  filter_policy_scope  = each.value.filter_policy != null ? "MessageAttributes" : null
}

# IAM Policy for services to access SNS/SQS
resource "aws_iam_policy" "messaging" {
  name        = "${var.project}-${var.environment}-messaging-policy"
  description = "IAM policy for SNS/SQS access"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "SNSPublish"
        Effect = "Allow"
        Action = [
          "sns:Publish",
          "sns:GetTopicAttributes"
        ]
        Resource = [for topic in aws_sns_topic.topics : topic.arn]
      },
      {
        Sid    = "SQSAccess"
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:GetQueueUrl",
          "sqs:ChangeMessageVisibility"
        ]
        Resource = concat(
          [for queue in aws_sqs_queue.queues : queue.arn],
          [for dlq in aws_sqs_queue.dlq : dlq.arn]
        )
      }
    ]
  })
}
