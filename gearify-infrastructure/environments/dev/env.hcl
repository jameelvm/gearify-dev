# Dev environment configuration
# Optimized for AWS Free Tier (new account)

locals {
  environment = "dev"
  aws_region  = "us-east-1"

  # Dev-specific settings
  vpc_cidr          = "10.0.0.0/16"
  nat_gateway_count = 1

  # EKS settings (t2.micro is free tier but limited)
  # Using t3.micro for better performance, ~$7.60/month
  eks_node_instance_types = ["t3.micro"]
  eks_node_desired_size   = 1
  eks_node_min_size       = 1
  eks_node_max_size       = 3

  # RDS settings - FREE TIER (750 hrs/month for 12 months)
  rds_instance_class       = "db.t2.micro"
  rds_multi_az             = false
  rds_deletion_protection  = false
  rds_skip_final_snapshot  = true

  # ElastiCache settings - FREE TIER (750 hrs/month for 12 months)
  redis_node_type           = "cache.t2.micro"
  redis_num_cache_clusters  = 1

  # Feature flags
  enable_performance_insights = false
  point_in_time_recovery      = false

  # OpenSearch - DISABLED (no free tier, ~$35/month)
  # Enable when needed: enable_opensearch = true
  enable_opensearch = false
}
