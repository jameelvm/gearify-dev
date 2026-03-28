# VPC Module Variables

variable "project" {
  description = "Project name"
  type        = string
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
}

variable "aws_region" {
  description = "AWS region"
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "az_count" {
  description = "Number of availability zones to use"
  type        = number
  default     = 3
}

variable "nat_gateway_count" {
  description = "Number of NAT gateways (1 for dev, 3 for prod)"
  type        = number
  default     = 1
}

variable "enable_ecr_endpoints" {
  description = "Enable VPC endpoints for ECR"
  type        = bool
  default     = true
}

variable "enable_secrets_endpoint" {
  description = "Enable VPC endpoint for Secrets Manager"
  type        = bool
  default     = true
}

variable "enable_sts_endpoint" {
  description = "Enable VPC endpoint for STS"
  type        = bool
  default     = true
}
