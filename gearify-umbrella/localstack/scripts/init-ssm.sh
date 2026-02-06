#!/bin/bash

# SSM Parameter Store Initialization
# This script creates all SSM parameters

set -e

echo "=========================================="
echo "Creating SSM parameters..."
echo "=========================================="

echo "  - Creating parameter: /gearify/config/feature-flags"
awslocal ssm put-parameter \
  --name "/gearify/config/feature-flags" \
  --description "Global feature flags configuration" \
  --value '{"enableNewCheckout":false,"enableProductReviews":true,"enableWishlist":true,"enableLiveChat":false,"maintenanceMode":false}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "    Parameter /gearify/config/feature-flags already exists"

echo "  - Creating parameter: /gearify/config/api-rate-limits"
awslocal ssm put-parameter \
  --name "/gearify/config/api-rate-limits" \
  --description "API rate limiting configuration" \
  --value '{"requestsPerMinute":100,"requestsPerHour":5000,"burstSize":20,"enableRateLimiting":true}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "    Parameter /gearify/config/api-rate-limits already exists"

echo "  - Creating parameter: /gearify/config/cache-settings"
awslocal ssm put-parameter \
  --name "/gearify/config/cache-settings" \
  --description "Caching configuration for services" \
  --value '{"productCacheTTL":300,"categoryCacheTTL":600,"userSessionTTL":3600,"enableDistributedCache":true,"cacheProvider":"Redis"}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "    Parameter /gearify/config/cache-settings already exists"

echo "SSM parameters created successfully!"
echo ""
