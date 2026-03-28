# S3 Module for Gearify
# Creates S3 buckets for media storage

data "aws_caller_identity" "current" {}

locals {
  buckets = {
    "product-images" = {
      description = "Product images and thumbnails"
      cors        = true
      versioning  = true
    }
    "tenant-assets" = {
      description = "Tenant-specific assets (logos, branding)"
      cors        = true
      versioning  = true
    }
    "order-documents" = {
      description = "Order documents (invoices, receipts)"
      cors        = false
      versioning  = true
    }
  }
}

# S3 Buckets
resource "aws_s3_bucket" "buckets" {
  for_each = local.buckets

  bucket = "${var.project}-${each.key}-${var.environment}-${data.aws_caller_identity.current.account_id}"

  tags = {
    Name        = "${var.project}-${each.key}-${var.environment}"
    Environment = var.environment
    Description = each.value.description
  }
}

# Block public access
resource "aws_s3_bucket_public_access_block" "buckets" {
  for_each = aws_s3_bucket.buckets

  bucket = each.value.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Versioning
resource "aws_s3_bucket_versioning" "buckets" {
  for_each = { for k, v in local.buckets : k => v if v.versioning }

  bucket = aws_s3_bucket.buckets[each.key].id

  versioning_configuration {
    status = "Enabled"
  }
}

# Server-side encryption
resource "aws_s3_bucket_server_side_encryption_configuration" "buckets" {
  for_each = aws_s3_bucket.buckets

  bucket = each.value.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

# CORS configuration for buckets that need it
resource "aws_s3_bucket_cors_configuration" "buckets" {
  for_each = { for k, v in local.buckets : k => v if v.cors }

  bucket = aws_s3_bucket.buckets[each.key].id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "PUT", "POST", "DELETE", "HEAD"]
    allowed_origins = var.cors_allowed_origins
    expose_headers  = ["ETag", "Content-Length", "Content-Type"]
    max_age_seconds = 3600
  }
}

# Lifecycle rules for cost optimization
resource "aws_s3_bucket_lifecycle_configuration" "buckets" {
  for_each = aws_s3_bucket.buckets

  bucket = each.value.id

  rule {
    id     = "transition-to-ia"
    status = "Enabled"

    transition {
      days          = 90
      storage_class = "STANDARD_IA"
    }

    noncurrent_version_transition {
      noncurrent_days = 30
      storage_class   = "STANDARD_IA"
    }

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }

  rule {
    id     = "abort-incomplete-uploads"
    status = "Enabled"

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

# IAM Policy for S3 access
resource "aws_iam_policy" "s3_access" {
  name        = "${var.project}-${var.environment}-s3-access-policy"
  description = "IAM policy for S3 bucket access"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ListBuckets"
        Effect = "Allow"
        Action = [
          "s3:ListBucket",
          "s3:GetBucketLocation"
        ]
        Resource = [for bucket in aws_s3_bucket.buckets : bucket.arn]
      },
      {
        Sid    = "ObjectAccess"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:GetObjectVersion",
          "s3:DeleteObjectVersion"
        ]
        Resource = [for bucket in aws_s3_bucket.buckets : "${bucket.arn}/*"]
      }
    ]
  })
}
