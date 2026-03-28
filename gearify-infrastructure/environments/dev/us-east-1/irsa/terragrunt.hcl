# IRSA configuration for Dev environment

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

include "env" {
  path   = "${get_terragrunt_dir()}/../../env.hcl"
  expose = true
}

terraform {
  source = "../../../../modules/irsa"
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

dependency "eks" {
  config_path = "../eks"

  mock_outputs = {
    oidc_provider_arn = "arn:aws:iam::123456789012:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
    oidc_provider_url = "https://oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
  }
}

inputs = {
  project           = "gearify"
  environment       = include.env.locals.environment
  aws_region        = include.env.locals.aws_region
  oidc_provider_arn = dependency.eks.outputs.oidc_provider_arn
  oidc_provider_url = dependency.eks.outputs.oidc_provider_url
  namespace         = "gearify-dev"
}
