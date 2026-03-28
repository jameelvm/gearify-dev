using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using CsvHelper;
using CsvHelper.Configuration;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Infrastructure.ML;

public class PersonalizeDataExporter
{
    private readonly IProductRepository _productRepository;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<PersonalizeDataExporter> _logger;
    private readonly AIServiceConfiguration _config;

    public PersonalizeDataExporter(
        IProductRepository productRepository,
        IAmazonS3 s3Client,
        ILogger<PersonalizeDataExporter> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _productRepository = productRepository;
        _s3Client = s3Client;
        _logger = logger;
        _config = config.Value;
    }

    public async Task ExportItemsDatasetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var bucket = _config.DataExportBucket ?? "gearify-ml-data";
        var key = $"personalize/{tenantId}/items/{DateTime.UtcNow:yyyy-MM-dd}/items.csv";

        _logger.LogInformation("Starting items dataset export for tenant {TenantId}", tenantId);

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        // Header
        csv.WriteField("ITEM_ID");
        csv.WriteField("CATEGORY");
        csv.WriteField("SUBCATEGORY");
        csv.WriteField("BRAND");
        csv.WriteField("PRICE");
        csv.WriteField("DEPARTMENT");
        csv.WriteField("CREATION_TIMESTAMP");
        csv.WriteField("IS_DEAL");
        csv.WriteField("IS_BEST_SELLER");
        csv.WriteField("RATING_AVERAGE");
        await csv.NextRecordAsync();

        var products = await _productRepository.GetAllAsync(tenantId);
        var count = 0;

        foreach (var product in products)
        {
            csv.WriteField(product.Id);
            csv.WriteField(product.Category);
            csv.WriteField(product.Subcategory);
            csv.WriteField(product.Brand);
            csv.WriteField(product.Price);
            csv.WriteField(product.Department);
            csv.WriteField(new DateTimeOffset(product.CreatedAt).ToUnixTimeSeconds());
            csv.WriteField(product.IsDeal ? 1 : 0);
            csv.WriteField(product.IsBestSeller ? 1 : 0);
            csv.WriteField(product.RatingAverage ?? 0);
            await csv.NextRecordAsync();
            count++;
        }

        await writer.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = memoryStream,
            ContentType = "text/csv"
        }, cancellationToken);

        _logger.LogInformation("Exported {Count} items to s3://{Bucket}/{Key}", count, bucket, key);
    }
}
