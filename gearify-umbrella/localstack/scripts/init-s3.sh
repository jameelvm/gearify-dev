#!/bin/bash

# S3 Buckets Initialization
# This script creates all S3 buckets with CORS configuration

set -e

CONFIG_DIR="/etc/localstack/init/ready.d"

echo "=========================================="
echo "Creating S3 buckets..."
echo "=========================================="

# Product images bucket
echo "  - Creating bucket: gearify-product-images"
awslocal s3 mb s3://gearify-product-images --region us-east-1 2>/dev/null || echo "    Bucket already exists"
awslocal s3api put-bucket-cors \
  --bucket gearify-product-images \
  --cors-configuration file://${CONFIG_DIR}/s3/cors/product-images-cors.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to set CORS"

# Tenant assets bucket
echo "  - Creating bucket: gearify-tenant-assets"
awslocal s3 mb s3://gearify-tenant-assets --region us-east-1 2>/dev/null || echo "    Bucket already exists"
awslocal s3api put-bucket-cors \
  --bucket gearify-tenant-assets \
  --cors-configuration file://${CONFIG_DIR}/s3/cors/tenant-assets-cors.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to set CORS"

# Order documents bucket
echo "  - Creating bucket: gearify-order-documents"
awslocal s3 mb s3://gearify-order-documents --region us-east-1 2>/dev/null || echo "    Bucket already exists"

echo "S3 buckets created successfully!"
echo ""
