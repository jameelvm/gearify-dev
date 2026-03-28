# Global ECR configuration (shared across all environments)

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

terraform {
  source = "../../modules/ecr"
}

# Generate AWS provider with region (ECR is global, use us-east-1)
generate "aws_provider" {
  path      = "provider_aws.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<EOF
provider "aws" {
  region = "us-east-1"

  default_tags {
    tags = {
      Project   = "gearify"
      ManagedBy = "terragrunt"
    }
  }
}
EOF
}

inputs = {
  project                     = "gearify"
  image_tag_mutability        = "MUTABLE"
  scan_on_push                = true
  keep_image_count            = 30
  untagged_image_days         = 7
  feature_branch_days         = 14
  enable_cross_account_access = false
  cross_account_arns          = []
}
