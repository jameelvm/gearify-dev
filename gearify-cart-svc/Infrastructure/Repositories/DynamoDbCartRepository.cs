using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CartService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Infrastructure.Repositories;

public class DynamoDbCartRepository : ICartRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbCartRepository> _logger;
    private readonly string _tableName;

    public DynamoDbCartRepository(
        IAmazonDynamoDB dynamoDb,
        IConfiguration configuration,
        ILogger<DynamoDbCartRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
        _tableName = configuration["StorageConfiguration:DynamoDb:CartTableName"] ?? "gearify-carts";
    }

    public async Task<Cart?> GetCartAsync(string userId, string tenantId)
    {
        try
        {
            var pk = $"TENANT#{tenantId}#USER#{userId}";

            // Query all items for this cart (metadata + items)
            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = pk } }
                }
            });

            if (response.Items.Count == 0)
            {
                return null;
            }

            // Find metadata record and item records
            var metadataItem = response.Items.FirstOrDefault(i =>
                i.TryGetValue("SK", out var sk) && sk.S == "CART#METADATA");

            if (metadataItem == null)
            {
                return null;
            }

            var cart = MapToCart(metadataItem);

            // Map cart items
            var cartItems = response.Items
                .Where(i => i.TryGetValue("SK", out var sk) && sk.S.StartsWith("ITEM#"))
                .Select(MapToCartItem)
                .ToList();

            cart.Items = cartItems;

            _logger.LogDebug("Retrieved cart for user {UserId} with {ItemCount} items", userId, cart.Items.Count);

            return cart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart for user {UserId}", userId);
            throw;
        }
    }

    public async Task SaveCartAsync(Cart cart)
    {
        try
        {
            var pk = $"TENANT#{cart.TenantId}#USER#{cart.UserId}";

            // Use TransactWriteItems to save metadata and all items atomically
            var transactItems = new List<TransactWriteItem>
            {
                // Cart metadata
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = CreateMetadataItem(cart, pk)
                    }
                }
            };

            // Cart items
            foreach (var item in cart.Items)
            {
                transactItems.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = CreateCartItemRecord(item, pk, cart)
                    }
                });
            }

            // Execute in batches of 25 (DynamoDB limit)
            foreach (var batch in transactItems.Chunk(25))
            {
                await _dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = batch.ToList()
                });
            }

            _logger.LogDebug("Saved cart {CartId} for user {UserId} with {ItemCount} items",
                cart.Id, cart.UserId, cart.Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cart for user {UserId}", cart.UserId);
            throw;
        }
    }

    public async Task DeleteCartAsync(string userId, string tenantId)
    {
        try
        {
            var pk = $"TENANT#{tenantId}#USER#{userId}";

            // First, query all items for this cart
            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = pk } }
                },
                ProjectionExpression = "PK, SK"
            });

            if (response.Items.Count == 0)
            {
                return;
            }

            // Delete all items in batches of 25
            foreach (var batch in response.Items.Chunk(25))
            {
                var writeRequests = batch.Select(item => new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "PK", item["PK"] },
                            { "SK", item["SK"] }
                        }
                    }
                }).ToList();

                await _dynamoDb.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        { _tableName, writeRequests }
                    }
                });
            }

            _logger.LogDebug("Deleted cart for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cart for user {UserId}", userId);
            throw;
        }
    }

    private Dictionary<string, AttributeValue> CreateMetadataItem(Cart cart, string pk)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = pk } },
            { "SK", new AttributeValue { S = "CART#METADATA" } },
            { "Id", new AttributeValue { S = cart.Id } },
            { "UserId", new AttributeValue { S = cart.UserId } },
            { "TenantId", new AttributeValue { S = cart.TenantId } },
            { "ItemCount", new AttributeValue { N = cart.Items.Count.ToString() } },
            { "TotalAmount", new AttributeValue { N = cart.TotalAmount.ToString("F2") } },
            { "Currency", new AttributeValue { S = cart.Currency } },
            { "Status", new AttributeValue { S = "active" } },
            { "CreatedAt", new AttributeValue { S = cart.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = cart.UpdatedAt.ToString("O") } },
            // GSI1: For abandoned cart queries by status
            { "GSI1PK", new AttributeValue { S = $"TENANT#{cart.TenantId}#STATUS#active" } },
            { "GSI1SK", new AttributeValue { S = cart.UpdatedAt.ToString("O") } },
            // GSI2: For user carts across tenants
            { "GSI2PK", new AttributeValue { S = $"USER#{cart.UserId}" } },
            { "GSI2SK", new AttributeValue { S = $"TENANT#{cart.TenantId}" } }
        };

        if (cart.ExpiresAt.HasValue)
        {
            item["ExpiresAt"] = new AttributeValue { S = cart.ExpiresAt.Value.ToString("O") };
            item["TTL"] = new AttributeValue { N = new DateTimeOffset(cart.ExpiresAt.Value).ToUnixTimeSeconds().ToString() };
        }

        return item;
    }

    private Dictionary<string, AttributeValue> CreateCartItemRecord(CartItem item, string pk, Cart cart)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = pk } },
            { "SK", new AttributeValue { S = $"ITEM#{item.ProductId}" } },
            { "ProductId", new AttributeValue { S = item.ProductId } },
            { "ProductName", new AttributeValue { S = item.ProductName } },
            { "Sku", new AttributeValue { S = item.Sku } },
            { "Quantity", new AttributeValue { N = item.Quantity.ToString() } },
            { "UnitPrice", new AttributeValue { N = item.Price.ToString("F2") } },
            { "LineTotal", new AttributeValue { N = (item.Price * item.Quantity).ToString("F2") } },
            { "ImageUrl", new AttributeValue { S = item.ImageUrl } },
            { "AddedAt", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
        };
    }

    private Cart MapToCart(Dictionary<string, AttributeValue> item)
    {
        var cart = new Cart
        {
            Id = item.TryGetValue("Id", out var id) ? id.S : string.Empty,
            UserId = item.TryGetValue("UserId", out var userId) ? userId.S : string.Empty,
            TenantId = item.TryGetValue("TenantId", out var tenantId) ? tenantId.S : string.Empty,
            TotalAmount = item.TryGetValue("TotalAmount", out var total) ? decimal.Parse(total.N) : 0,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            CreatedAt = item.TryGetValue("CreatedAt", out var created) ? DateTime.Parse(created.S) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAt", out var updated) ? DateTime.Parse(updated.S) : DateTime.UtcNow
        };

        if (item.TryGetValue("ExpiresAt", out var expires))
        {
            cart.ExpiresAt = DateTime.Parse(expires.S);
        }

        return cart;
    }

    private CartItem MapToCartItem(Dictionary<string, AttributeValue> item)
    {
        return new CartItem
        {
            ProductId = item.TryGetValue("ProductId", out var productId) ? productId.S : string.Empty,
            ProductName = item.TryGetValue("ProductName", out var name) ? name.S : string.Empty,
            Sku = item.TryGetValue("Sku", out var sku) ? sku.S : string.Empty,
            Quantity = item.TryGetValue("Quantity", out var qty) ? int.Parse(qty.N) : 0,
            Price = item.TryGetValue("UnitPrice", out var price) ? decimal.Parse(price.N) : 0,
            ImageUrl = item.TryGetValue("ImageUrl", out var imageUrl) ? imageUrl.S : string.Empty
        };
    }
}
