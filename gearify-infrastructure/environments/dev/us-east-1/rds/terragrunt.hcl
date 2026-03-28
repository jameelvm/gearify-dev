# RDS configuration for Dev environment

include "root" {
  path = find_in_parent_folders("terragrunt.hcl")
}

include "env" {
  path   = "${get_terragrunt_dir()}/../../env.hcl"
  expose = true
}

terraform {
  source = "../../../../modules/rds"
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
    vpc_id               = "vpc-mock"
    db_subnet_group_name = "mock-subnet-group"
  }
}

dependency "eks" {
  config_path = "../eks"

  mock_outputs = {
    node_security_group_id = "sg-mock"
  }
}

inputs = {
  project                     = "gearify"
  environment                 = include.env.locals.environment
  vpc_id                      = dependency.vpc.outputs.vpc_id
  db_subnet_group_name        = dependency.vpc.outputs.db_subnet_group_name
  allowed_security_groups     = [dependency.eks.outputs.node_security_group_id]
  engine_version              = "16.3"
  instance_class              = include.env.locals.rds_instance_class
  allocated_storage           = 20
  max_allocated_storage       = 100
  multi_az                    = include.env.locals.rds_multi_az
  deletion_protection         = include.env.locals.rds_deletion_protection
  skip_final_snapshot         = include.env.locals.rds_skip_final_snapshot
  backup_retention_period     = 7
  performance_insights_enabled = include.env.locals.enable_performance_insights
  create_additional_databases = true
}
