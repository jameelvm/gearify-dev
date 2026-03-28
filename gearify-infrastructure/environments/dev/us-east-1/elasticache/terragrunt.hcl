# ElastiCache configuration for Dev environment

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

include "env" {
  path   = "${get_terragrunt_dir()}/../../env.hcl"
  expose = true
}

terraform {
  source = "../../../../modules/elasticache"
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
    vpc_id                        = "vpc-mock"
    elasticache_subnet_group_name = "mock-subnet-group"
  }
}

dependency "eks" {
  config_path = "../eks"

  mock_outputs = {
    node_security_group_id = "sg-mock"
  }
}

inputs = {
  project                    = "gearify"
  environment                = include.env.locals.environment
  vpc_id                     = dependency.vpc.outputs.vpc_id
  subnet_group_name          = dependency.vpc.outputs.elasticache_subnet_group_name
  allowed_security_groups    = [dependency.eks.outputs.node_security_group_id]
  engine_version             = "7.1"
  node_type                  = include.env.locals.redis_node_type
  num_cache_clusters         = include.env.locals.redis_num_cache_clusters
  transit_encryption_enabled = false
  snapshot_retention_limit   = 1
}
