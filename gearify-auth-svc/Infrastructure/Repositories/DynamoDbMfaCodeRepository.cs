using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of MFA code repository
/// </summary>
public class DynamoDbMfaCodeRepository : IMfaCodeRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbMfaCodeRepository> _logger;
    private const string TableName = "MfaCodes";

    public DynamoDbMfaCodeRepository(
        IAmazonDynamoDB dynamoDb,
        ILogger<DynamoDbMfaCodeRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
    }

    public async Task CreateAsync(MfaCode code)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"USER#{code.UserId}" } },
                { "SK", new AttributeValue { S = $"MFACODE#{code.Id}" } },
                { "Id", new AttributeValue { S = code.Id } },
                { "UserId", new AttributeValue { S = code.UserId } },
                { "TenantId", new AttributeValue { S = code.TenantId } },
                { "CodeHash", new AttributeValue { S = code.CodeHash } },
                { "Method", new AttributeValue { S = code.Method.ToString() } },
                { "CreatedAt", new AttributeValue { S = code.CreatedAt.ToString("O") } },
                { "ExpiresAt", new AttributeValue { S = code.ExpiresAt.ToString("O") } },
                { "IsUsed", new AttributeValue { BOOL = code.IsUsed } },
                { "AttemptCount", new AttributeValue { N = code.AttemptCount.ToString() } },
                { "Purpose", new AttributeValue { S = code.Purpose } }
            };

            var request = new PutItemRequest
            {
                TableName = TableName,
                Item = item
            };

            await _dynamoDb.PutItemAsync(request);
            _logger.LogInformation("MFA code created for user {UserId}", code.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MFA code for user {UserId}", code.UserId);
            throw;
        }
    }

    public async Task<MfaCode?> GetByUserAndCodeAsync(string userId, string codeHash, string purpose)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = TableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                    { ":sk", new AttributeValue { S = "MFACODE#" } }
                }
            };

            var response = await _dynamoDb.QueryAsync(queryRequest);

            foreach (var item in response.Items)
            {
                var code = MapToMfaCode(item);

                // Match by code hash and optionally purpose
                if (BCrypt.Net.BCrypt.Verify(codeHash, code.CodeHash))
                {
                    if (string.IsNullOrEmpty(purpose) || code.Purpose == purpose)
                    {
                        return code;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA code for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<MfaCode>> GetActiveCodesAsync(string userId)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = TableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                FilterExpression = "IsUsed = :isUsed AND ExpiresAt > :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                    { ":sk", new AttributeValue { S = "MFACODE#" } },
                    { ":isUsed", new AttributeValue { BOOL = false } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
                }
            };

            var response = await _dynamoDb.QueryAsync(queryRequest);

            return response.Items.Select(MapToMfaCode).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active MFA codes for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateAsync(MfaCode code)
    {
        try
        {
            var updateRequest = new UpdateItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"USER#{code.UserId}" } },
                    { "SK", new AttributeValue { S = $"MFACODE#{code.Id}" } }
                },
                UpdateExpression = "SET IsUsed = :isUsed, AttemptCount = :attemptCount",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":isUsed", new AttributeValue { BOOL = code.IsUsed } },
                    { ":attemptCount", new AttributeValue { N = code.AttemptCount.ToString() } }
                }
            };

            await _dynamoDb.UpdateItemAsync(updateRequest);
            _logger.LogInformation("MFA code updated for user {UserId}", code.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MFA code for user {UserId}", code.UserId);
            throw;
        }
    }

    public async Task<int> DeleteExpiredCodesAsync(string userId)
    {
        try
        {
            var codes = await GetAllCodesAsync(userId);
            var expiredCodes = codes.Where(c => c.ExpiresAt < DateTime.UtcNow).ToList();

            foreach (var code in expiredCodes)
            {
                await DeleteCodeAsync(userId, code.Id);
            }

            _logger.LogInformation("Deleted {Count} expired MFA codes for user {UserId}", expiredCodes.Count, userId);
            return expiredCodes.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete expired MFA codes for user {UserId}", userId);
            throw;
        }
    }

    public async Task DeleteAllUserCodesAsync(string userId)
    {
        try
        {
            var codes = await GetAllCodesAsync(userId);

            foreach (var code in codes)
            {
                await DeleteCodeAsync(userId, code.Id);
            }

            _logger.LogInformation("Deleted all MFA codes for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete all MFA codes for user {UserId}", userId);
            throw;
        }
    }

    private async Task<List<MfaCode>> GetAllCodesAsync(string userId)
    {
        var queryRequest = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                { ":sk", new AttributeValue { S = "MFACODE#" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(queryRequest);
        return response.Items.Select(MapToMfaCode).ToList();
    }

    private async Task DeleteCodeAsync(string userId, string codeId)
    {
        var deleteRequest = new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"USER#{userId}" } },
                { "SK", new AttributeValue { S = $"MFACODE#{codeId}" } }
            }
        };

        await _dynamoDb.DeleteItemAsync(deleteRequest);
    }

    private static MfaCode MapToMfaCode(Dictionary<string, AttributeValue> item)
    {
        return new MfaCode
        {
            Id = item["Id"].S,
            UserId = item["UserId"].S,
            TenantId = item["TenantId"].S,
            CodeHash = item["CodeHash"].S,
            Method = Enum.Parse<MfaMethod>(item["Method"].S),
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            ExpiresAt = DateTime.Parse(item["ExpiresAt"].S),
            IsUsed = item["IsUsed"].BOOL,
            AttemptCount = int.Parse(item["AttemptCount"].N),
            Purpose = item["Purpose"].S
        };
    }
}
