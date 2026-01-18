using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of user session repository
/// </summary>
public class DynamoDbUserSessionRepository : IUserSessionRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbUserSessionRepository> _logger;
    private readonly string _tableName;

    public DynamoDbUserSessionRepository(
        IAmazonDynamoDB dynamoDb,
        IOptions<StorageConfiguration> storageConfig,
        ILogger<DynamoDbUserSessionRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = storageConfig.Value.DynamoDb.SessionsTableName;
        _logger = logger;
    }

    public async Task CreateAsync(UserSession session)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"USER#{session.UserId}" } },
                { "SK", new AttributeValue { S = $"SESSION#{session.Id}" } },
                { "Id", new AttributeValue { S = session.Id } },
                { "UserId", new AttributeValue { S = session.UserId } },
                { "TenantId", new AttributeValue { S = session.TenantId } },
                { "RefreshToken", new AttributeValue { S = session.RefreshToken } },
                { "DeviceInfo", new AttributeValue { S = session.DeviceInfo } },
                { "IpAddress", new AttributeValue { S = session.IpAddress } },
                { "CreatedAt", new AttributeValue { S = session.CreatedAt.ToString("O") } },
                { "LastAccessedAt", new AttributeValue { S = session.LastAccessedAt.ToString("O") } },
                { "ExpiresAt", new AttributeValue { S = session.ExpiresAt.ToString("O") } },
                { "IsActive", new AttributeValue { BOOL = session.IsActive } }
            };

            if (!string.IsNullOrEmpty(session.Location))
            {
                item.Add("Location", new AttributeValue { S = session.Location });
            }

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            };

            await _dynamoDb.PutItemAsync(request);
            _logger.LogInformation("Created session {SessionId} for user {UserId}", session.Id, session.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {UserId}", session.UserId);
            throw;
        }
    }

    public async Task<UserSession?> GetByIdAsync(string sessionId, string userId)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"USER#{userId}" } },
                    { "SK", new AttributeValue { S = $"SESSION#{sessionId}" } }
                }
            };

            var response = await _dynamoDb.GetItemAsync(request);

            if (response.Item == null || response.Item.Count == 0)
            {
                return null;
            }

            return MapToUserSession(response.Item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session {SessionId} for user {UserId}", sessionId, userId);
            throw;
        }
    }

    public async Task<UserSession?> GetByRefreshTokenAsync(string userId, string refreshToken)
    {
        try
        {
            var sessions = await GetUserSessionsAsync(userId);
            return sessions.FirstOrDefault(s => s.RefreshToken == refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session by refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<UserSession>> GetUserSessionsAsync(string userId)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                    { ":sk", new AttributeValue { S = "SESSION#" } }
                }
            };

            var response = await _dynamoDb.QueryAsync(queryRequest);

            return response.Items.Select(MapToUserSession).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<UserSession>> GetActiveSessionsAsync(string userId)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                FilterExpression = "IsActive = :isActive AND ExpiresAt > :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                    { ":sk", new AttributeValue { S = "SESSION#" } },
                    { ":isActive", new AttributeValue { BOOL = true } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
                }
            };

            var response = await _dynamoDb.QueryAsync(queryRequest);

            return response.Items.Select(MapToUserSession).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateAsync(UserSession session)
    {
        try
        {
            var updateRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"USER#{session.UserId}" } },
                    { "SK", new AttributeValue { S = $"SESSION#{session.Id}" } }
                },
                UpdateExpression = "SET IsActive = :isActive, LastAccessedAt = :lastAccessedAt",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":isActive", new AttributeValue { BOOL = session.IsActive } },
                    { ":lastAccessedAt", new AttributeValue { S = session.LastAccessedAt.ToString("O") } }
                }
            };

            await _dynamoDb.UpdateItemAsync(updateRequest);
            _logger.LogInformation("Updated session {SessionId} for user {UserId}", session.Id, session.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session {SessionId} for user {UserId}", session.Id, session.UserId);
            throw;
        }
    }

    public async Task DeleteAsync(string sessionId, string userId)
    {
        try
        {
            var deleteRequest = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"USER#{userId}" } },
                    { "SK", new AttributeValue { S = $"SESSION#{sessionId}" } }
                }
            };

            await _dynamoDb.DeleteItemAsync(deleteRequest);
            _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId} for user {UserId}", sessionId, userId);
            throw;
        }
    }

    public async Task DeleteAllUserSessionsAsync(string userId, string? exceptSessionId = null)
    {
        try
        {
            var sessions = await GetUserSessionsAsync(userId);

            var sessionsToDelete = string.IsNullOrEmpty(exceptSessionId)
                ? sessions
                : sessions.Where(s => s.Id != exceptSessionId).ToList();

            foreach (var session in sessionsToDelete)
            {
                await DeleteAsync(session.Id, userId);
            }

            _logger.LogInformation("Deleted {Count} sessions for user {UserId}", sessionsToDelete.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete all sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> DeleteExpiredSessionsAsync(string userId)
    {
        try
        {
            var sessions = await GetUserSessionsAsync(userId);
            var expiredSessions = sessions.Where(s => s.ExpiresAt < DateTime.UtcNow).ToList();

            foreach (var session in expiredSessions)
            {
                await DeleteAsync(session.Id, userId);
            }

            _logger.LogInformation("Deleted {Count} expired sessions for user {UserId}", expiredSessions.Count, userId);

            return expiredSessions.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete expired sessions for user {UserId}", userId);
            throw;
        }
    }

    private static UserSession MapToUserSession(Dictionary<string, AttributeValue> item)
    {
        return new UserSession
        {
            Id = item["Id"].S,
            UserId = item["UserId"].S,
            TenantId = item["TenantId"].S,
            RefreshToken = item["RefreshToken"].S,
            DeviceInfo = item["DeviceInfo"].S,
            IpAddress = item["IpAddress"].S,
            Location = item.ContainsKey("Location") ? item["Location"].S : null,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            LastAccessedAt = DateTime.Parse(item["LastAccessedAt"].S),
            ExpiresAt = DateTime.Parse(item["ExpiresAt"].S),
            IsActive = item["IsActive"].BOOL
        };
    }
}
