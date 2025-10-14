#!/usr/bin/env bash
set -e

ENV_FILE=".env"

if [ ! -f "$ENV_FILE" ]; then
    echo "❌ .env file not found. Copy .env.template to .env and fill in values."
    exit 1
fi

REQUIRED_VARS=(
    "LOCALSTACK_API_KEY"
    "AWS_REGION"
    "POSTGRES_CONNECTION_STRING"
    "REDIS_URL"
)

MISSING=()

for VAR in "${REQUIRED_VARS[@]}"; do
    VALUE=$(grep "^${VAR}=" "$ENV_FILE" | cut -d '=' -f2-)
    if [ -z "$VALUE" ] || [ "$VALUE" = "your-pro-license-key-here" ]; then
        MISSING+=("$VAR")
    fi
done

if [ ${#MISSING[@]} -ne 0 ]; then
    echo "❌ Missing or placeholder values for:"
    for VAR in "${MISSING[@]}"; do
        echo "   - $VAR"
    done
    exit 1
fi

echo "✅ Environment variables validated"
