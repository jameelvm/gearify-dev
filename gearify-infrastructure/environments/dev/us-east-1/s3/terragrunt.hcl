# S3 configuration for Dev environment

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

include "env" {
  path   = "${get_terragrunt_dir()}/../../env.hcl"
  expose = true
}

terraform {
  source = "../../../../modules/s3"
}

# Generate AWS provider with region
generate "aws_provider" {
  path      = "provider_aws.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<EOF
provider "aws" {
  region = "${include.env.locals.aws_region}"

  default_tags {
    tags = {
      Project     = "gearify"
      Environment = "${include.env.locals.environment}"
      ManagedBy   = "terragrunt"
    }
  }
}
EOF
}

inputs = {
  project              = "gearify"
  environment          = include.env.locals.environment
  cors_allowed_origins = ["*"]  # In prod, restrict to specific domains
}
