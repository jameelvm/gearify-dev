using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.OrderService.Domain.Entities;
using System.Text.Json;

namespace Gearify.OrderService.Infrastructure.Repositories;

public class DynamoDbOrderRepository : IOrderRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "gearify-orders";

    public DynamoDbOrderRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task<Order?> GetByIdAsync(string orderId, string tenantId)
    {
        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"ORDER#{orderId}" } }
            }
        });

        return response.IsItemSet ? DeserializeOrder(response.Item) : null;
    }

    public async Task<List<Order>> GetByUserIdAsync(string userId, string tenantId)
    {
        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#USER#{userId}" } }
            }
        });

        return response.Items.Select(DeserializeOrder).ToList();
    }

    public async Task CreateAsync(Order order)
    {
        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{order.TenantId}" } },
                { "SK", new AttributeValue { S = $"ORDER#{order.Id}" } },
                { "GSI1PK", new AttributeValue { S = $"TENANT#{order.TenantId}#USER#{order.UserId}" } },
                { "GSI1SK", new AttributeValue { S = $"ORDER#{order.Id}" } },
                { "Id", new AttributeValue { S = order.Id } },
                { "UserId", new AttributeValue { S = order.UserId } },
                { "TotalAmount", new AttributeValue { N = order.TotalAmount.ToString() } },
                { "Status", new AttributeValue { S = order.Status.ToString() } },
                { "Items", new AttributeValue { S = JsonSerializer.Serialize(order.Items) } },
                { "CreatedAt", new AttributeValue { S = order.CreatedAt.ToString("O") } }
            }
        });
    }

    public async Task UpdateAsync(Order order)
    {
        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{order.TenantId}" } },
                { "SK", new AttributeValue { S = $"ORDER#{order.Id}" } }
            },
            UpdateExpression = "SET #status = :status, UpdatedAt = :updated",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#status", "Status" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":status", new AttributeValue { S = order.Status.ToString() } },
                { ":updated", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
            }
        });
    }

    private Order DeserializeOrder(Dictionary<string, AttributeValue> item)
    {
        return new Order
        {
            Id = item["Id"].S,
            TenantId = item.ContainsKey("TenantId") ? item["TenantId"].S : "",
            UserId = item["UserId"].S,
            TotalAmount = decimal.Parse(item["TotalAmount"].N),
            Status = Enum.Parse<OrderStatus>(item["Status"].S),
            Items = JsonSerializer.Deserialize<List<OrderItem>>(item["Items"].S) ?? new(),
            CreatedAt = DateTime.Parse(item["CreatedAt"].S)
        };
    }
}
