# SNS/SQS Module Outputs

output "sns_topic_arns" {
  description = "Map of SNS topic names to ARNs"
  value       = { for k, v in aws_sns_topic.topics : k => v.arn }
}

output "sns_topic_names" {
  description = "Map of SNS topic keys to full names"
  value       = { for k, v in aws_sns_topic.topics : k => v.name }
}

output "sqs_queue_arns" {
  description = "Map of SQS queue names to ARNs"
  value       = { for k, v in aws_sqs_queue.queues : k => v.arn }
}

output "sqs_queue_urls" {
  description = "Map of SQS queue names to URLs"
  value       = { for k, v in aws_sqs_queue.queues : k => v.url }
}

output "sqs_queue_names" {
  description = "Map of SQS queue keys to full names"
  value       = { for k, v in aws_sqs_queue.queues : k => v.name }
}

output "dlq_arns" {
  description = "Map of DLQ names to ARNs"
  value       = { for k, v in aws_sqs_queue.dlq : k => v.arn }
}

output "messaging_policy_arn" {
  description = "ARN of the messaging IAM policy"
  value       = aws_iam_policy.messaging.arn
}

# Outputs formatted for service configuration
output "order_events_topic_arn" {
  description = "ARN of the order events SNS topic"
  value       = aws_sns_topic.topics["order-events"].arn
}

output "payment_events_topic_arn" {
  description = "ARN of the payment events SNS topic"
  value       = aws_sns_topic.topics["payment-events"].arn
}

output "shipping_events_topic_arn" {
  description = "ARN of the shipping events SNS topic"
  value       = aws_sns_topic.topics["shipping-events"].arn
}

output "catalog_events_topic_arn" {
  description = "ARN of the catalog events SNS topic"
  value       = aws_sns_topic.topics["catalog-events"].arn
}
