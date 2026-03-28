# Root Terragrunt configuration for Gearify Infrastructure
# This file contains common configuration shared across all environments

locals {
  # Common tags applied to all resources
  common_tags = {
    Project    = "gearify"
    ManagedBy  = "terragrunt"
    Repository = "gearify-infrastructure"
  }
}

# Configure Terragrunt to automatically store state files in S3
# For local validation, use --terragrunt-disable-bucket-update flag
# For actual deployment, set AWS_ACCOUNT_ID env var or replace the placeholder
remote_state {
  backend = "s3"
  config = {
    encrypt        = true
    bucket         = "gearify-terraform-state-${get_env("AWS_ACCOUNT_ID", "PLACEHOLDER")}"
    key            = "${path_relative_to_include()}/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "gearify-terraform-locks"

    # Skip bucket creation/validation during local testing
    skip_bucket_versioning         = true
    skip_bucket_ssencryption       = true
    skip_bucket_root_access        = true
    skip_bucket_enforced_tls       = true
    skip_bucket_public_access_blocking = true
  }
  generate = {
    path      = "backend.tf"
    if_exists = "overwrite_terragrunt"
  }
}

# Generate provider configuration with all required providers
generate "provider" {
  path      = "provider.tf"
  if_exists = "overwrite_terragrunt"
  contents  = <<EOF
terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.25"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.12"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
}
EOF
}
