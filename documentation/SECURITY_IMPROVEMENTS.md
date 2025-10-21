# Security Improvements - Credential Management

## Summary

This document outlines the security improvements made to credential management in the Gearify microservices platform.

## Changes Made

### 1. Removed Hardcoded Credentials from Configuration Files

**Before:**
```json
{
  "AWS": {
    "Credentials": {
      "AccessKey": "test",
      "SecretKey": "test"
    }
  }
}
```

**After:**
```json
{
  "AWS": {
    "Region": "us-east-1",
    "DynamoDB": {
      "ServiceURL": "http://localhost:4566"
    }
  }
}
```

### 2. Implemented AWS Default Credential Provider Chain

**Before:**
```csharp
var credentials = new BasicAWSCredentials("test", "test");
services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(credentials, config));
```

**After:**
```csharp
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig { /* ... */ };

    // Try environment variables first (for local development)
    var accessKey = Configuration["AWS_ACCESS_KEY_ID"];
    var secretKey = Configuration["AWS_SECRET_ACCESS_KEY"];

    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
    {
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        return new AmazonDynamoDBClient(credentials, config);
    }

    // Production: Use IAM roles via default credential chain
    return new AmazonDynamoDBClient(config);
});
```

### 3. Updated Services

- ✅ **Catalog Service** (Startup.cs, appsettings.Development.json)
- ✅ **Cart Service** (Startup.cs)
- 📋 Other services follow the same pattern

### 4. Created Security Documentation

- **AWS_CREDENTIALS_BEST_PRACTICES.md** - Comprehensive guide on credential management
- **.env.example** - Template for local development environment variables
- Updated **.gitignore** - Enhanced to exclude sensitive files

## How It Works

### Local Development (with LocalStack)

Set environment variables:

```bash
# Option 1: Export in terminal
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

# Option 2: Use .env file (create from .env.example)
cp .env.example .env
# Edit .env with your values
```

### Production Deployment

**No credentials needed!** The application automatically uses:

1. **ECS/Fargate**: Task IAM Role
2. **EC2**: Instance Profile
3. **EKS**: Service Account with IRSA
4. **Lambda**: Execution Role

## Credential Provider Chain Order

The AWS SDK checks for credentials in this order:

1. Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
2. Shared credentials file (`~/.aws/credentials`)
3. IAM Instance Profile (EC2)
4. ECS Task Role
5. EKS Service Account

## Security Best Practices Applied

✅ **No credentials in source code**
✅ **No credentials in configuration files**
✅ **Environment variables for local development only**
✅ **IAM roles for production**
✅ **Secrets excluded from version control**
✅ **Factory pattern for dependency injection**
✅ **Least-privilege IAM policies** (see AWS_CREDENTIALS_BEST_PRACTICES.md)

## Migration Checklist

For developers moving from old pattern to new pattern:

- [ ] Remove credentials from appsettings.json files
- [ ] Create .env file from .env.example
- [ ] Set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY in .env
- [ ] Verify .env is in .gitignore
- [ ] Update Startup.cs to use factory pattern
- [ ] Test locally with LocalStack
- [ ] For production: Set up IAM roles instead of credentials

## Production Deployment

### ECS Task Definition Example

```json
{
  "family": "gearify-catalog-service",
  "taskRoleArn": "arn:aws:iam::123456789012:role/GearifyCatalogServiceRole",
  "executionRoleArn": "arn:aws:iam::123456789012:role/ecsTaskExecutionRole",
  "containerDefinitions": [
    {
      "name": "catalog-service",
      "image": "123456789012.dkr.ecr.us-east-1.amazonaws.com/gearify-catalog:latest",
      "environment": [
        {
          "name": "AWS__Region",
          "value": "us-east-1"
        },
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ]
    }
  ]
}
```

### IAM Role Policy Example

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:Query"
      ],
      "Resource": "arn:aws:dynamodb:us-east-1:123456789012:table/gearify-catalog-*"
    }
  ]
}
```

## Benefits

1. **Security**: Credentials never committed to source control
2. **Flexibility**: Easy to switch between local and production
3. **AWS Best Practices**: Uses IAM roles in production
4. **Auditability**: CloudTrail logs all API calls with IAM principal
5. **Rotation**: IAM role credentials auto-rotate
6. **Least Privilege**: Each service has its own IAM role with minimal permissions

## References

- [AWS SDK Credential Provider Chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html)
- [AWS Security Best Practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)
- [ECS Task IAM Roles](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-iam-roles.html)
- [ASP.NET Core Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
