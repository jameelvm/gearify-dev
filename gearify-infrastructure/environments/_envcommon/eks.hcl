# Common EKS configuration shared across environments

terraform {
  source = "${get_terragrunt_dir()}/../../../../modules/eks"
}

locals {
  env_vars = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  env      = local.env_vars.locals.environment
  region   = local.env_vars.locals.aws_region
}

dependency "vpc" {
  config_path = "../vpc"

  mock_outputs = {
    vpc_id             = "vpc-mock"
    private_subnet_ids = ["subnet-1", "subnet-2", "subnet-3"]
  }
}

inputs = {
  project            = "gearify"
  environment        = local.env
  aws_region         = local.region
  vpc_id             = dependency.vpc.outputs.vpc_id
  private_subnet_ids = dependency.vpc.outputs.private_subnet_ids
}
