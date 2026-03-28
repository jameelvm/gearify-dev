# ECR Module Variables

variable "project" {
  description = "Project name"
  type        = string
}

variable "image_tag_mutability" {
  description = "The tag mutability setting for the repository (MUTABLE or IMMUTABLE)"
  type        = string
  default     = "MUTABLE"
}

variable "scan_on_push" {
  description = "Enable image scanning on push"
  type        = bool
  default     = true
}

variable "keep_image_count" {
  description = "Number of tagged images to keep"
  type        = number
  default     = 30
}

variable "untagged_image_days" {
  description = "Days to keep untagged images"
  type        = number
  default     = 7
}

variable "feature_branch_days" {
  description = "Days to keep feature branch images"
  type        = number
  default     = 14
}

variable "enable_cross_account_access" {
  description = "Enable cross-account access to ECR"
  type        = bool
  default     = false
}

variable "cross_account_arns" {
  description = "List of AWS account ARNs for cross-account access"
  type        = list(string)
  default     = []
}
