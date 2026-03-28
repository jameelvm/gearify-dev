# OpenSearch Module Outputs

output "domain_id" {
  description = "The ID of the OpenSearch domain"
  value       = aws_opensearch_domain.main.domain_id
}

output "domain_arn" {
  description = "The ARN of the OpenSearch domain"
  value       = aws_opensearch_domain.main.arn
}

output "domain_endpoint" {
  description = "The endpoint of the OpenSearch domain"
  value       = aws_opensearch_domain.main.endpoint
}

output "domain_name" {
  description = "The name of the OpenSearch domain"
  value       = aws_opensearch_domain.main.domain_name
}

output "kibana_endpoint" {
  description = "The Kibana endpoint"
  value       = aws_opensearch_domain.main.dashboard_endpoint
}

output "security_group_id" {
  description = "The security group ID for OpenSearch"
  value       = aws_security_group.opensearch.id
}

output "secret_arn" {
  description = "The ARN of the Secrets Manager secret"
  value       = aws_secretsmanager_secret.opensearch.arn
}

output "secret_name" {
  description = "The name of the Secrets Manager secret"
  value       = aws_secretsmanager_secret.opensearch.name
}
