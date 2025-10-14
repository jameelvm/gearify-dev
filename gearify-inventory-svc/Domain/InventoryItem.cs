using Amazon.DynamoDBv2.DataModel;
using System;

namespace Gearify.InventoryService.Domain;

[DynamoDBTable("gearify-inventory")]
public class InventoryItem
{
    [DynamoDBHashKey]
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[DynamoDBTable("gearify-reservations")]
public class StockReservation
{
    [DynamoDBHashKey]
    public string ReservationId { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
    public string Status { get; set; } = "active";
}
