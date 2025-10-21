# LocalStack Troubleshooting Guide

## Error: "Unable to get IAM security credentials from EC2 Instance Metadata Service"

This error means the AWS SDK is trying to use the default credential chain instead of LocalStack credentials.

### Quick Fixes

#### 1. Verify Environment is set to Development

```bash
# PowerShell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project gearify-catalog-svc

# Bash/Linux
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project gearify-catalog-svc
```

#### 2. Verify LocalStack is Running

```bash
# Check if LocalStack is accessible
curl http://localhost:4566/_localstack/health

# Expected output: JSON with service statuses
```

#### 3. Check appsettings.Development.json

Ensure the file contains:

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
    "Profile": "localstack",
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566"
  }
}
```

#### 4. Check Startup Diagnostic Output

When the service starts, you should see:

```
=== LocalStack Configuration ===
UseLocalStack: True
LocalStackHost: localhost:4566
AWS Region: us-east-1
Environment: Development
================================
```

If `UseLocalStack` shows `False`, the configuration isn't being loaded.

### Common Issues

#### Issue 1: Environment Not Set to Development

**Symptom:** Service runs but tries to use IAM credentials

**Solution:**
```bash
# Always set environment before running
$env:ASPNETCORE_ENVIRONMENT="Development"  # PowerShell
export ASPNETCORE_ENVIRONMENT=Development   # Bash
```

#### Issue 2: LocalStack Not Running

**Symptom:** Connection errors or timeouts

**Solution:**
```bash
# Start LocalStack with Docker
docker run --rm -it -p 4566:4566 localstack/localstack

# Or with docker-compose
docker-compose up localstack
```

#### Issue 3: Wrong Configuration File

**Symptom:** Configuration values are null or default

**Solution:**
- Ensure you're editing `appsettings.Development.json`, not `appsettings.json`
- Check the file is in the same directory as the .csproj file
- Verify the file is set to "Copy if newer" in project properties

#### Issue 4: Package Not Installed

**Symptom:** LocalStack.Client.Extensions not found errors

**Solution:**
```bash
dotnet add package LocalStack.Client.Extensions --version 1.4.0
```

### Verification Steps

1. **Check Configuration Loading:**

Add this to Startup.cs temporarily:
```csharp
Console.WriteLine($"UseLocalStack: {Configuration.GetValue<bool>("LocalStack:UseLocalStack")}");
```

2. **Test LocalStack Connectivity:**

```bash
# Create a test table in LocalStack
aws --endpoint-url=http://localhost:4566 dynamodb list-tables
```

3. **Enable Detailed Logging:**

Add to appsettings.Development.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "Amazon": "Debug",
      "LocalStack": "Debug"
    }
  }
}
```

### Alternative: Manual Configuration (Fallback)

If LocalStack.Client.Extensions doesn't work, use manual configuration:

```csharp
// In Startup.cs ConfigureServices
var useLocalStack = Configuration.GetValue<bool>("LocalStack:UseLocalStack");

if (useLocalStack)
{
    // Manual LocalStack configuration
    var credentials = new BasicAWSCredentials("test", "test");

    services.AddSingleton<IAmazonDynamoDB>(sp =>
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        };
        return new AmazonDynamoDBClient(credentials, config);
    });

    services.AddSingleton<IAmazonS3>(sp =>
    {
        var config = new AmazonS3Config
        {
            ServiceURL = "http://localhost:4566",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };
        return new AmazonS3Client(credentials, config);
    });
}
else
{
    // Production: Use IAM roles
    services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
    services.AddAWSService<IAmazonDynamoDB>();
    services.AddAWSService<IAmazonS3>();
}
```

### Docker Compose Example

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
      - DATA_DIR=/tmp/localstack/data
    volumes:
      - "./localstack-data:/tmp/localstack"

  catalog-service:
    build: ./gearify-catalog-svc
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
    depends_on:
      - localstack
```

### Testing LocalStack Integration

```bash
# 1. Start LocalStack
docker run --rm -p 4566:4566 localstack/localstack

# 2. Set environment
export ASPNETCORE_ENVIRONMENT=Development

# 3. Run service
dotnet run --project gearify-catalog-svc

# 4. Check logs for LocalStack configuration output

# 5. Test endpoint
curl http://localhost:5000/health
```

### If All Else Fails

Check these files exist and are correct:
1. `gearify-catalog-svc/appsettings.Development.json` - LocalStack config
2. `gearify-catalog-svc/Startup.cs` - AddLocalStack() call
3. `gearify-catalog-svc/Gearify.CatalogService.csproj` - Package reference

Run with verbose logging:
```bash
dotnet run --project gearify-catalog-svc --verbosity detailed
```

### Getting Help

If issue persists, provide:
1. Output of diagnostic console logs
2. Content of appsettings.Development.json
3. Value of ASPNETCORE_ENVIRONMENT
4. LocalStack health check result
5. Full error stack trace
