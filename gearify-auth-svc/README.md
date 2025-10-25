# Gearify Auth Service

The Authentication and Authorization microservice for the Gearify e-commerce platform.

## Features

- User registration with email/password
- User login with JWT token generation
- Refresh token mechanism (7-day expiry)
- Password management (change password)
- Profile management
- BCrypt password hashing (12 salt rounds)
- Multi-tenancy support
- DynamoDB for user storage

## Architecture

This service follows Clean Architecture principles with the following layers:

- **Domain**: Entities and domain events
- **Application**: Commands, queries, and validators (CQRS with MediatR)
- **Infrastructure**: Repositories, services, and external integrations
- **API**: Controllers and DTOs

## Technologies

- .NET 8
- ASP.NET Core Web API
- MediatR (CQRS)
- FluentValidation
- JWT Bearer Authentication
- BCrypt.Net-Next
- AWS DynamoDB
- LocalStack (for local development)
- OpenTelemetry
- Serilog
- Swagger/OpenAPI

## API Endpoints

### Auth Controller (`/api/auth`)

- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login and get JWT tokens
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/me` - Get current user info (requires authentication)

### User Controller (`/api/users`)

- `PUT /api/users/profile` - Update user profile (requires authentication)
- `POST /api/users/change-password` - Change password (requires authentication)

## JWT Configuration

JWT tokens include the following claims:
- `sub`: User ID
- `email`: User email
- `tenantId`: Tenant identifier
- `role`: User role (Customer, Admin, Manager)
- `firstName`: User first name
- `lastName`: User last name

**Token Expiry:**
- Access Token: 15 minutes
- Refresh Token: 7 days

## DynamoDB Schema

### Table: gearify-users

**Primary Key:**
- PK: `TENANT#{tenantId}`
- SK: `USER#{userId}`

**GSI1 (Email Index):**
- GSI1PK: `TENANT#{tenantId}#EMAIL#{email}`
- GSI1SK: `USER#{userId}`

**GSI2 (Refresh Token Index):**
- GSI2PK: `TENANT#{tenantId}`
- GSI2SK: `REFRESH#{refreshToken}`

## Password Requirements

- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number

## Running Locally

### Prerequisites

- .NET 8 SDK
- Docker Desktop (for LocalStack)
- LocalStack running on `localhost:4566`

### Setup

1. Ensure LocalStack is running with DynamoDB
2. Create the DynamoDB table:

```bash
aws dynamodb create-table \
    --table-name gearify-users \
    --attribute-definitions \
        AttributeName=PK,AttributeType=S \
        AttributeName=SK,AttributeType=S \
        AttributeName=GSI1PK,AttributeType=S \
        AttributeName=GSI1SK,AttributeType=S \
        AttributeName=GSI2PK,AttributeType=S \
        AttributeName=GSI2SK,AttributeType=S \
    --key-schema \
        AttributeName=PK,KeyType=HASH \
        AttributeName=SK,KeyType=RANGE \
    --global-secondary-indexes \
        "[
            {
                \"IndexName\": \"GSI1\",
                \"KeySchema\": [{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],
                \"Projection\":{\"ProjectionType\":\"ALL\"},
                \"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}
            },
            {
                \"IndexName\": \"GSI2\",
                \"KeySchema\": [{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],
                \"Projection\":{\"ProjectionType\":\"ALL\"},
                \"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}
            }
        ]" \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 \
    --endpoint-url http://localhost:4566
```

3. Run the service:

```bash
cd gearify-auth-svc
dotnet run
```

4. Access Swagger UI: `http://localhost:5002/swagger`

## Configuration

### appsettings.json

```json
{
  "JwtSettings": {
    "Secret": "your-secret-key-here",
    "Issuer": "gearify-auth",
    "Audience": "gearify-api",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `SEQ_URL`: Seq logging endpoint
- `OTLP_ENDPOINT`: OpenTelemetry collector endpoint

## Security

- Passwords are hashed using BCrypt with 12 salt rounds
- JWT tokens are signed with HMAC-SHA256
- Refresh tokens are cryptographically secure random values
- All endpoints (except register/login) require authentication
- Multi-tenancy ensures data isolation

## Health Check

`GET /health` - Returns service health status

## License

Copyright © 2024 Gearify Team
