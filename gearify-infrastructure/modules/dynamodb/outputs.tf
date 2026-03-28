# DynamoDB Module Outputs

output "products_table_name" {
  description = "Name of the products table"
  value       = aws_dynamodb_table.products.name
}

output "products_table_arn" {
  description = "ARN of the products table"
  value       = aws_dynamodb_table.products.arn
}

output "tenants_table_name" {
  description = "Name of the tenants table"
  value       = aws_dynamodb_table.tenants.name
}

output "tenants_table_arn" {
  description = "ARN of the tenants table"
  value       = aws_dynamodb_table.tenants.arn
}

output "users_table_name" {
  description = "Name of the users table"
  value       = aws_dynamodb_table.users.name
}

output "users_table_arn" {
  description = "ARN of the users table"
  value       = aws_dynamodb_table.users.arn
}

output "feature_flags_table_name" {
  description = "Name of the feature flags table"
  value       = aws_dynamodb_table.feature_flags.name
}

output "feature_flags_table_arn" {
  description = "ARN of the feature flags table"
  value       = aws_dynamodb_table.feature_flags.arn
}

output "inventory_table_name" {
  description = "Name of the inventory table"
  value       = aws_dynamodb_table.inventory.name
}

output "inventory_table_arn" {
  description = "ARN of the inventory table"
  value       = aws_dynamodb_table.inventory.arn
}

output "media_table_name" {
  description = "Name of the media table"
  value       = aws_dynamodb_table.media.name
}

output "media_table_arn" {
  description = "ARN of the media table"
  value       = aws_dynamodb_table.media.arn
}

output "dynamodb_access_policy_arn" {
  description = "ARN of the DynamoDB access IAM policy"
  value       = aws_iam_policy.dynamodb_access.arn
}
