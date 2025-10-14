.PHONY: help clone-all pull-all status-all build-all build-parallel up down restart logs ps seed seed-clean validate-env test-e2e test-load shell-tenant shell-catalog shell-order shell-payment shell-shipping shell-notification shell-gateway shell-web localstack-status localstack-logs aws-resources cognito-login clean

help:
	@echo "Gearify Umbrella - Available targets:"
	@echo "  clone-all          - Clone all service repositories"
	@echo "  pull-all           - Pull latest changes for all repos"
	@echo "  status-all         - Show git status for all repos"
	@echo "  build-all          - Build all Docker images"
	@echo "  build-parallel     - Build all images in parallel"
	@echo "  up                 - Start all services"
	@echo "  down               - Stop all services"
	@echo "  restart            - Restart all services"
	@echo "  logs               - Tail logs from all services"
	@echo "  ps                 - Show running containers"
	@echo "  seed               - Seed databases with initial data"
	@echo "  seed-clean         - Wipe and re-seed all data"
	@echo "  validate-env       - Check required env vars"
	@echo "  test-e2e           - Run Playwright E2E tests"
	@echo "  test-load          - Run k6 load tests"
	@echo "  shell-<service>    - Open shell in service container"
	@echo "  localstack-status  - Check LocalStack services"
	@echo "  localstack-logs    - View LocalStack logs"
	@echo "  aws-resources      - List LocalStack AWS resources"
	@echo "  cognito-login      - Test Cognito authentication"
	@echo "  clean              - Remove all containers, volumes"

clone-all:
	@./scripts/clone-all.sh

pull-all:
	@for dir in ../gearify-*; do \
		if [ -d "$$dir/.git" ]; then \
			echo "Pulling $$dir..."; \
			(cd $$dir && git pull); \
		fi \
	done

status-all:
	@for dir in ../gearify-*; do \
		if [ -d "$$dir/.git" ]; then \
			echo "=== $$dir ==="; \
			(cd $$dir && git status -s); \
		fi \
	done

build-all:
	docker compose build

build-parallel:
	docker compose build --parallel

up:
	docker compose up -d

down:
	docker compose down

restart:
	docker compose restart

logs:
	docker compose logs -f

ps:
	docker compose ps

seed: validate-env
	@./scripts/seed.sh

seed-clean:
	@echo "Wiping data..."
	docker compose down -v
	docker compose up -d localstack postgres redis
	@sleep 10
	@./scripts/seed.sh

validate-env:
	@./scripts/validate-env.sh

test-e2e:
	cd tests/e2e && npx playwright test

test-load:
	cd tests/load && k6 run checkout-flow.js

shell-tenant:
	docker exec -it gearify-tenant-svc /bin/bash

shell-catalog:
	docker exec -it gearify-catalog-svc /bin/bash

shell-order:
	docker exec -it gearify-order-svc /bin/bash

shell-payment:
	docker exec -it gearify-payment-svc /bin/bash

shell-shipping:
	docker exec -it gearify-shipping-svc /bin/bash

shell-notification:
	docker exec -it gearify-notification-svc /bin/bash

shell-gateway:
	docker exec -it gearify-api-gateway /bin/bash

shell-web:
	docker exec -it gearify-web /bin/sh

localstack-status:
	docker exec gearify-localstack localstack status services

localstack-logs:
	docker logs -f gearify-localstack

aws-resources:
	@echo "=== DynamoDB Tables ==="
	@aws --endpoint-url=http://localhost:4566 dynamodb list-tables
	@echo "\n=== S3 Buckets ==="
	@aws --endpoint-url=http://localhost:4566 s3 ls
	@echo "\n=== SQS Queues ==="
	@aws --endpoint-url=http://localhost:4566 sqs list-queues
	@echo "\n=== SNS Topics ==="
	@aws --endpoint-url=http://localhost:4566 sns list-topics
	@echo "\n=== Cognito User Pools ==="
	@aws --endpoint-url=http://localhost:4566 cognito-idp list-user-pools --max-results 10

cognito-login:
	@echo "Testing Cognito login..."
	@aws --endpoint-url=http://localhost:4566 cognito-idp initiate-auth \
		--auth-flow USER_PASSWORD_AUTH \
		--client-id $$(grep COGNITO_CLIENT_ID .env | cut -d '=' -f2) \
		--auth-parameters USERNAME=admin@gearify.com,PASSWORD=Admin123!

clean:
	docker compose down -v --remove-orphans
	docker system prune -f
