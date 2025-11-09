using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.TenantService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Gearify.TenantService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of tenant repository
/// </summary>
public class DynamoDbTenantRepository : ITenantRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "gearify-tenants";
    private readonly ILogger<DynamoDbTenantRepository> _logger;

    public DynamoDbTenantRepository(
        IAmazonDynamoDB dynamoDb,
        ILogger<DynamoDbTenantRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
    }

    public async Task<Tenant?> GetByIdAsync(string tenantId)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                    { "SK", new AttributeValue { S = $"TENANT#{tenantId}" } }
                }
            };

            var response = await _dynamoDb.GetItemAsync(request);

            if (!response.IsItemSet)
                return null;

            return DeserializeTenant(response.Item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<IEnumerable<Tenant>> GetAllActiveAsync()
    {
        try
        {
            var request = new ScanRequest
            {
                TableName = _tableName,
                FilterExpression = "IsActive = :isActive",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":isActive", new AttributeValue { BOOL = true } }
                }
            };

            var response = await _dynamoDb.ScanAsync(request);
            return response.Items.Select(DeserializeTenant).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active tenants");
            throw;
        }
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync()
    {
        try
        {
            var request = new ScanRequest
            {
                TableName = _tableName
            };

            var response = await _dynamoDb.ScanAsync(request);
            return response.Items.Select(DeserializeTenant).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tenants");
            throw;
        }
    }

    public async Task CreateAsync(Tenant tenant)
    {
        try
        {
            var item = SerializeTenant(tenant);

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(PK)" // Prevent overwriting existing tenant
            });

            _logger.LogInformation("Tenant created: {TenantId}", tenant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant {TenantId}", tenant.Id);
            throw;
        }
    }

    public async Task UpdateAsync(Tenant tenant)
    {
        try
        {
            tenant.UpdatedAt = DateTime.UtcNow;
            var item = SerializeTenant(tenant);

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });

            _logger.LogInformation("Tenant updated: {TenantId}", tenant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", tenant.Id);
            throw;
        }
    }

    public async Task DeleteAsync(string tenantId)
    {
        try
        {
            // Soft delete - just mark as inactive
            var tenant = await GetByIdAsync(tenantId);
            if (tenant != null)
            {
                tenant.IsActive = false;
                await UpdateAsync(tenant);
                _logger.LogInformation("Tenant soft-deleted: {TenantId}", tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<bool> ExistsAndActiveAsync(string tenantId)
    {
        try
        {
            var tenant = await GetByIdAsync(tenantId);
            return tenant != null && tenant.IsActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tenant existence {TenantId}", tenantId);
            return false;
        }
    }

    private Dictionary<string, AttributeValue> SerializeTenant(Tenant tenant)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{tenant.Id}" } },
            { "SK", new AttributeValue { S = $"TENANT#{tenant.Id}" } },
            { "Id", new AttributeValue { S = tenant.Id } },
            { "Name", new AttributeValue { S = tenant.Name } },
            { "IsActive", new AttributeValue { BOOL = tenant.IsActive } },
            { "CreatedAt", new AttributeValue { S = tenant.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = tenant.UpdatedAt.ToString("O") } },
            { "Plan", new AttributeValue { S = tenant.Plan } },
            { "MaxUsers", new AttributeValue { N = tenant.MaxUsers.ToString() } },
            { "ContactEmail", new AttributeValue { S = tenant.ContactEmail } }
        };

        if (!string.IsNullOrEmpty(tenant.CustomDomain))
        {
            item["CustomDomain"] = new AttributeValue { S = tenant.CustomDomain };
        }

        return item;
    }

    private Tenant DeserializeTenant(Dictionary<string, AttributeValue> item)
    {
        return new Tenant
        {
            Id = item["Id"].S,
            Name = item["Name"].S,
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            Plan = item.ContainsKey("Plan") ? item["Plan"].S : "Free",
            MaxUsers = item.ContainsKey("MaxUsers") ? int.Parse(item["MaxUsers"].N) : 10,
            ContactEmail = item.ContainsKey("ContactEmail") ? item["ContactEmail"].S : string.Empty,
            CustomDomain = item.ContainsKey("CustomDomain") ? item["CustomDomain"].S : null
        };
    }
}
