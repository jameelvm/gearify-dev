using Amazon.S3;
using Amazon.S3.Model;
using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Infrastructure.Storage;

/// <summary>
/// S3 implementation of storage service
/// </summary>
public class ProductImageS3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductImageS3StorageService> _logger;
    private readonly string _bucketName;

    public ProductImageS3StorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IOptions<StorageConfiguration> storageSettings,
        ILogger<ProductImageS3StorageService> logger)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
        _bucketName = storageSettings.Value.AmazonS3.ProductImageBucketName;
    }

    public async Task<string> UploadAsync(Stream fileStream, string key, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = contentType,
                AutoCloseStream = false
            };

            var response = await _s3Client.PutObjectAsync(request, cancellationToken);

            _logger.LogInformation("Uploaded file to S3: {Key}, ETag: {ETag}", key, response.ETag);

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to S3: {Key}", key);
            throw;
        }
    }

    public async Task<Dictionary<ImageSize, string>> UploadVariantsAsync(
        Dictionary<ImageSize, Stream> variants,
        string baseKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<ImageSize, string>();

        foreach (var variant in variants)
        {
            var size = variant.Key;
            var stream = variant.Value;
            var key = GetVariantKey(baseKey, size);

            stream.Position = 0; // Reset stream position
            await UploadAsync(stream, key, contentType, cancellationToken);
            results[size] = key;
        }

        return results;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogInformation("Deleted file from S3: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from S3: {Key}", key);
            throw;
        }
    }

    public async Task DeleteMultipleAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keysList = keys.ToList();
        if (!keysList.Any())
            return;

        try
        {
            var request = new DeleteObjectsRequest
            {
                BucketName = _bucketName,
                Objects = keysList.Select(k => new KeyVersion { Key = k }).ToList()
            };

            var response = await _s3Client.DeleteObjectsAsync(request, cancellationToken);

            _logger.LogInformation("Deleted {Count} files from S3", response.DeletedObjects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple files from S3");
            throw;
        }
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file from S3: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence in S3: {Key}", key);
            throw;
        }
    }

    public string GetPublicUrl(string key)
    {
        var serviceUrl = _configuration["AWS:S3:ServiceUrl"] ?? "http://localhost:4566";
        return $"{serviceUrl}/{_bucketName}/{key}";
    }

    public async Task<string> GetPresignedUrlAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(expiration)
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for: {Key}", key);
            throw;
        }
    }

    private string GetVariantKey(string baseKey, ImageSize size)
    {
        var parts = baseKey.Split('/');
        if (parts.Length < 2)
            return baseKey;

        // Replace the size folder in the path
        // Example: tenants/default/products/prod-001/original/image.jpg
        //       -> tenants/default/products/prod-001/thumbnail/image.jpg
        parts[^2] = size.ToString().ToLower();
        return string.Join('/', parts);
    }
}
