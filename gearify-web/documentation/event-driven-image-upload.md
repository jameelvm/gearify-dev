# Event-Driven Product Image Upload Architecture

## Overview

This document describes the implementation of an event-driven, scalable image upload system for product creation in the Gearify admin workflow.

**Status:** 📋 **Planned** - Not yet implemented
**Priority:** 🟡 **Medium** - Performance optimization
**Estimated Time:** 3-4 hours
**Use Case:** Admin bulk product creation with multiple images

---

## Problem Statement

### Current Implementation Issues

**File:** `gearify-catalog-svc/Application/Commands/UploadProductImagesCommandHandler.cs`

```csharp
// ❌ CURRENT: Sequential HTTP calls to Media Service
for (int i = 0; i < request.Images.Count; i++)
{
    var image = request.Images[i];
    await _mediaServiceClient.UploadProductImageAsync(...);
    // Blocks and waits for each upload
}
```

### Performance Problems

**Scenario: Admin uploads product with 20 images**

| Metric | Current Performance | Issue |
|--------|-------------------|--------|
| Response Time | ~4000ms (20 × 200ms) | ❌ Admin waits 4 seconds |
| API Calls | 20 sequential HTTP requests | ❌ Network overhead |
| Scalability | Poor (blocks thread) | ❌ Can't scale workers |
| Fault Tolerance | None (no retry) | ❌ One failure = all fail |
| User Experience | Blocking UI | ❌ Can't create more products |

### Business Impact

- ⏱️ **Slow admin workflow** - Admin waits for each product
- 📉 **Poor scalability** - Can't handle bulk imports
- 🐛 **No fault tolerance** - Failed uploads lose data
- 💰 **Wasted resources** - Threads blocked waiting

---

## Proposed Solution: Event-Driven Architecture

### Key Principles

✅ **Eventual Consistency** - Acceptable for admin workflows
✅ **Asynchronous Processing** - Don't block the admin
✅ **Horizontal Scalability** - Add workers as needed
✅ **Fault Tolerance** - Automatic retries with DLQ
✅ **Decoupled Services** - Catalog doesn't depend on Media

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────┐
│ 1. ADMIN: Create Product + Upload 20 Images             │
└─────────────────────────┬────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────┐
│ 2. CATALOG SERVICE                                       │
│    ✓ Save product metadata (status: "Processing")       │
│    ✓ Save images to temp storage (S3)                   │
│    ✓ Publish event to SNS                               │
│    ✓ Return success IMMEDIATELY (~50ms)                 │
└─────────────────────────┬────────────────────────────────┘
                          ↓
                ┌──────────────────────┐
                │ 3. SNS TOPIC         │
                │ product-image-upload │
                │ -requests            │
                └──────────┬───────────┘
                           ↓
                ┌──────────────────────┐
                │ 4. SQS QUEUE         │
                │ product-image-       │
                │ uploads              │
                └──────────┬───────────┘
                           ↓
┌──────────────────────────────────────────────────────────┐
│ 5. IMAGE UPLOAD WORKER (Background Service)             │
│    ✓ Poll SQS for jobs (long polling)                   │
│    ✓ Download images from temp storage                  │
│    ✓ Upload to Media Service (PARALLEL)                 │
│    ✓ Update product status: "Ready"                     │
│    ✓ Delete from temp storage                           │
│    ✓ Delete SQS message                                 │
└──────────────────────────────────────────────────────────┘
                           ↓
                 ┌─────────────────┐
                 │ 6. PRODUCT READY│
                 │    Admin sees   │
                 │    "Ready ✓"    │
                 └─────────────────┘
```

### User Experience Flow

```
Admin Action                          System Response
─────────────────────────────────────────────────────────
1. Fill product form
2. Select 20 images
3. Click "Create Product"
                                      → Save metadata (10ms)
                                      → Upload to temp storage (40ms)
                                      → Publish event (5ms)
4. ✅ See success message (50ms)      ← Immediate response!
5. Continue creating more products
                                      → Background: Upload images (20s)
6. Auto-refresh status
   "Images uploading... (15/20)"      ← Progress indicator
7. Final status: "Ready ✓"           ← All done!
```

---

## Implementation Guide

### Phase 1: Update Catalog Service

#### 1.1: Create Event DTO

**File:** `gearify-catalog-svc/Application/Events/ProductImagesUploadRequestedEvent.cs`

```csharp
namespace Gearify.CatalogService.Application.Events;

/// <summary>
/// Event published when product images need to be uploaded to Media Service
/// This triggers background processing via SQS worker
/// </summary>
public record ProductImagesUploadRequestedEvent
{
    /// <summary>
    /// Unique job identifier for tracking
    /// </summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Product ID that images belong to
    /// </summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// S3 keys for images in temporary storage
    /// Format: temp/product-images/{guid}/{filename}
    /// </summary>
    public List<string> TemporaryImageKeys { get; init; } = new();

    /// <summary>
    /// Optional alt texts for images (accessibility)
    /// Index-matched with TemporaryImageKeys
    /// </summary>
    public List<string>? AltTexts { get; init; }

    /// <summary>
    /// User who requested the upload
    /// </summary>
    public string RequestedBy { get; init; } = string.Empty;

    /// <summary>
    /// When the upload was requested
    /// </summary>
    public DateTime RequestedAt { get; init; }

    /// <summary>
    /// Original file names (for logging/debugging)
    /// </summary>
    public List<string> OriginalFileNames { get; init; } = new();

    /// <summary>
    /// Total size in bytes (for monitoring)
    /// </summary>
    public long TotalSizeBytes { get; init; }
}
```

#### 1.2: Create Temporary Storage Service

**File:** `gearify-catalog-svc/Infrastructure/Storage/ITemporaryStorage.cs`

```csharp
namespace Gearify.CatalogService.Infrastructure.Storage;

/// <summary>
/// Temporary storage for images before processing
/// Uses S3 with lifecycle policy to auto-delete after 7 days
/// </summary>
public interface ITemporaryStorage
{
    /// <summary>
    /// Save image to temporary storage
    /// </summary>
    Task<string> SaveAsync(string key, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve image from temporary storage
    /// </summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete image from temporary storage
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate unique key for temporary storage
    /// Format: temp/product-images/{guid}/{filename}
    /// </summary>
    string GenerateKey(string fileName);
}
```

**File:** `gearify-catalog-svc/Infrastructure/Storage/S3TemporaryStorage.cs`

```csharp
public class S3TemporaryStorage : ITemporaryStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3TemporaryStorage> _logger;

    private const string BucketName = "gearify-temp-uploads";

    public S3TemporaryStorage(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<S3TemporaryStorage> logger)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateKey(string fileName)
    {
        var uniqueId = Guid.NewGuid().ToString("N");
        var sanitizedName = Path.GetFileName(fileName);
        return $"temp/product-images/{uniqueId}/{sanitizedName}";
    }

    public async Task<string> SaveAsync(
        string key,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = BucketName,
                Key = key,
                InputStream = stream,
                ContentType = GetContentType(key),
                // Auto-delete after 7 days
                TagSet = new List<Tag>
                {
                    new Tag { Key = "Lifecycle", Value = "Temporary" }
                }
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            _logger.LogInformation("Saved image to temp storage: {Key}", key);
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving to temp storage: {Key}", key);
            throw;
        }
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = BucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);

            // Copy to memory stream to avoid S3 stream disposal issues
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting from temp storage: {Key}", key);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(BucketName, key, cancellationToken);
            _logger.LogDebug("Deleted from temp storage: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting from temp storage: {Key}", key);
            // Don't throw - cleanup is best-effort
        }
    }

    private string GetContentType(string key)
    {
        var extension = Path.GetExtension(key).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
```

#### 1.3: Update Command Handler

**File:** `gearify-catalog-svc/Application/Commands/UploadProductImagesCommandHandler.cs`

```csharp
public class UploadProductImagesCommandHandler :
    IRequestHandler<UploadProductImagesCommand, UploadProductImagesResult>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITemporaryStorage _tempStorage;
    private readonly ILogger<UploadProductImagesCommandHandler> _logger;

    public async Task<UploadProductImagesResult> Handle(
        UploadProductImagesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Validate product exists
            var product = await _productRepository.GetByIdAsync(
                request.ProductId,
                _tenantContext.TenantId);

            if (product == null)
            {
                return new UploadProductImagesResult(
                    Success: false,
                    ErrorMessage: "Product not found");
            }

            // 2. Validate images
            if (request.Images == null || !request.Images.Any())
            {
                return new UploadProductImagesResult(
                    Success: false,
                    ErrorMessage: "No images provided");
            }

            // 3. Save images to temporary storage (parallel for speed)
            var tempImageKeys = new List<string>();
            var originalFileNames = new List<string>();
            long totalSize = 0;

            var uploadTasks = request.Images.Select(async image =>
            {
                var tempKey = _tempStorage.GenerateKey(image.FileName);
                using var stream = image.OpenReadStream();
                await _tempStorage.SaveAsync(tempKey, stream, cancellationToken);

                return new
                {
                    TempKey = tempKey,
                    FileName = image.FileName,
                    Size = image.Length
                };
            });

            var uploadResults = await Task.WhenAll(uploadTasks);

            foreach (var result in uploadResults)
            {
                tempImageKeys.Add(result.TempKey);
                originalFileNames.Add(result.FileName);
                totalSize += result.Size;
            }

            // 4. Update product status to "Processing"
            product.ImageUploadStatus = "Processing";
            product.TotalImages = request.Images.Count;
            product.ProcessedImages = 0;
            product.ImageUploadJobId = Guid.NewGuid().ToString();

            await _productRepository.UpdateAsync(product);

            // 5. Publish event to SNS - triggers background processing
            var uploadEvent = new ProductImagesUploadRequestedEvent
            {
                JobId = product.ImageUploadJobId,
                ProductId = request.ProductId,
                TenantId = _tenantContext.TenantId,
                TemporaryImageKeys = tempImageKeys,
                AltTexts = request.AltTexts,
                RequestedBy = "admin", // TODO: Get from auth context
                RequestedAt = DateTime.UtcNow,
                OriginalFileNames = originalFileNames,
                TotalSizeBytes = totalSize
            };

            await _eventPublisher.PublishAsync(
                uploadEvent,
                "gearify-product-image-upload-requests",
                cancellationToken);

            _logger.LogInformation(
                "Published image upload event for product {ProductId}, Job {JobId}, {ImageCount} images",
                request.ProductId,
                uploadEvent.JobId,
                request.Images.Count);

            // 6. Return immediately - don't wait for processing!
            return new UploadProductImagesResult(
                Success: true,
                JobId: uploadEvent.JobId,
                Message: $"Uploading {request.Images.Count} images in the background",
                UploadedImages: null); // No URLs yet - processing in background
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating image upload for product {ProductId}", request.ProductId);
            return new UploadProductImagesResult(
                Success: false,
                ErrorMessage: "Failed to initiate image upload");
        }
    }
}
```

#### 1.4: Update Product Entity

**File:** `gearify-catalog-svc/Domain/Entities/Product.cs`

```csharp
public class Product
{
    // ... existing fields ...

    /// <summary>
    /// Image upload status for eventual consistency
    /// </summary>
    public string ImageUploadStatus { get; set; } = "Pending";
    // Values: Pending, Processing, Ready, PartiallyFailed, Failed

    /// <summary>
    /// Job ID for tracking background upload
    /// </summary>
    public string? ImageUploadJobId { get; set; }

    /// <summary>
    /// Total number of images being uploaded
    /// </summary>
    public int TotalImages { get; set; } = 0;

    /// <summary>
    /// Number of images successfully processed
    /// </summary>
    public int ProcessedImages { get; set; } = 0;

    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? ImageUploadError { get; set; }

    /// <summary>
    /// When the upload was last updated
    /// </summary>
    public DateTime? ImageUploadUpdatedAt { get; set; }

    /// <summary>
    /// URLs for uploaded images
    /// </summary>
    public List<ProductImageUrls>? Images { get; set; }
}

public class ProductImageUrls
{
    public string MediaId { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public string Large { get; set; } = string.Empty;
    public string Medium { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? AltText { get; set; }
}
```

---

### Phase 2: Create Background Worker

#### 2.1: Queue Interface

**File:** `gearify-catalog-svc/Application/BackgroundJobs/IImageUploadQueue.cs`

```csharp
namespace Gearify.CatalogService.Application.BackgroundJobs;

/// <summary>
/// Queue for image upload jobs (SQS implementation)
/// </summary>
public interface IImageUploadQueue
{
    Task<List<QueueMessage<ProductImagesUploadRequestedEvent>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(
        string receiptHandle,
        CancellationToken cancellationToken = default);

    Task ReturnMessageAsync(
        string receiptHandle,
        int visibilityTimeoutSeconds = 300,
        CancellationToken cancellationToken = default);
}

public class QueueMessage<T>
{
    public T Body { get; set; } = default!;
    public string ReceiptHandle { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}
```

#### 2.2: SQS Implementation

**File:** `gearify-catalog-svc/Infrastructure/Messaging/SqsImageUploadQueue.cs`

```csharp
public class SqsImageUploadQueue : IImageUploadQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsImageUploadQueue> _logger;
    private string? _queueUrl;

    public async Task<List<QueueMessage<ProductImagesUploadRequestedEvent>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(cancellationToken);

        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitTimeSeconds,
            MessageAttributeNames = new List<string> { "All" }
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

        return response.Messages.Select(msg =>
        {
            // SNS wraps the message
            var messageBody = ExtractSnsMessage(msg.Body);
            var uploadEvent = JsonSerializer.Deserialize<ProductImagesUploadRequestedEvent>(messageBody);

            return new QueueMessage<ProductImagesUploadRequestedEvent>
            {
                Body = uploadEvent!,
                ReceiptHandle = msg.ReceiptHandle,
                MessageId = msg.MessageId
            };
        }).ToList();
    }

    // DeleteMessageAsync and ReturnMessageAsync similar to media service
    // ... (implementation omitted for brevity)

    private async Task<string> GetQueueUrlAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_queueUrl))
            return _queueUrl;

        var queueName = "gearify-product-image-uploads";
        var response = await _sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
        _queueUrl = response.QueueUrl;
        return _queueUrl;
    }

    private string ExtractSnsMessage(string messageBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageBody);
            if (doc.RootElement.TryGetProperty("Message", out var messageElement))
            {
                return messageElement.GetString() ?? messageBody;
            }
            return messageBody;
        }
        catch
        {
            return messageBody;
        }
    }
}
```

#### 2.3: Background Worker Service

**File:** `gearify-catalog-svc/Application/BackgroundJobs/ImageUploadWorker.cs`

```csharp
/// <summary>
/// Background service that processes product image uploads
/// Polls SQS for upload jobs and processes them asynchronously
/// Scalable: Can run multiple instances for horizontal scaling
/// </summary>
public class ImageUploadWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImageUploadWorker> _logger;

    public ImageUploadWorker(
        IServiceProvider serviceProvider,
        ILogger<ImageUploadWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image Upload Worker started");

        // Wait a bit for services to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUploadJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Image Upload Worker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Image Upload Worker stopped");
    }

    private async Task ProcessUploadJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IImageUploadQueue>();
        var mediaClient = scope.ServiceProvider.GetRequiredService<IMediaServiceClient>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var tempStorage = scope.ServiceProvider.GetRequiredService<ITemporaryStorage>();

        // Long polling - waits up to 20 seconds for messages
        var messages = await queue.ReceiveMessagesAsync(
            maxMessages: 5,  // Process up to 5 products concurrently
            waitTimeSeconds: 20,
            cancellationToken: cancellationToken);

        if (!messages.Any())
            return;

        _logger.LogInformation("Received {Count} upload jobs from queue", messages.Count);

        // Process all jobs concurrently
        var processingTasks = messages.Select(msg =>
            ProcessSingleUploadJobAsync(
                msg,
                queue,
                mediaClient,
                productRepo,
                tempStorage,
                cancellationToken));

        await Task.WhenAll(processingTasks);
    }

    private async Task ProcessSingleUploadJobAsync(
        QueueMessage<ProductImagesUploadRequestedEvent> queueMessage,
        IImageUploadQueue queue,
        IMediaServiceClient mediaClient,
        IProductRepository productRepo,
        ITemporaryStorage tempStorage,
        CancellationToken cancellationToken)
    {
        var job = queueMessage.Body;
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Processing upload job {JobId} for product {ProductId}: {ImageCount} images, {TotalSize} bytes",
                job.JobId,
                job.ProductId,
                job.TemporaryImageKeys.Count,
                job.TotalSizeBytes);

            // Upload all images to Media Service in PARALLEL
            var uploadTasks = job.TemporaryImageKeys.Select(async (tempKey, index) =>
            {
                try
                {
                    // 1. Download from temp storage
                    var imageStream = await tempStorage.GetAsync(tempKey, cancellationToken);
                    var fileName = job.OriginalFileNames.ElementAtOrDefault(index)
                        ?? Path.GetFileName(tempKey);
                    var contentType = GetContentType(fileName);
                    var altText = job.AltTexts?.ElementAtOrDefault(index);

                    // 2. Upload to Media Service
                    var result = await mediaClient.UploadProductImageAsync(
                        imageStream: imageStream,
                        fileName: fileName,
                        contentType: contentType,
                        sizeInBytes: imageStream.Length,
                        productId: job.ProductId,
                        displayOrder: index,
                        altText: altText,
                        cancellationToken: cancellationToken);

                    // 3. Cleanup temp storage
                    await tempStorage.DeleteAsync(tempKey, cancellationToken);

                    _logger.LogDebug(
                        "Uploaded image {Index}/{Total} for job {JobId}: {FileName}",
                        index + 1,
                        job.TemporaryImageKeys.Count,
                        job.JobId,
                        fileName);

                    return new { Success = true, Result = result, Index = index };
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to upload image {TempKey} for job {JobId}",
                        tempKey,
                        job.JobId);

                    return new { Success = false, Result = (MediaUploadResponse?)null, Index = index };
                }
            });

            // Wait for all uploads to complete
            var uploadResults = await Task.WhenAll(uploadTasks);

            // Count successes/failures
            var successCount = uploadResults.Count(r => r.Success);
            var failureCount = uploadResults.Length - successCount;

            // Update product with results
            var product = await productRepo.GetByIdAsync(job.ProductId, job.TenantId);

            if (product != null)
            {
                // Determine final status
                if (successCount == job.TemporaryImageKeys.Count)
                {
                    product.ImageUploadStatus = "Ready";
                }
                else if (successCount > 0)
                {
                    product.ImageUploadStatus = "PartiallyFailed";
                    product.ImageUploadError = $"{failureCount} of {job.TemporaryImageKeys.Count} images failed to upload";
                }
                else
                {
                    product.ImageUploadStatus = "Failed";
                    product.ImageUploadError = "All images failed to upload";
                }

                product.ProcessedImages = successCount;
                product.ImageUploadUpdatedAt = DateTime.UtcNow;

                // Store image URLs
                product.Images = uploadResults
                    .Where(r => r.Success && r.Result != null)
                    .Select(r => new ProductImageUrls
                    {
                        MediaId = r.Result!.MediaId,
                        Original = r.Result.Urls?.Original ?? "",
                        Large = r.Result.Urls?.Large ?? "",
                        Medium = r.Result.Urls?.Medium ?? "",
                        Thumbnail = r.Result.Urls?.Thumbnail ?? "",
                        DisplayOrder = r.Index,
                        AltText = job.AltTexts?.ElementAtOrDefault(r.Index)
                    })
                    .OrderBy(img => img.DisplayOrder)
                    .ToList();

                await productRepo.UpdateAsync(product);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Completed upload job {JobId} in {Duration}ms: {SuccessCount}/{TotalCount} images uploaded, status: {Status}",
                    job.JobId,
                    duration.TotalMilliseconds,
                    successCount,
                    job.TemporaryImageKeys.Count,
                    product.ImageUploadStatus);
            }
            else
            {
                _logger.LogWarning(
                    "Product {ProductId} not found for job {JobId} - unable to update status",
                    job.ProductId,
                    job.JobId);
            }

            // Delete message from queue (success)
            await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing upload job {JobId} for product {ProductId}",
                job.JobId,
                job.ProductId);

            // Update product status to failed
            try
            {
                var product = await productRepo.GetByIdAsync(job.ProductId, job.TenantId);
                if (product != null)
                {
                    product.ImageUploadStatus = "Failed";
                    product.ImageUploadError = ex.Message;
                    product.ImageUploadUpdatedAt = DateTime.UtcNow;
                    await productRepo.UpdateAsync(product);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update product status for job {JobId}", job.JobId);
            }

            // Return message to queue for retry (max 3 times, then DLQ)
            await queue.ReturnMessageAsync(
                queueMessage.ReceiptHandle,
                visibilityTimeoutSeconds: 300,  // Wait 5 minutes before retry
                cancellationToken);
        }
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
```

---

### Phase 3: Infrastructure Setup

#### 3.1: Create S3 Bucket for Temp Storage

**File:** `gearify-umbrella/localstack/init-aws.sh`

Add to S3 section:

```bash
# Temporary upload bucket (auto-deletes after 7 days)
echo "  - Creating bucket: gearify-temp-uploads"
awslocal s3 mb s3://gearify-temp-uploads --region us-east-1 2>/dev/null || echo "    Bucket already exists"

# Set lifecycle policy for auto-cleanup
awslocal s3api put-bucket-lifecycle-configuration \
  --bucket gearify-temp-uploads \
  --lifecycle-configuration file://${CONFIG_DIR}/s3/lifecycle/temp-uploads-lifecycle.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to set lifecycle policy"
```

**File:** `gearify-umbrella/localstack/s3/lifecycle/temp-uploads-lifecycle.json`

```json
{
  "Rules": [
    {
      "Id": "DeleteTempFilesAfter7Days",
      "Status": "Enabled",
      "Prefix": "temp/",
      "Expiration": {
        "Days": 7
      }
    }
  ]
}
```

#### 3.2: Create SNS Topic

**File:** `gearify-umbrella/localstack/sns/topics/product-image-upload-requests.json`

```json
{
  "TopicName": "gearify-product-image-upload-requests",
  "DisplayName": "Product Image Upload Requests",
  "Attributes": {
    "DeliveryPolicy": "{\"http\":{\"defaultHealthyRetryPolicy\":{\"minDelayTarget\":20,\"maxDelayTarget\":20,\"numRetries\":3}}}",
    "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":\"*\"},\"Action\":\"SNS:Publish\",\"Resource\":\"*\"}]}"
  },
  "Tags": {
    "Service": "CatalogService",
    "Environment": "Development",
    "Purpose": "ProductImageUpload"
  },
  "Subscriptions": [
    {
      "Protocol": "sqs",
      "Endpoint": "arn:aws:sqs:us-east-1:000000000000:gearify-product-image-uploads"
    }
  ]
}
```

#### 3.3: Create SQS Queue with DLQ

**File:** `gearify-umbrella/localstack/sqs/queues/product-image-uploads.json`

```json
{
  "QueueName": "gearify-product-image-uploads",
  "Attributes": {
    "DelaySeconds": "0",
    "MessageRetentionPeriod": "1209600",
    "ReceiveMessageWaitTimeSeconds": "20",
    "VisibilityTimeout": "600",
    "RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:000000000000:gearify-product-image-uploads-dlq\",\"maxReceiveCount\":3}"
  },
  "Tags": {
    "Service": "CatalogService",
    "Environment": "Development",
    "Purpose": "ProductImageUpload"
  }
}
```

**File:** `gearify-umbrella/localstack/sqs/queues/product-image-uploads-dlq.json`

```json
{
  "QueueName": "gearify-product-image-uploads-dlq",
  "Attributes": {
    "MessageRetentionPeriod": "1209600"
  },
  "Tags": {
    "Service": "CatalogService",
    "Type": "DeadLetterQueue"
  }
}
```

#### 3.4: Update init-aws.sh

Add queue and topic creation:

```bash
# Product image upload queue
echo "  - Creating queue: gearify-product-image-uploads"
awslocal sqs create-queue --queue-name gearify-product-image-uploads --region us-east-1 2>/dev/null || echo "    Queue already exists"

# DLQ
echo "  - Creating DLQ: gearify-product-image-uploads-dlq"
awslocal sqs create-queue --queue-name gearify-product-image-uploads-dlq --region us-east-1 2>/dev/null || echo "    DLQ already exists"

# SNS Topic
PRODUCT_IMAGE_TOPIC_ARN=$(awslocal sns create-topic --name gearify-product-image-upload-requests --region us-east-1 --output text 2>/dev/null || echo "")
echo "  - Created topic: gearify-product-image-upload-requests"

# Subscribe SQS to SNS
if [ ! -z "$PRODUCT_IMAGE_TOPIC_ARN" ]; then
  QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-product-image-uploads --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PRODUCT_IMAGE_TOPIC_ARN --protocol sqs --notification-endpoint $QUEUE_ARN --region us-east-1 2>/dev/null
    echo "  - Subscribed gearify-product-image-uploads to topic"
  fi
fi
```

---

### Phase 4: Register Services in Startup

**File:** `gearify-catalog-svc/Startup.cs`

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... existing services ...

    // Temporary storage for images
    services.AddScoped<ITemporaryStorage, S3TemporaryStorage>();

    // Event publisher (SNS)
    services.AddScoped<IEventPublisher, SnsEventPublisher>();

    // Image upload queue (SQS)
    services.AddScoped<IImageUploadQueue, SqsImageUploadQueue>();

    // Background worker for processing uploads
    services.AddHostedService<ImageUploadWorker>();

    // AWS SQS client
    services.AddSingleton<IAmazonSQS>(sp =>
    {
        var endpoint = Environment.GetEnvironmentVariable("SQS_ENDPOINT")
            ?? "http://localhost:4566";
        var config = new AmazonSQSConfig
        {
            ServiceURL = endpoint
        };
        var credentials = new BasicAWSCredentials("test", "test");
        return new AmazonSQSClient(credentials, config);
    });
}
```

---

## Testing Strategy

### Unit Tests

```csharp
[Test]
public async Task UploadProductImages_ShouldPublishEvent_AndReturnImmediately()
{
    // Arrange
    var command = new UploadProductImagesCommand(
        ProductId: "prod-123",
        Images: GetMockImages(5),
        AltTexts: null);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await _handler.Handle(command, CancellationToken.None);
    stopwatch.Stop();

    // Assert
    result.Success.Should().BeTrue();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should be fast!
    _eventPublisher.Verify(x => x.PublishAsync(
        It.IsAny<ProductImagesUploadRequestedEvent>(),
        "gearify-product-image-upload-requests",
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Tests

```csharp
[Test]
public async Task EndToEnd_UploadImages_ShouldCompleteInBackground()
{
    // 1. Upload product with images
    var result = await _catalogClient.UploadProductImagesAsync("prod-123", GetMockImages(10));
    result.Success.Should().BeTrue();

    // 2. Product should be in "Processing" state
    var product = await _catalogClient.GetProductAsync("prod-123");
    product.ImageUploadStatus.Should().Be("Processing");

    // 3. Wait for background processing
    await Task.Delay(TimeSpan.FromSeconds(30));

    // 4. Product should now be "Ready"
    product = await _catalogClient.GetProductAsync("prod-123");
    product.ImageUploadStatus.Should().Be("Ready");
    product.ProcessedImages.Should().Be(10);
    product.Images.Should().HaveCount(10);
}
```

### Load Tests

```csharp
[Test]
public async Task LoadTest_100Products_20ImagesEach_ShouldHandleLoad()
{
    var tasks = Enumerable.Range(1, 100).Select(async i =>
    {
        var productId = $"prod-{i:000}";
        return await _catalogClient.UploadProductImagesAsync(
            productId,
            GetMockImages(20));
    });

    // All should return quickly
    var stopwatch = Stopwatch.StartNew();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // 100 products × 20 images = 2000 images
    // Should return in < 10 seconds (not wait for uploads)
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
}
```

---

## Monitoring & Observability

### Metrics to Track

```csharp
// Prometheus metrics
public class ImageUploadMetrics
{
    // Jobs
    private static readonly Counter JobsReceived = Metrics.CreateCounter(
        "product_image_upload_jobs_received_total",
        "Total number of upload jobs received");

    private static readonly Counter JobsCompleted = Metrics.CreateCounter(
        "product_image_upload_jobs_completed_total",
        "Total number of upload jobs completed",
        new CounterConfiguration { LabelNames = new[] { "status" } });

    // Images
    private static readonly Counter ImagesUploaded = Metrics.CreateCounter(
        "product_images_uploaded_total",
        "Total number of images uploaded",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    // Performance
    private static readonly Histogram JobDuration = Metrics.CreateHistogram(
        "product_image_upload_job_duration_seconds",
        "Duration of upload jobs in seconds");

    private static readonly Gauge ActiveJobs = Metrics.CreateGauge(
        "product_image_upload_jobs_active",
        "Number of currently active upload jobs");

    // Queue
    private static readonly Gauge QueueDepth = Metrics.CreateGauge(
        "product_image_upload_queue_depth",
        "Number of messages in upload queue");
}
```

### Logging

```csharp
// Structured logging examples
_logger.LogInformation(
    "Upload job started: JobId={JobId}, ProductId={ProductId}, ImageCount={ImageCount}, TotalSize={TotalSizeBytes}",
    job.JobId, job.ProductId, job.TemporaryImageKeys.Count, job.TotalSizeBytes);

_logger.LogInformation(
    "Upload job completed: JobId={JobId}, Duration={DurationMs}ms, Success={SuccessCount}/{TotalCount}, Status={Status}",
    job.JobId, duration.TotalMilliseconds, successCount, totalCount, product.ImageUploadStatus);

_logger.LogError(
    ex,
    "Upload job failed: JobId={JobId}, ProductId={ProductId}, Error={Error}",
    job.JobId, job.ProductId, ex.Message);
```

### Dashboards

**Grafana Dashboard Panels:**

1. **Job Throughput**
   - Jobs received per minute
   - Jobs completed per minute
   - Success rate

2. **Image Processing**
   - Images uploaded per minute
   - Success/failure ratio
   - Average images per job

3. **Performance**
   - Average job duration
   - P95/P99 job duration
   - Active jobs gauge

4. **Queue Health**
   - Queue depth
   - Messages in DLQ
   - Age of oldest message

5. **Errors**
   - Failed jobs (by error type)
   - Messages sent to DLQ
   - Retry attempts

---

## Scalability Analysis

### Horizontal Scaling

**Single Worker Instance:**
```
Messages per poll: 5
Processing time per job: 20 seconds (20 images)
Throughput: 15 jobs/minute = 300 images/minute
```

**5 Worker Instances:**
```
Throughput: 75 jobs/minute = 1500 images/minute
```

**10 Worker Instances:**
```
Throughput: 150 jobs/minute = 3000 images/minute
```

### Auto-Scaling Configuration

**Docker Compose (Development):**
```bash
# Scale workers manually
docker-compose up --scale image-upload-worker=5
```

**AWS ECS (Production):**
```yaml
AutoScalingConfiguration:
  TargetMetric: SQS ApproximateNumberOfMessages
  TargetValue: 50
  ScaleInCooldown: 300
  ScaleOutCooldown: 60
  MinCapacity: 1
  MaxCapacity: 10
```

**Kubernetes (Future):**
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: image-upload-worker
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: image-upload-worker
  minReplicas: 1
  maxReplicas: 10
  metrics:
  - type: External
    external:
      metric:
        name: sqs_queue_depth
      target:
        type: AverageValue
        averageValue: "50"
```

---

## Performance Comparison

### Before (Sequential HTTP Calls)

**Scenario: Admin uploads product with 20 images**

| Metric | Value | User Experience |
|--------|-------|-----------------|
| Response Time | 4000ms | ❌ Admin waits 4 seconds |
| Throughput | 15 products/hour | ❌ Very slow bulk import |
| Scalability | Poor | ❌ Adding servers doesn't help |
| Fault Tolerance | None | ❌ One failure = lose all |

### After (Event-Driven Architecture)

**Scenario: Admin uploads product with 20 images**

| Metric | Value | User Experience |
|--------|-------|-----------------|
| Response Time | 50ms | ✅ Instant response! |
| Throughput | 300+ products/hour | ✅ Fast bulk import |
| Scalability | Excellent | ✅ Add workers = more throughput |
| Fault Tolerance | Auto-retry + DLQ | ✅ Resilient to failures |

**Improvement:**
- **80x faster response** (4000ms → 50ms)
- **20x more throughput** (15 → 300 products/hour)
- **∞ scalability** (horizontal worker scaling)

---

## Migration Plan

### Phase 1: Build Infrastructure (Week 1)
- [ ] Create SNS topic and SQS queues
- [ ] Create S3 temp bucket with lifecycle
- [ ] Update init-aws.sh script
- [ ] Test infrastructure locally

### Phase 2: Implement Services (Week 2)
- [ ] Create event DTOs
- [ ] Implement temporary storage service
- [ ] Update command handler (publish events)
- [ ] Update Product entity
- [ ] Add unit tests

### Phase 3: Build Worker (Week 3)
- [ ] Create queue interface and SQS implementation
- [ ] Implement ImageUploadWorker background service
- [ ] Add retry logic and error handling
- [ ] Add metrics and logging
- [ ] Integration tests

### Phase 4: Deploy & Monitor (Week 4)
- [ ] Deploy to development environment
- [ ] Monitor metrics and logs
- [ ] Load testing (100 products)
- [ ] Fix any issues
- [ ] Documentation

### Phase 5: Production Rollout (Week 5)
- [ ] Deploy to staging
- [ ] Performance testing
- [ ] Deploy to production (gradual rollout)
- [ ] Monitor for 1 week
- [ ] Optimize based on metrics

---

## Rollback Plan

### If Issues Occur

**Option 1: Feature Flag (Recommended)**

```csharp
public class UploadProductImagesCommandHandler
{
    private readonly IFeatureFlag _featureFlags;

    public async Task<Result> Handle(...)
    {
        if (await _featureFlags.IsEnabledAsync("EventDrivenImageUpload"))
        {
            // New: Event-driven approach
            return await UploadViaEventsAsync(...);
        }
        else
        {
            // Old: Direct HTTP calls (fallback)
            return await UploadDirectlyAsync(...);
        }
    }
}
```

**Option 2: Quick Rollback**

1. Disable background worker:
   ```bash
   docker-compose stop image-upload-worker
   ```

2. Revert code changes:
   ```bash
   git revert <commit-hash>
   git push
   ```

3. Deploy previous version

4. Process pending messages manually

---

## Security Considerations

### Temporary Storage

- ✅ **Auto-delete** after 7 days (S3 lifecycle)
- ✅ **Tenant isolation** in S3 paths
- ✅ **Signed URLs** for secure access (future)
- ❌ **No public access** to temp bucket

### Queue Security

- ✅ **VPC isolation** in production
- ✅ **IAM policies** for queue access
- ✅ **Message encryption** at rest (AWS KMS)
- ✅ **DLQ** for failed messages (no data loss)

### Data Validation

- ✅ **File type validation** (JPEG, PNG, WebP only)
- ✅ **Size limits** (10 MB max per image)
- ✅ **Malware scanning** (future enhancement)
- ✅ **Image dimensions** check

---

## Cost Analysis

### Development (LocalStack)
- SNS/SQS: $0 (LocalStack)
- S3 Storage: $0 (LocalStack)
- Worker compute: $0 (local Docker)
**Total: $0**

### Production (AWS)

**Assumptions:**
- 1000 products/day
- 10 images per product = 10,000 images/day
- Average image size: 1 MB

**Costs:**
```
SNS:
  - 10,000 publishes/day × 30 days = 300,000/month
  - First 1M free, then $0.50/million
  - Cost: $0

SQS:
  - 10,000 messages/day × 30 days = 300,000/month
  - First 1M free
  - Cost: $0

S3 Temp Storage:
  - 10,000 images × 1 MB = 10 GB
  - Auto-deleted after 7 days, average storage: 5 GB
  - $0.023/GB/month × 5 GB = $0.12/month

S3 Requests (PUT/GET):
  - Uploads: 10,000 × $0.005/1000 = $0.05
  - Downloads: 10,000 × $0.0004/1000 = $0.004
  - Total: $0.054/month

ECS/Fargate (1 worker):
  - 0.25 vCPU, 0.5 GB RAM
  - $0.04048/vCPU-hour + $0.004445/GB-hour
  - (0.25 × $0.04048 + 0.5 × $0.004445) × 730 hours = $8.99/month

Total: ~$9.20/month (with room for 10x growth)
```

**Scaling:**
- 10,000 products/day = $50/month
- 100,000 products/day = $300/month

---

## FAQ

### Q: What happens if the worker crashes mid-processing?

**A:** The message visibility timeout expires (10 minutes), and the message becomes visible again in the queue for retry. After 3 failed attempts, it moves to the DLQ for manual review.

### Q: Can we process uploads faster?

**A:** Yes! Increase parallel processing:
```csharp
// Process 10 jobs concurrently instead of 5
var messages = await queue.ReceiveMessagesAsync(maxMessages: 10);
```

Or scale workers horizontally:
```bash
docker-compose up --scale image-upload-worker=10
```

### Q: How do we monitor stuck jobs?

**A:** Check queue metrics:
```bash
# Messages in queue
aws sqs get-queue-attributes --queue-url ... --attribute-names ApproximateNumberOfMessages

# Messages in DLQ (failures)
aws sqs get-queue-attributes --queue-url ...dlq --attribute-names ApproximateNumberOfMessages
```

Set up CloudWatch alarms:
- Queue depth > 100 for 5 minutes
- DLQ has any messages
- Oldest message age > 1 hour

### Q: What if S3 is down?

**A:**
1. SQS message returns to queue (visibility timeout)
2. Worker retries after 5 minutes
3. After 3 failures → DLQ
4. Admin gets alert
5. Manual intervention or wait for S3 recovery

### Q: Can we use this for video uploads?

**A:** Yes! Just increase:
- S3 temp storage retention (7 → 30 days)
- SQS visibility timeout (10 → 60 minutes)
- Worker processing logic for video transcoding
- File size limits (10 MB → 500 MB)

---

## Implementation Checklist

### Infrastructure
- [ ] Create S3 temp bucket with lifecycle
- [ ] Create SNS topic
- [ ] Create SQS queue with DLQ
- [ ] Subscribe queue to topic
- [ ] Update init-aws.sh
- [ ] Test infrastructure locally

### Catalog Service
- [ ] Create event DTOs
- [ ] Implement ITemporaryStorage interface
- [ ] Implement S3TemporaryStorage
- [ ] Update UploadProductImagesCommandHandler
- [ ] Update Product entity
- [ ] Add IEventPublisher (reuse or create)
- [ ] Register services in Startup
- [ ] Unit tests

### Background Worker
- [ ] Create IImageUploadQueue interface
- [ ] Implement SqsImageUploadQueue
- [ ] Create ImageUploadWorker service
- [ ] Add error handling and retries
- [ ] Add metrics and logging
- [ ] Register in Startup
- [ ] Integration tests

### Admin UI
- [ ] Add progress indicator component
- [ ] Poll product status API
- [ ] Show upload progress (X/Y images)
- [ ] Handle "Processing", "Ready", "Failed" states
- [ ] Add retry button for failed uploads

### Monitoring
- [ ] Add Prometheus metrics
- [ ] Create Grafana dashboard
- [ ] Set up CloudWatch alarms
- [ ] Configure DLQ monitoring
- [ ] Add structured logging

### Documentation
- [ ] API documentation
- [ ] Runbook for operations
- [ ] Troubleshooting guide
- [ ] Architecture diagram
- [ ] Performance benchmarks

### Testing
- [ ] Unit tests (80% coverage)
- [ ] Integration tests (end-to-end)
- [ ] Load tests (100 products, 20 images each)
- [ ] Failure scenario tests
- [ ] Performance regression tests

### Deployment
- [ ] Deploy to development
- [ ] Smoke tests
- [ ] Deploy to staging
- [ ] UAT testing
- [ ] Deploy to production (gradual rollout)
- [ ] Monitor for 1 week

---

## Next Steps

When you're ready to implement, tell Claude:

> **"Implement event-driven image upload as documented in event-driven-image-upload.md"**

Claude will follow this guide step-by-step to build the complete solution.

---

**Created:** December 27, 2024
**Last Updated:** December 27, 2024
**Owner:** Development Team
**Estimated Implementation Time:** 3-4 hours
**Priority:** Medium (performance optimization)
**Status:** 📋 Planned

---

## References

- [AWS SQS Best Practices](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-best-practices.html)
- [Event-Driven Architecture Patterns](https://martinfowler.com/articles/201701-event-driven.html)
- [Eventual Consistency](https://www.allthingsdistributed.com/2008/12/eventually_consistent.html)
- [Media Service Architecture](./media-service-architecture.md)
