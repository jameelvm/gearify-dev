#!/usr/bin/env bash
set -e

echo "🌱 Seeding databases..."

# Seed DynamoDB
echo "Seeding DynamoDB..."
node scripts/seed/seed-dynamodb.js

# Seed PostgreSQL
echo "Seeding PostgreSQL..."
docker exec -i gearify-postgres psql -U postgres -d gearify < scripts/seed/seed-postgres.sql

# Seed Redis
echo "Seeding Redis..."
bash scripts/seed/seed-redis.sh

echo "✅ Seeding complete!"
