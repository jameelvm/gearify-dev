# RDS PostgreSQL Module for Gearify
# Creates PostgreSQL instances for Order, Payment, and Shipping services

# Random password for database
resource "random_password" "db_password" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

# Security Group for RDS
resource "aws_security_group" "rds" {
  name        = "${var.project}-${var.environment}-rds-sg"
  description = "Security group for RDS PostgreSQL"
  vpc_id      = var.vpc_id

  ingress {
    description     = "PostgreSQL from EKS nodes"
    from_port       = 5432
    to_port         = 5432
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
    Name = "${var.project}-${var.environment}-rds-sg"
  }
}

# Parameter Group
resource "aws_db_parameter_group" "main" {
  name        = "${var.project}-${var.environment}-pg16"
  family      = "postgres16"
  description = "Custom parameter group for ${var.project} ${var.environment}"

  parameter {
    name  = "log_connections"
    value = "1"
  }

  parameter {
    name  = "log_disconnections"
    value = "1"
  }

  parameter {
    name  = "log_statement"
    value = "ddl"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = "1000"
  }

  tags = {
    Name = "${var.project}-${var.environment}-pg16"
  }
}

# RDS Instance
resource "aws_db_instance" "main" {
  identifier = "${var.project}-${var.environment}-postgres"

  engine               = "postgres"
  engine_version       = var.engine_version
  instance_class       = var.instance_class
  allocated_storage    = var.allocated_storage
  max_allocated_storage = var.max_allocated_storage
  storage_type         = "gp3"
  storage_encrypted    = true

  db_name  = "gearify"
  username = var.db_username
  password = random_password.db_password.result

  vpc_security_group_ids = [aws_security_group.rds.id]
  db_subnet_group_name   = var.db_subnet_group_name
  parameter_group_name   = aws_db_parameter_group.main.name

  multi_az               = var.multi_az
  publicly_accessible    = false
  deletion_protection    = var.deletion_protection
  skip_final_snapshot    = var.skip_final_snapshot
  final_snapshot_identifier = var.skip_final_snapshot ? null : "${var.project}-${var.environment}-final-snapshot"

  backup_retention_period = var.backup_retention_period
  backup_window          = "03:00-04:00"
  maintenance_window     = "Mon:04:00-Mon:05:00"

  performance_insights_enabled          = var.performance_insights_enabled
  performance_insights_retention_period = var.performance_insights_enabled ? 7 : null

  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  tags = {
    Name = "${var.project}-${var.environment}-postgres"
  }
}

# Store credentials in Secrets Manager
resource "aws_secretsmanager_secret" "db_credentials" {
  name        = "gearify/${var.environment}/database/main"
  description = "PostgreSQL credentials for ${var.project} ${var.environment}"

  tags = {
    Name = "gearify/${var.environment}/database/main"
  }
}

resource "aws_secretsmanager_secret_version" "db_credentials" {
  secret_id = aws_secretsmanager_secret.db_credentials.id
  secret_string = jsonencode({
    username             = var.db_username
    password             = random_password.db_password.result
    host                 = aws_db_instance.main.address
    port                 = aws_db_instance.main.port
    database             = "gearify"
    connection_string    = "Host=${aws_db_instance.main.address};Port=${aws_db_instance.main.port};Database=gearify;Username=${var.db_username};Password=${random_password.db_password.result}"
    orders_connection    = "Host=${aws_db_instance.main.address};Port=${aws_db_instance.main.port};Database=gearify_orders;Username=${var.db_username};Password=${random_password.db_password.result}"
    payments_connection  = "Host=${aws_db_instance.main.address};Port=${aws_db_instance.main.port};Database=gearify_payments;Username=${var.db_username};Password=${random_password.db_password.result}"
    shipping_connection  = "Host=${aws_db_instance.main.address};Port=${aws_db_instance.main.port};Database=gearify_shipping;Username=${var.db_username};Password=${random_password.db_password.result}"
  })
}

# Create additional databases using null_resource
# Note: In production, consider using a separate provisioner or migration tool
resource "null_resource" "create_databases" {
  count = var.create_additional_databases ? 1 : 0

  triggers = {
    db_instance_id = aws_db_instance.main.id
  }

  provisioner "local-exec" {
    command = <<-EOT
      echo "Additional databases (gearify_orders, gearify_payments, gearify_shipping) should be created manually or via migration scripts"
    EOT
  }

  depends_on = [aws_db_instance.main]
}
