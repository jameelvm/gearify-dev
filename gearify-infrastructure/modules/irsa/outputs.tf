# IRSA Module Outputs

output "service_role_arns" {
  description = "Map of service names to IAM role ARNs"
  value       = { for k, v in aws_iam_role.service_roles : k => v.arn }
}

output "catalog_svc_role_arn" {
  description = "IAM role ARN for catalog-svc"
  value       = aws_iam_role.service_roles["catalog-svc"].arn
}

output "tenant_svc_role_arn" {
  description = "IAM role ARN for tenant-svc"
  value       = aws_iam_role.service_roles["tenant-svc"].arn
}

output "auth_svc_role_arn" {
  description = "IAM role ARN for auth-svc"
  value       = aws_iam_role.service_roles["auth-svc"].arn
}

output "search_svc_role_arn" {
  description = "IAM role ARN for search-svc"
  value       = aws_iam_role.service_roles["search-svc"].arn
}

output "order_svc_role_arn" {
  description = "IAM role ARN for order-svc"
  value       = aws_iam_role.service_roles["order-svc"].arn
}

output "payment_svc_role_arn" {
  description = "IAM role ARN for payment-svc"
  value       = aws_iam_role.service_roles["payment-svc"].arn
}

output "shipping_svc_role_arn" {
  description = "IAM role ARN for shipping-svc"
  value       = aws_iam_role.service_roles["shipping-svc"].arn
}

output "inventory_svc_role_arn" {
  description = "IAM role ARN for inventory-svc"
  value       = aws_iam_role.service_roles["inventory-svc"].arn
}

output "media_svc_role_arn" {
  description = "IAM role ARN for media-svc"
  value       = aws_iam_role.service_roles["media-svc"].arn
}

output "notification_svc_role_arn" {
  description = "IAM role ARN for notification-svc"
  value       = aws_iam_role.service_roles["notification-svc"].arn
}
