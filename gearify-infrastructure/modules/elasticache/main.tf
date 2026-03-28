# ElastiCache Redis Module for Gearify
# Creates Redis cluster for caching and session management

# Random auth token for Redis
resource "random_password" "redis_auth_token" {
  count            = var.transit_encryption_enabled ? 1 : 0
  length           = 32
  special          = false
}

# Security Group for ElastiCache
resource "aws_security_group" "redis" {
  name        = "${var.project}-${var.environment}-redis-sg"
  description = "Security group for ElastiCache Redis"
  vpc_id      = var.vpc_id

  ingress {
    description     = "Redis from EKS nodes"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = var.allowed_security_groups
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project}-${var.environment}-redis-sg"
  }
}

# Parameter Group
resource "aws_elasticache_parameter_group" "main" {
  name        = "${var.project}-${var.environment}-redis7"
  family      = "redis7"
  description = "Custom parameter group for ${var.project} ${var.environment}"

  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"
  }

  tags = {
    Name = "${var.project}-${var.environment}-redis7"
  }
}

# ElastiCache Replication Group
resource "aws_elasticache_replication_group" "main" {
  replication_group_id = "${var.project}-${var.environment}"
  description          = "Redis cluster for ${var.project} ${var.environment}"

  engine               = "redis"
  engine_version       = var.engine_version
  node_type            = var.node_type
  num_cache_clusters   = var.num_cache_clusters
  port                 = 6379
  parameter_group_name = aws_elasticache_parameter_group.main.name

  subnet_group_name  = var.subnet_group_name
  security_group_ids = [aws_security_group.redis.id]

  automatic_failover_enabled = var.num_cache_clusters > 1
  multi_az_enabled          = var.num_cache_clusters > 1

  at_rest_encryption_enabled = true
  transit_encryption_enabled = var.transit_encryption_enabled
  auth_token                 = var.transit_encryption_enabled ? random_password.redis_auth_token[0].result : null

  snapshot_retention_limit = var.snapshot_retention_limit
  snapshot_window         = "03:00-04:00"
  maintenance_window      = "Mon:04:00-Mon:05:00"

  auto_minor_version_upgrade = true

  tags = {
    Name = "${var.project}-${var.environment}-redis"
  }
}

# Store credentials in Secrets Manager
resource "aws_secretsmanager_secret" "redis_credentials" {
  name        = "gearify/${var.environment}/redis"
  description = "Redis credentials for ${var.project} ${var.environment}"

  tags = {
    Name = "gearify/${var.environment}/redis"
  }
}

resource "aws_secretsmanager_secret_version" "redis_credentials" {
  secret_id = aws_secretsmanager_secret.redis_credentials.id
  secret_string = jsonencode({
    host              = aws_elasticache_replication_group.main.primary_endpoint_address
    port              = 6379
    auth_token        = var.transit_encryption_enabled ? random_password.redis_auth_token[0].result : ""
    connection_string = var.transit_encryption_enabled ? "rediss://:${random_password.redis_auth_token[0].result}@${aws_elasticache_replication_group.main.primary_endpoint_address}:6379" : "redis://${aws_elasticache_replication_group.main.primary_endpoint_address}:6379"
  })
}
