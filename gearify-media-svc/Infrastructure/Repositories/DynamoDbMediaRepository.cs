using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Infrastructure.Constants;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of media repository
/// </summary>
public class DynamoDbMediaRepository : IMediaRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbMediaRepository> _logger;

    public DynamoDbMediaRepository(
        IAmazonDynamoDB dynamoDb,
        ILogger<DynamoDbMediaRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
    }

    public async Task<MediaMetadata?> GetByIdAsync(string mediaId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"MEDIA#{mediaId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return MapToMediaMetadata(response.Item);
    }

    public async Task<List<MediaMetadata>> GetByEntityAsync(string entityType, string entityId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            FilterExpression = "IsDeleted = :isDeleted",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#{entityType.ToUpper()}#{entityId}" } },
                { ":isDeleted", new AttributeValue { BOOL = false } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        return response.Items
            .Select(MapToMediaMetadata)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.UploadedAt)
            .ToList();
    }

    public async Task<List<MediaMetadata>> GetByIdsAsync(List<string> mediaIds, string tenantId)
    {
        if (!mediaIds.Any())
            return new List<MediaMetadata>();

        var results = new List<MediaMetadata>();

        // DynamoDB BatchGetItem limit is 100 items
        foreach (var batch in mediaIds.Chunk(100))
        {
            var request = new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    {
                        DynamoDbTableNames.MEDIA,
                        new KeysAndAttributes
                        {
                            Keys = batch.Select(id => new Dictionary<string, AttributeValue>
                            {
                                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                                { "SK", new AttributeValue { S = $"MEDIA#{id}" } }
                            }).ToList()
                        }
                    }
                }
            };

            var response = await _dynamoDb.BatchGetItemAsync(request);

            if (response.Responses.TryGetValue(DynamoDbTableNames.MEDIA, out var items))
            {
                results.AddRange(items.Select(MapToMediaMetadata));
            }
        }

        return results;
    }

    public async Task CreateAsync(MediaMetadata media)
    {
        var item = MapToAttributeValues(media);

        var request = new PutItemRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            Item = item
        };

        await _dynamoDb.PutItemAsync(request);

        _logger.LogInformation("Created media metadata: {MediaId} for {EntityType} {EntityId}",
            media.Id, media.EntityType, media.EntityId);
    }

    public async Task UpdateAsync(MediaMetadata media)
    {
        var item = MapToAttributeValues(media);

        var request = new PutItemRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            Item = item
        };

        await _dynamoDb.PutItemAsync(request);

        _logger.LogInformation("Updated media metadata: {MediaId}", media.Id);
    }

    public async Task DeleteAsync(string mediaId, string tenantId)
    {
        var request = new UpdateItemRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"MEDIA#{mediaId}" } }
            },
            UpdateExpression = "SET IsDeleted = :isDeleted, DeletedAt = :deletedAt",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":isDeleted", new AttributeValue { BOOL = true } },
                { ":deletedAt", new AttributeValue { S = DateTime.UtcNow.ToString("o") } }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);

        _logger.LogInformation("Soft deleted media: {MediaId}", mediaId);
    }

    public async Task HardDeleteAsync(string mediaId, string tenantId)
    {
        var request = new DeleteItemRequest
        {
            TableName = DynamoDbTableNames.MEDIA,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"MEDIA#{mediaId}" } }
            }
        };

        await _dynamoDb.DeleteItemAsync(request);

        _logger.LogInformation("Hard deleted media: {MediaId}", mediaId);
    }

    private MediaMetadata MapToMediaMetadata(Dictionary<string, AttributeValue> item)
    {
        return new MediaMetadata
        {
            PK = item["PK"].S,
            SK = item["SK"].S,
            GSI1PK = item.TryGetValue("GSI1PK", out var gsi1pk) ? gsi1pk.S : string.Empty,
            GSI1SK = item.TryGetValue("GSI1SK", out var gsi1sk) ? gsi1sk.S : string.Empty,
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            EntityType = item["EntityType"].S,
            EntityId = item["EntityId"].S,
            FileName = item["FileName"].S,
            OriginalFileName = item["OriginalFileName"].S,
            ContentType = item["ContentType"].S,
            SizeInBytes = long.Parse(item["SizeInBytes"].N),
            Width = item.TryGetValue("Width", out var width) ? int.Parse(width.N) : null,
            Height = item.TryGetValue("Height", out var height) ? int.Parse(height.N) : null,
            OriginalKey = item.TryGetValue("OriginalKey", out var origKey) ? origKey.S : string.Empty,
            ThumbnailKey = item.TryGetValue("ThumbnailKey", out var thumbKey) ? thumbKey.S : string.Empty,
            MediumKey = item.TryGetValue("MediumKey", out var medKey) ? medKey.S : string.Empty,
            LargeKey = item.TryGetValue("LargeKey", out var largeKey) ? largeKey.S : string.Empty,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
            AltText = item.TryGetValue("AltText", out var altText) ? altText.S : null,
            UploadedAt = DateTime.Parse(item["UploadedAt"].S),
            UploadedBy = item.TryGetValue("UploadedBy", out var uploadedBy) ? uploadedBy.S : string.Empty,
            IsDeleted = item.TryGetValue("IsDeleted", out var isDeleted) && isDeleted.BOOL,
            DeletedAt = item.TryGetValue("DeletedAt", out var deletedAt) ? DateTime.Parse(deletedAt.S) : null
        };
    }

    private Dictionary<string, AttributeValue> MapToAttributeValues(MediaMetadata media)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = media.PK } },
            { "SK", new AttributeValue { S = media.SK } },
            { "GSI1PK", new AttributeValue { S = media.GSI1PK } },
            { "GSI1SK", new AttributeValue { S = media.GSI1SK } },
            { "Id", new AttributeValue { S = media.Id } },
            { "TenantId", new AttributeValue { S = media.TenantId } },
            { "EntityType", new AttributeValue { S = media.EntityType } },
            { "EntityId", new AttributeValue { S = media.EntityId } },
            { "FileName", new AttributeValue { S = media.FileName } },
            { "OriginalFileName", new AttributeValue { S = media.OriginalFileName } },
            { "ContentType", new AttributeValue { S = media.ContentType } },
            { "SizeInBytes", new AttributeValue { N = media.SizeInBytes.ToString() } },
            { "OriginalKey", new AttributeValue { S = media.OriginalKey } },
            { "ThumbnailKey", new AttributeValue { S = media.ThumbnailKey } },
            { "MediumKey", new AttributeValue { S = media.MediumKey } },
            { "LargeKey", new AttributeValue { S = media.LargeKey } },
            { "DisplayOrder", new AttributeValue { N = media.DisplayOrder.ToString() } },
            { "UploadedAt", new AttributeValue { S = media.UploadedAt.ToString("o") } },
            { "UploadedBy", new AttributeValue { S = media.UploadedBy } },
            { "IsDeleted", new AttributeValue { BOOL = media.IsDeleted } }
        };

        if (media.Width.HasValue)
            item["Width"] = new AttributeValue { N = media.Width.Value.ToString() };

        if (media.Height.HasValue)
            item["Height"] = new AttributeValue { N = media.Height.Value.ToString() };

        if (!string.IsNullOrEmpty(media.AltText))
            item["AltText"] = new AttributeValue { S = media.AltText };

        if (media.DeletedAt.HasValue)
            item["DeletedAt"] = new AttributeValue { S = media.DeletedAt.Value.ToString("o") };

        return item;
    }
}
