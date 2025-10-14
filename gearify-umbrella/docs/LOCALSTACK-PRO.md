# LocalStack Pro Setup

## Overview
LocalStack Pro provides full AWS service emulation locally, including Cognito for authentication.

## Getting Started

### 1. Obtain License Key
- Sign up at https://app.localstack.cloud
- Copy your API key

### 2. Configure
Add to `.env`:
```bash
LOCALSTACK_API_KEY=your-key-here
```

### 3. Start Services
```bash
make up
```

## Available Services
- ✅ **Cognito**: Full authentication (User Pools, App Clients)
- ✅ **DynamoDB**: Complete feature parity
- ✅ **S3**: Event notifications, versioning
- ✅ **SQS/SNS**: Full messaging features
- ✅ **Secrets Manager**: Secret storage
- ✅ **Parameter Store**: Configuration management
- ✅ **Lambda**: Function execution

## Testing Cognito Locally

### Create User
```bash
awslocal cognito-idp admin-create-user \
  --user-pool-id <pool-id> \
  --username test@example.com \
  --temporary-password TempPass123!
```

### Login
```bash
awslocal cognito-idp initiate-auth \
  --auth-flow USER_PASSWORD_AUTH \
  --client-id <client-id> \
  --auth-parameters USERNAME=test@example.com,PASSWORD=Pass123!
```

## Useful Commands

### Check Service Status
```bash
make localstack-status
```

### List Resources
```bash
make aws-resources
```

### View Logs
```bash
make localstack-logs
```

## Pro-Specific Features

### 1. Persistent State
Data survives container restarts with volume mount.

### 2. Cloud Pods
Share environment snapshots with team:
```bash
localstack pod save my-env
localstack pod load my-env
```

### 3. Advanced IAM
Full policy simulation and enforcement.

## Troubleshooting

**Problem**: License validation fails
**Solution**: Verify API key and check internet connectivity

**Problem**: Services not initialized
**Solution**: Check init script logs in container

**Problem**: Performance issues
**Solution**: Allocate more resources to Docker Desktop
