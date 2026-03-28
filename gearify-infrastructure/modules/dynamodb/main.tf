# DynamoDB Module for Gearify
# Creates DynamoDB tables for Catalog, Tenant, and other services

# Products Table
resource "aws_dynamodb_table" "products" {
  name         = "${var.project}-products-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "tenantId"
  range_key    = "productId"

  attribute {
    name = "tenantId"
    type = "S"
  }

  attribute {
    name = "productId"
    type = "S"
  }

  attribute {
    name = "category"
    type = "S"
  }

  attribute {
    name = "createdAt"
    type = "S"
  }

  attribute {
    name = "price"
    type = "N"
  }

  attribute {
    name = "name"
    type = "S"
  }

  # GSI for category queries
  global_secondary_index {
    name            = "CategoryIndex"
    hash_key        = "tenantId"
    range_key       = "category"
    projection_type = "ALL"
  }

  # GSI for date sorting
  global_secondary_index {
    name            = "CreatedAtIndex"
    hash_key        = "tenantId"
    range_key       = "createdAt"
    projection_type = "ALL"
  }

  # GSI for price sorting
  global_secondary_index {
    name            = "PriceIndex"
    hash_key        = "tenantId"
    range_key       = "price"
    projection_type = "ALL"
  }

  # GSI for name sorting
  global_secondary_index {
    name            = "NameIndex"
    hash_key        = "tenantId"
    range_key       = "name"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-products-${var.environment}"
    Environment = var.environment
  }
}

# Tenants Table
resource "aws_dynamodb_table" "tenants" {
  name         = "${var.project}-tenants-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "tenantId"

  attribute {
    name = "tenantId"
    type = "S"
  }

  attribute {
    name = "domain"
    type = "S"
  }

  global_secondary_index {
    name            = "DomainIndex"
    hash_key        = "domain"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-tenants-${var.environment}"
    Environment = var.environment
  }
}

# Users Table (for Auth service)
resource "aws_dynamodb_table" "users" {
  name         = "${var.project}-users-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "userId"

  attribute {
    name = "userId"
    type = "S"
  }

  attribute {
    name = "email"
    type = "S"
  }

  attribute {
    name = "tenantId"
    type = "S"
  }

  global_secondary_index {
    name            = "EmailIndex"
    hash_key        = "email"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "TenantIndex"
    hash_key        = "tenantId"
    range_key       = "userId"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-users-${var.environment}"
    Environment = var.environment
  }
}

# Feature Flags Table
resource "aws_dynamodb_table" "feature_flags" {
  name         = "${var.project}-feature-flags-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "tenantId"
  range_key    = "flagName"

  attribute {
    name = "tenantId"
    type = "S"
  }

  attribute {
    name = "flagName"
    type = "S"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-feature-flags-${var.environment}"
    Environment = var.environment
  }
}

# Inventory Table
resource "aws_dynamodb_table" "inventory" {
  name         = "${var.project}-inventory-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "tenantId"
  range_key    = "productId"

  attribute {
    name = "tenantId"
    type = "S"
  }

  attribute {
    name = "productId"
    type = "S"
  }

  attribute {
    name = "warehouseId"
    type = "S"
  }

  global_secondary_index {
    name            = "WarehouseIndex"
    hash_key        = "tenantId"
    range_key       = "warehouseId"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-inventory-${var.environment}"
    Environment = var.environment
  }
}

# Media Table
resource "aws_dynamodb_table" "media" {
  name         = "${var.project}-media-${var.environment}"
  billing_mode = var.billing_mode
  hash_key     = "tenantId"
  range_key    = "mediaId"

  attribute {
    name = "tenantId"
    type = "S"
  }

  attribute {
    name = "mediaId"
    type = "S"
  }

  attribute {
    name = "productId"
    type = "S"
  }

  global_secondary_index {
    name            = "ProductMediaIndex"
    hash_key        = "tenantId"
    range_key       = "productId"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = var.point_in_time_recovery
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = "${var.project}-media-${var.environment}"
    Environment = var.environment
  }
}

# IAM Policy for DynamoDB access
resource "aws_iam_policy" "dynamodb_access" {
  name        = "${var.project}-${var.environment}-dynamodb-access-policy"
  description = "IAM policy for DynamoDB table access"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "DynamoDBAccess"
        Effect = "Allow"
        Action = [
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:BatchGetItem",
          "dynamodb:BatchWriteItem",
          "dynamodb:DescribeTable"
        ]
        Resource = [
          aws_dynamodb_table.products.arn,
          "${aws_dynamodb_table.products.arn}/index/*",
          aws_dynamodb_table.tenants.arn,
          "${aws_dynamodb_table.tenants.arn}/index/*",
          aws_dynamodb_table.users.arn,
          "${aws_dynamodb_table.users.arn}/index/*",
          aws_dynamodb_table.feature_flags.arn,
          aws_dynamodb_table.inventory.arn,
          "${aws_dynamodb_table.inventory.arn}/index/*",
          aws_dynamodb_table.media.arn,
          "${aws_dynamodb_table.media.arn}/index/*"
        ]
      }
    ]
  })
}
