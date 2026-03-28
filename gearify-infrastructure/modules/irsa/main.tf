# IRSA Module for Gearify
# Creates IAM roles for Kubernetes service accounts

data "aws_caller_identity" "current" {}

# Service-specific IAM roles
locals {
  services = {
    "catalog-svc" = {
      policies = ["dynamodb", "sns", "s3-read"]
    }
    "tenant-svc" = {
      policies = ["dynamodb", "s3-read"]
    }
    "auth-svc" = {
      policies = ["dynamodb", "secrets"]
    }
    "search-svc" = {
      policies = ["sqs", "opensearch"]
    }
    "cart-svc" = {
      policies = []  # Only needs Redis (handled via secrets)
    }
    "order-svc" = {
      policies = ["sns", "sqs", "secrets"]
    }
    "payment-svc" = {
      policies = ["sns", "sqs", "secrets"]
    }
    "shipping-svc" = {
      policies = ["sns", "sqs", "secrets"]
    }
    "inventory-svc" = {
      policies = ["dynamodb", "sqs"]
    }
    "media-svc" = {
      policies = ["s3", "dynamodb", "sns", "sqs"]
    }
    "notification-svc" = {
      policies = ["sqs", "ses", "secrets"]
    }
  }
}

# DynamoDB Policy
resource "aws_iam_policy" "dynamodb" {
  name        = "${var.project}-${var.environment}-dynamodb-policy"
  description = "DynamoDB access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:BatchGetItem",
          "dynamodb:BatchWriteItem"
        ]
        Resource = [
          "arn:aws:dynamodb:${var.aws_region}:${data.aws_caller_identity.current.account_id}:table/${var.project}-*-${var.environment}",
          "arn:aws:dynamodb:${var.aws_region}:${data.aws_caller_identity.current.account_id}:table/${var.project}-*-${var.environment}/index/*"
        ]
      }
    ]
  })
}

# SNS Policy
resource "aws_iam_policy" "sns" {
  name        = "${var.project}-${var.environment}-sns-policy"
  description = "SNS access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sns:Publish",
          "sns:GetTopicAttributes"
        ]
        Resource = "arn:aws:sns:${var.aws_region}:${data.aws_caller_identity.current.account_id}:${var.project}-*-${var.environment}"
      }
    ]
  })
}

# SQS Policy
resource "aws_iam_policy" "sqs" {
  name        = "${var.project}-${var.environment}-sqs-policy"
  description = "SQS access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:GetQueueUrl",
          "sqs:ChangeMessageVisibility"
        ]
        Resource = "arn:aws:sqs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:${var.project}-*-${var.environment}"
      }
    ]
  })
}

# S3 Full Policy
resource "aws_iam_policy" "s3" {
  name        = "${var.project}-${var.environment}-s3-policy"
  description = "S3 full access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:ListBucket"
        ]
        Resource = [
          "arn:aws:s3:::${var.project}-*-${var.environment}-*",
          "arn:aws:s3:::${var.project}-*-${var.environment}-*/*"
        ]
      }
    ]
  })
}

# S3 Read-Only Policy
resource "aws_iam_policy" "s3_read" {
  name        = "${var.project}-${var.environment}-s3-read-policy"
  description = "S3 read access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:ListBucket"
        ]
        Resource = [
          "arn:aws:s3:::${var.project}-*-${var.environment}-*",
          "arn:aws:s3:::${var.project}-*-${var.environment}-*/*"
        ]
      }
    ]
  })
}

# Secrets Manager Policy
resource "aws_iam_policy" "secrets" {
  name        = "${var.project}-${var.environment}-secrets-policy"
  description = "Secrets Manager access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = "arn:aws:secretsmanager:${var.aws_region}:${data.aws_caller_identity.current.account_id}:secret:${var.project}/${var.environment}/*"
      }
    ]
  })
}

# OpenSearch Policy
resource "aws_iam_policy" "opensearch" {
  name        = "${var.project}-${var.environment}-opensearch-policy"
  description = "OpenSearch access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "es:ESHttpGet",
          "es:ESHttpPost",
          "es:ESHttpPut",
          "es:ESHttpDelete",
          "es:ESHttpHead"
        ]
        Resource = "arn:aws:es:${var.aws_region}:${data.aws_caller_identity.current.account_id}:domain/${var.project}-${var.environment}/*"
      }
    ]
  })
}

# SES Policy
resource "aws_iam_policy" "ses" {
  name        = "${var.project}-${var.environment}-ses-policy"
  description = "SES access for Gearify services"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ses:SendEmail",
          "ses:SendRawEmail"
        ]
        Resource = "*"
      }
    ]
  })
}

# Policy mapping
locals {
  policy_arns = {
    "dynamodb"   = aws_iam_policy.dynamodb.arn
    "sns"        = aws_iam_policy.sns.arn
    "sqs"        = aws_iam_policy.sqs.arn
    "s3"         = aws_iam_policy.s3.arn
    "s3-read"    = aws_iam_policy.s3_read.arn
    "secrets"    = aws_iam_policy.secrets.arn
    "opensearch" = aws_iam_policy.opensearch.arn
    "ses"        = aws_iam_policy.ses.arn
  }
}

# IAM Roles for each service
resource "aws_iam_role" "service_roles" {
  for_each = local.services

  name = "${var.project}-${var.environment}-${each.key}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRoleWithWebIdentity"
      Effect = "Allow"
      Principal = {
        Federated = var.oidc_provider_arn
      }
      Condition = {
        StringEquals = {
          "${replace(var.oidc_provider_url, "https://", "")}:sub" = "system:serviceaccount:${var.namespace}:${each.key}"
          "${replace(var.oidc_provider_url, "https://", "")}:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })

  tags = {
    Name        = "${var.project}-${var.environment}-${each.key}-role"
    Service     = each.key
    Environment = var.environment
  }
}

# Attach policies to roles
resource "aws_iam_role_policy_attachment" "service_policies" {
  for_each = {
    for pair in flatten([
      for service, config in local.services : [
        for policy in config.policies : {
          key    = "${service}-${policy}"
          role   = service
          policy = policy
        }
      ]
    ]) : pair.key => pair
  }

  role       = aws_iam_role.service_roles[each.value.role].name
  policy_arn = local.policy_arns[each.value.policy]
}
