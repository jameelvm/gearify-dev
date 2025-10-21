# LocalStack Configuration Guide

## Overview

This project uses the **LocalStack.Client.Extensions** NuGet package for simplified LocalStack integration in development environments. This approach allows you to keep LocalStack-specific credentials in `appsettings.Development.json` while maintaining production security best practices.

## Why LocalStack.Client.Extensions?

### Benefits

✅ **Development Credentials in Config**: Safe to store "test" credentials in appsettings.Development.json (only used with LocalStack)
✅ **Clean Code**: No manual credential handling in Startup.cs
✅ **Automatic Configuration**: Extension methods handle all AWS service setup
✅ **Environment Separation**: LocalStack only used when `UseLocalStack: true`
✅ **Production Ready**: Production uses IAM roles automatically (no LocalStack)

### vs. Manual Configuration

**Before (Manual):**
```csharp
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var credentials = new BasicAWSCredentials(
        Configuration["AWS_ACCESS_KEY_ID"],
        Configuration["AWS_SECRET_ACCESS_KEY"]
    );
    var config = new AmazonDynamoDBConfig
    {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "us-east-1"
    };
    return new AmazonDynamoDBClient(credentials, config);
});
```

**After (LocalStack Extensions):**
```csharp
// Automatic configuration based on appsettings
services.AddLocalStack(Configuration);
services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
services.AddAWSService<IAmazonDynamoDB>();
services.AddAWSService<IAmazonS3>();
```

## Configuration

### appsettings.Development.json

```json
{
  "LocalStack": {
    "UseLocalStack": true,
    "Session": {
      "AwsAccessKeyId": "test",
      "AwsAccessKey": "test",
      "AwsSecretAccessKey": "test"
    },
    "Config": {
      "LocalStackHost": "localhost:4566"
    }
  },
  "AWS": {
    "Region": "us-east-1"
  }
}
```

### appsettings.Production.json

```json
{
  "LocalStack": {
    "UseLocalStack": false
  },
  "AWS": {
    "Region": "us-east-1"
  }
}
```

## Startup.cs Implementation

```csharp
using LocalStack.Client.Extensions;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // LocalStack Configuration (Development only)
        // Automatically configures AWS services to use LocalStack when enabled
        services.AddLocalStack(Configuration);

        // AWS Service Configuration
        // Uses LocalStack in development, IAM roles in production
        services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonSNS>();
        services.AddAWSService<IAmazonSecretsManager>();
    }
}
```

## How It Works

### Development Environment

1. **UseLocalStack = true** in appsettings.Development.json
2. LocalStack.Client.Extensions intercepts AWS SDK calls
3. All AWS services automatically point to `localhost:4566`
4. Uses "test" credentials from configuration
5. No environment variables needed

### Production Environment

1. **UseLocalStack = false** (or omitted) in appsettings.Production.json
2. AWS SDK uses default credential provider chain
3. Credentials sourced from IAM roles:
   - ECS Task Role
   - EC2 Instance Profile
   - EKS Service Account (IRSA)
4. Services connect to real AWS endpoints

## Package Installation

```bash
# For each service
dotnet add package LocalStack.Client.Extensions --version 1.4.0
```

## Security Considerations

### Why "test" Credentials are Safe in Development Config

1. **Only Works with LocalStack**: These credentials only function when pointing to LocalStack
2. **LocalStack Ignores Auth**: LocalStack doesn't validate credentials (accepts any value)
3. **No Real AWS Access**: Cannot be used against real AWS services
4. **Development Only**: Production config has `UseLocalStack: false`
5. **Not Committed**: While safe, you can still exclude from source control if preferred

### Production Security

- **No credentials in configuration** (production)
- **IAM roles only** (managed by AWS)
- **Automatic credential rotation**
- **CloudTrail audit logging**
- **Least-privilege policies**

## Supported AWS Services

The LocalStack.Client.Extensions package supports:

- ✅ DynamoDB
- ✅ S3
- ✅ SQS
- ✅ SNS
- ✅ Lambda
- ✅ Secrets Manager
- ✅ CloudWatch
- ✅ And more...

## Environment Variable Override (Optional)

You can still use environment variables if preferred:

```bash
# .env file (for docker-compose)
LOCALSTACK__USELOCALSTACK=true
LOCALSTACK__SESSION__AWSACCESSKEYID=test
LOCALSTACK__SESSION__AWSSECRETACCESSKEY=test
LOCALSTACK__CONFIG__LOCALSTACKHOST=localhost:4566
```

Environment variables override appsettings values following .NET configuration hierarchy.

## Docker Compose Example

```yaml
version: '3.8'

services:
  localstack:
    image: localstack/localstack:latest
    ports:
      - "4566:4566"
    environment:
      - SERVICES=dynamodb,s3,sqs,sns,secretsmanager
      - DEBUG=1
    volumes:
      - "./localstack-data:/var/lib/localstack"

  catalog-service:
    build: ./gearify-catalog-svc
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      # LocalStack config comes from appsettings.Development.json
    depends_on:
      - localstack
```

## Comparison: LocalStack Extensions vs. Environment Variables

| Aspect | LocalStack.Client.Extensions | Environment Variables |
|--------|------------------------------|----------------------|
| **Config Location** | appsettings.Development.json | .env or docker-compose.yml |
| **Code Complexity** | Minimal (3 lines) | Moderate (manual factory) |
| **Credential Safety** | Safe (LocalStack only) | Safe (if not committed) |
| **Production** | Automatic IAM roles | Automatic IAM roles |
| **Developer Experience** | Excellent (no env setup) | Good (requires .env file) |
| **Best For** | Team projects, consistency | CI/CD, containerized dev |

## Recommendation

### Use LocalStack.Client.Extensions When:
- ✅ Working in a team environment
- ✅ Want simple, consistent configuration
- ✅ Prefer appsettings over environment variables
- ✅ Need automatic LocalStack integration

### Use Environment Variables When:
- ✅ Running in containers (Docker/Kubernetes)
- ✅ CI/CD pipelines
- ✅ Highly sensitive about any credentials in files
- ✅ Need runtime configuration flexibility

## Migration from Manual Configuration

### Before (Manual Factory Pattern):
```csharp
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig { ServiceURL = "..." };
    var credentials = new BasicAWSCredentials("test", "test");
    return new AmazonDynamoDBClient(credentials, config);
});
```

### After (LocalStack Extensions):
```csharp
services.AddLocalStack(Configuration);
services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
services.AddAWSService<IAmazonDynamoDB>();
```

## Troubleshooting

### LocalStack not connecting
- Verify `UseLocalStack: true` in appsettings.Development.json
- Check LocalStack is running: `curl http://localhost:4566/_localstack/health`
- Ensure `LocalStackHost` is correct (usually `localhost:4566`)

### Production using LocalStack
- Set `UseLocalStack: false` in appsettings.Production.json
- Verify `ASPNETCORE_ENVIRONMENT=Production`
- Check IAM role is attached to compute resource

### Service not registered
- Ensure `AddAWSService<T>()` called for each service
- Verify package references are correct
- Check using statements include `LocalStack.Client.Extensions`

## References

- [LocalStack.Client NuGet](https://www.nuget.org/packages/LocalStack.Client/)
- [LocalStack.Client.Extensions NuGet](https://www.nuget.org/packages/LocalStack.Client.Extensions/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
