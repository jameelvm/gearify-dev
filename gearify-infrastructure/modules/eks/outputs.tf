# EKS Module Outputs

output "cluster_id" {
  description = "The ID of the EKS cluster"
  value       = aws_eks_cluster.main.id
}

output "cluster_name" {
  description = "The name of the EKS cluster"
  value       = aws_eks_cluster.main.name
}

output "cluster_arn" {
  description = "The ARN of the EKS cluster"
  value       = aws_eks_cluster.main.arn
}

output "cluster_endpoint" {
  description = "The endpoint for the EKS cluster API server"
  value       = aws_eks_cluster.main.endpoint
}

output "cluster_certificate_authority_data" {
  description = "Base64 encoded certificate data for the cluster"
  value       = aws_eks_cluster.main.certificate_authority[0].data
}

output "cluster_security_group_id" {
  description = "Security group ID for the EKS cluster"
  value       = aws_security_group.cluster.id
}

output "node_security_group_id" {
  description = "Security group ID for the EKS nodes"
  value       = aws_security_group.node_group.id
}

output "oidc_provider_arn" {
  description = "The ARN of the OIDC provider"
  value       = aws_iam_openid_connect_provider.cluster.arn
}

output "oidc_provider_url" {
  description = "The URL of the OIDC provider"
  value       = aws_iam_openid_connect_provider.cluster.url
}

output "node_group_role_arn" {
  description = "The ARN of the node group IAM role"
  value       = aws_iam_role.node_group.arn
}

output "aws_lb_controller_role_arn" {
  description = "The ARN of the AWS Load Balancer Controller IAM role"
  value       = aws_iam_role.aws_lb_controller.arn
}

output "external_secrets_role_arn" {
  description = "The ARN of the External Secrets Operator IAM role"
  value       = aws_iam_role.external_secrets.arn
}

output "cluster_version" {
  description = "The Kubernetes version of the EKS cluster"
  value       = aws_eks_cluster.main.version
}
