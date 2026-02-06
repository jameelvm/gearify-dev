#!/bin/bash

# OpenSearch Domain Initialization
# This script creates the OpenSearch domain

set -e

echo "=========================================="
echo "Creating OpenSearch domain..."
echo "=========================================="

echo "  - Creating domain: gearify-search"
awslocal opensearch create-domain \
  --domain-name gearify-search \
  --engine-version OpenSearch_2.5 \
  --cluster-config InstanceType=t3.small.search,InstanceCount=1 \
  --ebs-options EBSEnabled=true,VolumeType=gp2,VolumeSize=10 \
  --region us-east-1 \
  2>/dev/null || echo "    OpenSearch domain gearify-search already exists"

echo "OpenSearch domain created successfully!"
echo ""
