# ECR Module Outputs

output "repository_arns" {
  description = "Map of repository names to ARNs"
  value       = { for k, v in aws_ecr_repository.repos : k => v.arn }
}

output "repository_urls" {
  description = "Map of repository names to URLs"
  value       = { for k, v in aws_ecr_repository.repos : k => v.repository_url }
}

output "repository_registry_id" {
  description = "The registry ID where the repositories are created"
  value       = values(aws_ecr_repository.repos)[0].registry_id
}

output "ecr_push_policy_arn" {
  description = "ARN of the ECR push IAM policy"
  value       = aws_iam_policy.ecr_push.arn
}

output "ecr_pull_policy_arn" {
  description = "ARN of the ECR pull IAM policy"
  value       = aws_iam_policy.ecr_pull.arn
}

# Individual repository URLs for convenience
output "api_gateway_repository_url" {
  description = "URL of the API Gateway repository"
  value       = aws_ecr_repository.repos["gearify-api-gateway"].repository_url
}

output "web_repository_url" {
  description = "URL of the Web repository"
  value       = aws_ecr_repository.repos["gearify-web"].repository_url
}

output "catalog_svc_repository_url" {
  description = "URL of the Catalog Service repository"
  value       = aws_ecr_repository.repos["gearify-catalog-svc"].repository_url
}

output "auth_svc_repository_url" {
  description = "URL of the Auth Service repository"
  value       = aws_ecr_repository.repos["gearify-auth-svc"].repository_url
}

output "order_svc_repository_url" {
  description = "URL of the Order Service repository"
  value       = aws_ecr_repository.repos["gearify-order-svc"].repository_url
}

output "payment_svc_repository_url" {
  description = "URL of the Payment Service repository"
  value       = aws_ecr_repository.repos["gearify-payment-svc"].repository_url
}

output "shipping_svc_repository_url" {
  description = "URL of the Shipping Service repository"
  value       = aws_ecr_repository.repos["gearify-shipping-svc"].repository_url
}
