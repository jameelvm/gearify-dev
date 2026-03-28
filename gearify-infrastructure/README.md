# Gearify Infrastructure

Terraform/Terragrunt infrastructure as code for the Gearify microservices platform on AWS.

## Prerequisites

- [Terraform](https://www.terraform.io/downloads) >= 1.5.0
- [Terragrunt](https://terragrunt.gruntwork.io/docs/getting-started/install/) >= 0.50.0
- [AWS CLI](https://aws.amazon.com/cli/) configured with appropriate credentials
- AWS account with sufficient permissions

## Repository Structure

```
gearify-infrastructure/
├── terragrunt.hcl                    # Root configuration
├── modules/                          # Reusable Terraform modules
│   ├── vpc/                         # VPC with subnets, NAT, endpoints
│   ├── eks/                         # EKS cluster with node groups, IRSA
│   ├── rds/                         # PostgreSQL database
│   ├── elasticache/                 # Redis cluster
│   ├── sns-sqs/                     # Event-driven messaging
│   ├── ecr/                         # Container registries
│   ├── s3/                          # Object storage
│   ├── dynamodb/                    # NoSQL tables
│   └── secrets-manager/             # Secrets management
├── environments/
│   ├── _envcommon/                  # Shared configuration
│   └── dev/us-east-1/               # Dev environment
│       ├── vpc/
│       ├── eks/
│       ├── rds/
│       ├── elasticache/
│       ├── sns-sqs/
│       ├── dynamodb/
│       ├── s3/
│       └── secrets/
└── global/                          # Global resources
    └── ecr/                         # ECR repositories (shared)
```

## Quick Start

### 1. Configure AWS Credentials

```bash
aws configure
# Or export credentials
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_REGION="us-east-1"
```

### 2. Create Terraform State Backend

Before running Terragrunt, create the S3 bucket and DynamoDB table for state management:

```bash
# Create S3 bucket for state
aws s3 mb s3://gearify-terraform-state-$(aws sts get-caller-identity --query Account --output text)

# Enable versioning
aws s3api put-bucket-versioning \
  --bucket gearify-terraform-state-$(aws sts get-caller-identity --query Account --output text) \
  --versioning-configuration Status=Enabled

# Create DynamoDB table for locking
aws dynamodb create-table \
  --table-name gearify-terraform-locks \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST
```

### 3. Deploy Global Resources (ECR)

```bash
cd global/ecr
terragrunt apply
```

### 4. Deploy Dev Environment

Deploy in order (VPC first, then EKS, then data stores):

```bash
cd environments/dev/us-east-1

# Deploy VPC
cd vpc && terragrunt apply && cd ..

# Deploy EKS
cd eks && terragrunt apply && cd ..

# Deploy data stores (can run in parallel)
cd rds && terragrunt apply && cd ..
cd elasticache && terragrunt apply && cd ..
cd dynamodb && terragrunt apply && cd ..
cd s3 && terragrunt apply && cd ..

# Deploy messaging
cd sns-sqs && terragrunt apply && cd ..

# Deploy secrets
cd secrets && terragrunt apply && cd ..
```

Or deploy everything at once:

```bash
cd environments/dev/us-east-1
terragrunt run-all apply
```

### 5. Configure kubectl

```bash
aws eks update-kubeconfig --name gearify-dev --region us-east-1
```

## Module Descriptions

### VPC Module
Creates a VPC with:
- 3 Availability Zones
- Public, private, and database subnets
- NAT Gateway (1 for dev, 3 for prod)
- VPC endpoints for S3, DynamoDB, ECR, Secrets Manager, STS

### EKS Module
Creates an EKS cluster with:
- Managed node groups
- IRSA (IAM Roles for Service Accounts)
- AWS Load Balancer Controller IAM role
- External Secrets Operator IAM role
- EBS CSI Driver

### RDS Module
Creates PostgreSQL database with:
- Single instance (dev) or Multi-AZ (prod)
- Secrets stored in AWS Secrets Manager
- Automated backups

### ElastiCache Module
Creates Redis cluster with:
- Single node (dev) or cluster mode (prod)
- Optional TLS encryption
- Credentials in Secrets Manager

### SNS/SQS Module
Creates event-driven messaging infrastructure:
- 6 SNS topics (order, payment, shipping, media, catalog)
- 13+ SQS queues with filter policies
- Dead letter queues

### ECR Module
Creates container registries for all 13 services with:
- Lifecycle policies for image cleanup
- Image scanning on push

## Environment Variables

Each environment has its own `env.hcl` with settings:

| Variable | Dev | Staging | Prod |
|----------|-----|---------|------|
| VPC CIDR | 10.0.0.0/16 | 10.1.0.0/16 | 10.2.0.0/16 |
| NAT Gateways | 1 | 1 | 3 |
| EKS Nodes | t3.medium (2-5) | t3.large (2-8) | m5.large (3-15) |
| RDS Instance | db.t3.medium | db.t3.large | db.r5.large |
| RDS Multi-AZ | No | No | Yes |
| Redis Node | cache.t3.micro | cache.t3.small | cache.r5.large |

## Outputs

After deployment, key outputs include:

- `vpc_id` - VPC identifier
- `cluster_endpoint` - EKS API endpoint
- `db_instance_endpoint` - RDS connection endpoint
- `redis_endpoint` - Redis connection endpoint
- `repository_urls` - ECR repository URLs

Access outputs:

```bash
cd environments/dev/us-east-1/eks
terragrunt output
```

## Destroying Resources

```bash
# Destroy specific module
cd environments/dev/us-east-1/eks
terragrunt destroy

# Destroy all (use with caution!)
cd environments/dev/us-east-1
terragrunt run-all destroy
```

## Troubleshooting

### State Lock Issues
```bash
# Force unlock (use with caution)
terragrunt force-unlock LOCK_ID
```

### EKS Access Issues
```bash
# Update kubeconfig
aws eks update-kubeconfig --name gearify-dev --region us-east-1

# Check cluster status
aws eks describe-cluster --name gearify-dev --region us-east-1
```

### Module Changes Not Detected
```bash
# Clear cache
rm -rf .terragrunt-cache/
terragrunt apply
```

## Security Notes

- All resources are deployed in private subnets where applicable
- Secrets are stored in AWS Secrets Manager (never in code)
- Encryption at rest enabled for RDS, ElastiCache, S3, DynamoDB
- Security groups follow least-privilege principle
- IRSA used for pod-level IAM permissions
