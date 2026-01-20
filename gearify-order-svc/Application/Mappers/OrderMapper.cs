using System;
using System.Linq;
using System.Text.Json;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Domain.Entities;

namespace Gearify.OrderService.Application.Mappers;

public static class OrderMapper
{
    public static OrderDto ToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            TenantId = order.TenantId,
            UserId = order.UserId,
            Status = order.Status.ToString(),
            Items = order.Items.Select(ToItemDto).ToList(),
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            ShippingAddress = ParseAddress(order.ShippingAddress),
            BillingAddress = ParseAddress(order.BillingAddress),
            PaymentId = order.PaymentId,
            PaymentStatus = order.PaymentStatus,
            ShipmentId = order.ShipmentId,
            ShippingStatus = order.ShippingStatus,
            SagaState = order.SagaState.ToString(),
            StatusHistory = order.StatusHistory.Select(ToHistoryDto).ToList(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt
        };
    }

    public static OrderSummaryDto ToSummaryDto(Order order)
    {
        return new OrderSummaryDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            ItemCount = order.Items.Sum(i => i.Quantity),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAt = order.CreatedAt
        };
    }

    public static OrderItemDto ToItemDto(OrderItem item)
    {
        return new OrderItemDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductSku = item.ProductSku,
            ProductName = item.ProductName,
            ProductImageUrl = item.ProductImageUrl,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountAmount = item.DiscountAmount,
            TotalPrice = item.TotalPrice
        };
    }

    public static OrderStatusHistoryDto ToHistoryDto(OrderStatusHistory history)
    {
        return new OrderStatusHistoryDto
        {
            FromStatus = history.FromStatus.ToString(),
            ToStatus = history.ToStatus.ToString(),
            Reason = history.Reason,
            ChangedBy = history.ChangedBy,
            CreatedAt = history.CreatedAt
        };
    }

    public static OrderItem ToEntity(CreateOrderItemRequest request, Guid orderId)
    {
        return new OrderItem
        {
            OrderId = orderId,
            ProductId = request.ProductId,
            ProductSku = request.ProductSku,
            ProductName = request.ProductName,
            ProductImageUrl = request.ProductImageUrl,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalPrice = request.UnitPrice * request.Quantity
        };
    }

    private static AddressDto? ParseAddress(JsonDocument? addressJson)
    {
        if (addressJson == null)
            return null;

        try
        {
            var root = addressJson.RootElement;
            return new AddressDto
            {
                AddressId = GetStringProperty(root, "addressId"),
                FullName = GetStringProperty(root, "fullName") ?? "",
                Street = GetStringProperty(root, "street") ?? "",
                Street2 = GetStringProperty(root, "street2"),
                City = GetStringProperty(root, "city") ?? "",
                State = GetStringProperty(root, "state") ?? "",
                PostalCode = GetStringProperty(root, "postalCode") ?? "",
                Country = GetStringProperty(root, "country") ?? "",
                Phone = GetStringProperty(root, "phone")
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    public static JsonDocument? ToJsonDocument(AddressDto? address)
    {
        if (address == null)
            return null;

        var json = JsonSerializer.Serialize(new
        {
            addressId = address.AddressId,
            fullName = address.FullName,
            street = address.Street,
            street2 = address.Street2,
            city = address.City,
            state = address.State,
            postalCode = address.PostalCode,
            country = address.Country,
            phone = address.Phone
        });

        return JsonDocument.Parse(json);
    }
}
