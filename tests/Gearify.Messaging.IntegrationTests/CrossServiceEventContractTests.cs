using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Gearify.SharedKernel.Events;
using Xunit;

using OrderPublisherEvents = Gearify.OrderService.Events;
using PaymentPublisherEvents = Gearify.PaymentService.Events;
using OrderInboundEvents = Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using PaymentInboundEvents = Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;

namespace Gearify.Messaging.IntegrationTests;

/// <summary>
/// Contract tests that verify publisher event types can be deserialized
/// into consumer event types across service boundaries.
/// Catches serialization mismatches, missing properties, and type incompatibilities.
/// </summary>
public class CrossServiceEventContractTests
{
    private static readonly JsonSerializerOptions PublisherOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ConsumerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void OrderCreatedEvent_Publisher_CompatibleWith_Consumer()
    {
        // Arrange — Order Service publishes this full event
        var publisherEvent = new OrderPublisherEvents.OrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-2024-0001",
            TenantId = "tenant-contract",
            UserId = "user-123",
            GuestId = null,
            CheckoutId = Guid.NewGuid(),
            Items = new List<OrderPublisherEvents.OrderCreatedEvent.OrderItemInfo>
            {
                new()
                {
                    ProductId = "prod-1",
                    ProductName = "Widget",
                    VariantId = "var-1",
                    Quantity = 2,
                    UnitPrice = 25.00m,
                    TotalPrice = 50.00m
                }
            },
            Subtotal = 50.00m,
            Tax = 4.50m,
            ShippingCost = 5.99m,
            Total = 60.49m,
            Currency = "USD",
            ShippingAddress = new OrderPublisherEvents.OrderCreatedEvent.OrderAddressInfo
            {
                FullName = "John Doe",
                Street = "123 Main St",
                City = "Springfield",
                State = "IL",
                PostalCode = "62701",
                Country = "US"
            },
            OccurredAt = DateTime.UtcNow
        };

        // Act — wrap in EventEnvelope, serialize with publisher options,
        // then deserialize with consumer options (mimics the full SNS→SQS flow)
        var envelope = EventEnvelope.Wrap(publisherEvent, publisherEvent.TenantId, "corr-123");
        var envelopeJson = JsonSerializer.Serialize(envelope, PublisherOptions);
        var deserializedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(envelopeJson, ConsumerOptions)!;
        var payloadJson = JsonSerializer.Serialize(deserializedEnvelope.Payload, ConsumerOptions);
        var consumerEvent = JsonSerializer.Deserialize<PaymentInboundEvents.OrderCreatedEvent>(payloadJson, ConsumerOptions);

        // Assert — consumer receives the fields it needs
        consumerEvent.Should().NotBeNull();
        consumerEvent!.OrderId.Should().Be(publisherEvent.OrderId);
        consumerEvent.OrderNumber.Should().Be("ORD-2024-0001");
        consumerEvent.TenantId.Should().Be("tenant-contract");
        consumerEvent.UserId.Should().Be("user-123");
        consumerEvent.Total.Should().Be(60.49m);
        consumerEvent.Currency.Should().Be("USD");
    }

    [Fact]
    public void PaymentCompletedEvent_Publisher_CompatibleWith_Consumer()
    {
        // Arrange — Payment Service publishes this event
        var publisherEvent = new PaymentPublisherEvents.PaymentCompletedEvent
        {
            TransactionId = Guid.NewGuid(),
            TenantId = "tenant-pay",
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-2024-0002",
            UserId = "user-456",
            Amount = 150.00m,
            Currency = "USD",
            Provider = Gearify.PaymentService.Domain.Entities.PaymentProvider.Stripe,
            ProviderTransactionId = "pi_abc123",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        var envelope = EventEnvelope.Wrap(publisherEvent, publisherEvent.TenantId, "corr-456");
        var envelopeJson = JsonSerializer.Serialize(envelope, PublisherOptions);
        var deserializedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(envelopeJson, ConsumerOptions)!;
        var payloadJson = JsonSerializer.Serialize(deserializedEnvelope.Payload, ConsumerOptions);
        var consumerEvent = JsonSerializer.Deserialize<OrderInboundEvents.PaymentCompletedEvent>(payloadJson, ConsumerOptions);

        // Assert
        consumerEvent.Should().NotBeNull();
        consumerEvent!.TransactionId.Should().Be(publisherEvent.TransactionId);
        consumerEvent.TenantId.Should().Be("tenant-pay");
        consumerEvent.OrderId.Should().Be(publisherEvent.OrderId);
        consumerEvent.OrderNumber.Should().Be("ORD-2024-0002");
        consumerEvent.UserId.Should().Be("user-456");
        consumerEvent.Amount.Should().Be(150.00m);
        consumerEvent.Currency.Should().Be("USD");
        // Provider is enum on publisher side, string on consumer side
        consumerEvent.Provider.Should().Be("Stripe");
        consumerEvent.ProviderTransactionId.Should().Be("pi_abc123");
    }

    [Fact]
    public void PaymentFailedEvent_Publisher_CompatibleWith_Consumer()
    {
        // Arrange
        var publisherEvent = new PaymentPublisherEvents.PaymentFailedEvent
        {
            TransactionId = Guid.NewGuid(),
            TenantId = "tenant-fail",
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-2024-0003",
            UserId = "user-789",
            Amount = 99.99m,
            Currency = "USD",
            Provider = Gearify.PaymentService.Domain.Entities.PaymentProvider.PayPal,
            ErrorCode = Gearify.PaymentService.Domain.Entities.PaymentErrorCode.InsufficientFunds,
            ErrorMessage = "Insufficient funds in account",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        var envelope = EventEnvelope.Wrap(publisherEvent, publisherEvent.TenantId, "corr-789");
        var envelopeJson = JsonSerializer.Serialize(envelope, PublisherOptions);
        var deserializedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(envelopeJson, ConsumerOptions)!;
        var payloadJson = JsonSerializer.Serialize(deserializedEnvelope.Payload, ConsumerOptions);
        var consumerEvent = JsonSerializer.Deserialize<OrderInboundEvents.PaymentFailedEvent>(payloadJson, ConsumerOptions);

        // Assert
        consumerEvent.Should().NotBeNull();
        consumerEvent!.TransactionId.Should().Be(publisherEvent.TransactionId);
        consumerEvent.TenantId.Should().Be("tenant-fail");
        consumerEvent.OrderId.Should().Be(publisherEvent.OrderId);
        consumerEvent.OrderNumber.Should().Be("ORD-2024-0003");
        consumerEvent.UserId.Should().Be("user-789");
        consumerEvent.Amount.Should().Be(99.99m);
        consumerEvent.Currency.Should().Be("USD");
        // Enum → string conversion
        consumerEvent.Provider.Should().Be("PayPal");
        consumerEvent.ErrorCode.Should().Be("InsufficientFunds");
        consumerEvent.ErrorMessage.Should().Be("Insufficient funds in account");
    }

    [Fact]
    public void OrderCancelledEvent_Publisher_CompatibleWith_Consumer()
    {
        // Arrange
        var publisherEvent = new OrderPublisherEvents.OrderCancelledEvent
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-2024-0004",
            TenantId = "tenant-cancel",
            UserId = "user-cancel",
            Reason = "Customer requested cancellation",
            CancelledBy = "user-cancel",
            PaymentId = Guid.NewGuid(),
            PaidAmount = 200.00m,
            Currency = "USD",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        var envelope = EventEnvelope.Wrap(publisherEvent, publisherEvent.TenantId, "corr-cancel");
        var envelopeJson = JsonSerializer.Serialize(envelope, PublisherOptions);
        var deserializedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(envelopeJson, ConsumerOptions)!;
        var payloadJson = JsonSerializer.Serialize(deserializedEnvelope.Payload, ConsumerOptions);
        var consumerEvent = JsonSerializer.Deserialize<PaymentInboundEvents.OrderCancelledEvent>(payloadJson, ConsumerOptions);

        // Assert
        consumerEvent.Should().NotBeNull();
        consumerEvent!.OrderId.Should().Be(publisherEvent.OrderId);
        consumerEvent.OrderNumber.Should().Be("ORD-2024-0004");
        consumerEvent.TenantId.Should().Be("tenant-cancel");
        consumerEvent.UserId.Should().Be("user-cancel");
        consumerEvent.Reason.Should().Be("Customer requested cancellation");
        consumerEvent.CancelledBy.Should().Be("user-cancel");
        consumerEvent.PaymentId.Should().Be(publisherEvent.PaymentId);
        consumerEvent.PaidAmount.Should().Be(200.00m);
        consumerEvent.Currency.Should().Be("USD");
    }
}
