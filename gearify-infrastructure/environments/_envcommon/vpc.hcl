# Common VPC configuration shared across environments

terraform {
  source = "${get_terragrunt_dir()}/../../../../modules/vpc"
}

locals {
  env_vars = read_terragrunt_config(find_in_parent_folders("env.hcl"))
  env      = local.env_vars.locals.environment
  region   = local.env_vars.locals.aws_region
}

inputs = {
  project     = "gearify"
  environment = local.env
  aws_region  = local.region
}
