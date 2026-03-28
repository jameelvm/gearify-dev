# ECR Module for Gearify
# Creates container registries for all microservices

locals {
  # All Gearify service repositories
  repositories = toset([
    "gearify-tenant-svc",
    "gearify-catalog-svc",
    "gearify-auth-svc",
    "gearify-search-svc",
    "gearify-cart-svc",
    "gearify-order-svc",
    "gearify-payment-svc",
    "gearify-shipping-svc",
    "gearify-inventory-svc",
    "gearify-media-svc",
    "gearify-notification-svc",
    "gearify-api-gateway",
    "gearify-web"
  ])
}

# ECR Repositories
resource "aws_ecr_repository" "repos" {
  for_each = local.repositories

  name                 = each.value
  image_tag_mutability = var.image_tag_mutability

  image_scanning_configuration {
    scan_on_push = var.scan_on_push
  }

  encryption_configuration {
    encryption_type = "AES256"
  }

  tags = {
    Name    = each.value
    Project = var.project
  }
}

# Lifecycle Policy for all repositories
resource "aws_ecr_lifecycle_policy" "repos" {
  for_each = aws_ecr_repository.repos

  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last ${var.keep_image_count} images"
        selection = {
          tagStatus     = "tagged"
          tagPrefixList = ["v", "dev-", "staging-", "prod-"]
          countType     = "imageCountMoreThan"
          countNumber   = var.keep_image_count
        }
        action = {
          type = "expire"
        }
      },
      {
        rulePriority = 2
        description  = "Delete untagged images older than ${var.untagged_image_days} days"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = var.untagged_image_days
        }
        action = {
          type = "expire"
        }
      },
      {
        rulePriority = 3
        description  = "Delete feature branch images older than ${var.feature_branch_days} days"
        selection = {
          tagStatus     = "tagged"
          tagPrefixList = ["feature-", "fix-", "feat-"]
          countType     = "sinceImagePushed"
          countUnit     = "days"
          countNumber   = var.feature_branch_days
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

# Repository Policy (optional - for cross-account access)
resource "aws_ecr_repository_policy" "repos" {
  for_each = var.enable_cross_account_access ? aws_ecr_repository.repos : {}

  repository = each.value.name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowPull"
        Effect    = "Allow"
        Principal = {
          AWS = var.cross_account_arns
        }
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability"
        ]
      }
    ]
  })
}

# IAM Policy for CI/CD to push images
resource "aws_iam_policy" "ecr_push" {
  name        = "${var.project}-ecr-push-policy"
  description = "IAM policy for pushing images to ECR"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "GetAuthorizationToken"
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken"
        ]
        Resource = "*"
      },
      {
        Sid    = "PushPullImages"
        Effect = "Allow"
        Action = [
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload",
          "ecr:DescribeRepositories",
          "ecr:DescribeImages",
          "ecr:ListImages"
        ]
        Resource = [for repo in aws_ecr_repository.repos : repo.arn]
      }
    ]
  })
}

# IAM Policy for EKS nodes to pull images
resource "aws_iam_policy" "ecr_pull" {
  name        = "${var.project}-ecr-pull-policy"
  description = "IAM policy for pulling images from ECR"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "GetAuthorizationToken"
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken"
        ]
        Resource = "*"
      },
      {
        Sid    = "PullImages"
        Effect = "Allow"
        Action = [
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:DescribeRepositories",
          "ecr:DescribeImages",
          "ecr:ListImages"
        ]
        Resource = [for repo in aws_ecr_repository.repos : repo.arn]
      }
    ]
  })
}
