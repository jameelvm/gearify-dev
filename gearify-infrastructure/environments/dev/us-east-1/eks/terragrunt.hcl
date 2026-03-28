# EKS configuration for Dev environment

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

include "env" {
  path   = "${get_terragrunt_dir()}/../../env.hcl"
  expose = true
}

terraform {
  source = "../../../../modules/eks"
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

dependency "vpc" {
  config_path = "../vpc"

  mock_outputs = {
    vpc_id             = "vpc-mock"
    private_subnet_ids = ["subnet-1", "subnet-2", "subnet-3"]
  }
}

inputs = {
  project                = "gearify"
  environment            = include.env.locals.environment
  aws_region             = include.env.locals.aws_region
  vpc_id                 = dependency.vpc.outputs.vpc_id
  private_subnet_ids     = dependency.vpc.outputs.private_subnet_ids
  cluster_version        = "1.29"
  endpoint_public_access = true
  node_instance_types    = include.env.locals.eks_node_instance_types
  capacity_type          = "ON_DEMAND"
  node_desired_size      = include.env.locals.eks_node_desired_size
  node_min_size          = include.env.locals.eks_node_min_size
  node_max_size          = include.env.locals.eks_node_max_size
  node_disk_size         = 50
}
