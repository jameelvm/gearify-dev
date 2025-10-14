#!/usr/bin/env bash

echo "Seeding Redis..."

docker exec gearify-redis redis-cli SETNX "feature:default:enable-checkout" "true"
docker exec gearify-redis redis-cli SETNX "feature:default:enable-paypal" "true"
docker exec gearify-redis redis-cli SETNX "feature:global-demo:enable-checkout" "true"
docker exec gearify-redis redis-cli SETNX "session:demo-session-id" '{"userId":"user-123","tenantId":"default"}'

echo "✓ Redis seeded"
