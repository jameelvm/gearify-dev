#!/bin/bash

# LocalStack AWS Resources Initialization - Orchestrator
# This script coordinates the initialization of all AWS resources
# by calling individual resource-specific scripts.

set -e

echo "=========================================="
echo "Initializing LocalStack AWS Resources"
echo "=========================================="
echo ""

# Configuration
SCRIPT_DIR="/etc/localstack/init/ready.d/scripts"
CONFIG_DIR="/etc/localstack/init/ready.d"

# Wait for LocalStack to be ready
echo "Waiting for LocalStack to be ready..."
until awslocal s3 ls 2>/dev/null; do
  echo "Waiting for LocalStack..."
  sleep 2
done
echo "LocalStack is ready!"
echo ""

# ==========================================
# Initialize DynamoDB Tables and Seed Data
# ==========================================
if [ -f "${SCRIPT_DIR}/init-dynamodb.sh" ]; then
  source "${SCRIPT_DIR}/init-dynamodb.sh"
else
  echo "WARNING: init-dynamodb.sh not found, skipping DynamoDB initialization"
fi

# ==========================================
# Initialize S3 Buckets
# ==========================================
if [ -f "${SCRIPT_DIR}/init-s3.sh" ]; then
  source "${SCRIPT_DIR}/init-s3.sh"
else
  echo "WARNING: init-s3.sh not found, skipping S3 initialization"
fi

# ==========================================
# Initialize SQS Queues
# ==========================================
if [ -f "${SCRIPT_DIR}/init-sqs.sh" ]; then
  source "${SCRIPT_DIR}/init-sqs.sh"
else
  echo "WARNING: init-sqs.sh not found, skipping SQS initialization"
fi

# ==========================================
# Initialize SNS Topics and Subscriptions
# ==========================================
if [ -f "${SCRIPT_DIR}/init-sns.sh" ]; then
  source "${SCRIPT_DIR}/init-sns.sh"
else
  echo "WARNING: init-sns.sh not found, skipping SNS initialization"
fi

# ==========================================
# Initialize Secrets Manager
# ==========================================
if [ -f "${SCRIPT_DIR}/init-secrets.sh" ]; then
  source "${SCRIPT_DIR}/init-secrets.sh"
else
  echo "WARNING: init-secrets.sh not found, skipping Secrets Manager initialization"
fi

# ==========================================
# Initialize SSM Parameter Store
# ==========================================
if [ -f "${SCRIPT_DIR}/init-ssm.sh" ]; then
  source "${SCRIPT_DIR}/init-ssm.sh"
else
  echo "WARNING: init-ssm.sh not found, skipping SSM initialization"
fi

# ==========================================
# Initialize OpenSearch Domain
# ==========================================
if [ -f "${SCRIPT_DIR}/init-opensearch.sh" ]; then
  source "${SCRIPT_DIR}/init-opensearch.sh"
else
  echo "WARNING: init-opensearch.sh not found, skipping OpenSearch initialization"
fi

# ==========================================
# Summary
# ==========================================
echo "=========================================="
echo "LocalStack initialization completed!"
echo "=========================================="
echo ""
echo "Resources created:"
echo "  - DynamoDB Tables: $(awslocal dynamodb list-tables --region us-east-1 --output text | wc -w)"
echo "  - S3 Buckets: $(awslocal s3 ls | wc -l)"
echo "  - SQS Queues: $(awslocal sqs list-queues --region us-east-1 --output text | wc -w)"
echo "  - SNS Topics: $(awslocal sns list-topics --region us-east-1 --output text | wc -w)"
echo "  - Secrets: 3"
echo "  - SSM Parameters: 3"
echo "  - OpenSearch Domains: 1"
echo ""
echo "Ready for development!"
echo "=========================================="
