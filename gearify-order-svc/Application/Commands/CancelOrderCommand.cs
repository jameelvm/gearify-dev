using System;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record CancelOrderCommand(
    Guid OrderId,
    string? TenantIdOverride,
    string Reason,
    string? CancelledBy = null
) : IRequest<CancelOrderResult>;

public record CancelOrderResult
{
    public bool Success { get; init; }

    /// <summary>
    /// True when cancellation is deferred (order was in PaymentProcessing status).
    /// The order will be cancelled after payment result is received.
    /// </summary>
    public bool IsPending { get; init; }

    /// <summary>
    /// True when a refund will be/has been initiated (order was paid).
    /// </summary>
    public bool RefundInitiated { get; init; }

    public OrderDto? Order { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }

    public static CancelOrderResult Succeeded(OrderDto order, bool refundInitiated, string? message = null) => new()
    {
        Success = true,
        IsPending = false,
        RefundInitiated = refundInitiated,
        Order = order,
        Message = message ?? (refundInitiated
            ? "Order cancelled. Refund is being processed."
            : "Order cancelled successfully.")
    };

    public static CancelOrderResult Pending(OrderDto order, string message) => new()
    {
        Success = true,
        IsPending = true,
        RefundInitiated = false,
        Order = order,
        Message = message
    };

    public static CancelOrderResult Failed(string errorMessage) => new()
    {
        Success = false,
        IsPending = false,
        RefundInitiated = false,
        ErrorMessage = errorMessage
    };
}
