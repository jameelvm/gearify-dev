# OpenSearch Module Variables

variable "project" {
  description = "Project name"
  type        = string
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
}

variable "vpc_id" {
  description = "VPC ID"
  type        = string
}

variable "subnet_ids" {
  description = "List of subnet IDs for OpenSearch"
  type        = list(string)
}

variable "allowed_security_groups" {
  description = "List of security group IDs allowed to access OpenSearch"
  type        = list(string)
}

variable "engine_version" {
  description = "OpenSearch engine version"
  type        = string
  default     = "OpenSearch_2.11"
}

variable "instance_type" {
  description = "OpenSearch instance type"
  type        = string
  default     = "t3.small.search"
}

variable "instance_count" {
  description = "Number of instances"
  type        = number
  default     = 1
}

variable "dedicated_master_enabled" {
  description = "Enable dedicated master nodes"
  type        = bool
  default     = false
}

variable "dedicated_master_type" {
  description = "Instance type for dedicated master nodes"
  type        = string
  default     = "t3.small.search"
}

variable "dedicated_master_count" {
  description = "Number of dedicated master nodes"
  type        = number
  default     = 3
}

variable "zone_awareness_enabled" {
  description = "Enable zone awareness"
  type        = bool
  default     = false
}

variable "availability_zone_count" {
  description = "Number of availability zones"
  type        = number
  default     = 2
}

variable "ebs_volume_size" {
  description = "EBS volume size in GB"
  type        = number
  default     = 20
}

variable "ebs_iops" {
  description = "EBS IOPS"
  type        = number
  default     = 3000
}

variable "ebs_throughput" {
  description = "EBS throughput in MiB/s"
  type        = number
  default     = 125
}

variable "master_username" {
  description = "Master username for OpenSearch"
  type        = string
  default     = "admin"
}

variable "log_retention_days" {
  description = "CloudWatch log retention in days"
  type        = number
  default     = 14
}
