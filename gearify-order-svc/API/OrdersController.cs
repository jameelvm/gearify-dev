using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.API.Models;
using Gearify.OrderService.Application.Commands;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.API;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IMediator mediator, ILogger<OrdersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(ModelState));
        }

        var command = new CreateOrderCommand(
            request.UserId,
            request.Items,
            request.ShippingAddress,
            request.BillingAddress,
            request.Subtotal,
            request.TaxAmount,
            request.ShippingAmount,
            request.DiscountAmount,
            request.Currency
        );

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return UnprocessableEntity(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("Order {OrderNumber} created successfully", result.Order!.OrderNumber);

        return CreatedAtAction(nameof(GetOrderById), new { id = result.Order.Id }, result.Order);
    }

    /// <summary>
    /// Get an order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id);
        var order = await _mediator.Send(query, cancellationToken);

        if (order == null)
        {
            return NotFound(new { error = $"Order with ID '{id}' was not found" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Get an order by order number
    /// </summary>
    [HttpGet("by-number/{orderNumber}")]
    public async Task<IActionResult> GetOrderByNumber(string orderNumber, CancellationToken cancellationToken)
    {
        var query = new GetOrderByNumberQuery(orderNumber);
        var order = await _mediator.Send(query, cancellationToken);

        if (order == null)
        {
            return NotFound(new { error = $"Order with number '{orderNumber}' was not found" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Get orders for a specific user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "User ID is required" });
        }

        var query = new GetOrdersByUserQuery(userId);
        var orders = await _mediator.Send(query, cancellationToken);

        return Ok(new OrderListResponse
        {
            Orders = orders,
            TotalCount = orders.Count
        });
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(id, request.Reason, request.CancelledBy);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            return Conflict(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", id, request.Reason);
        return Ok(result.Order);
    }

    /// <summary>
    /// Update order status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Domain.Entities.OrderStatus>(request.Status, true, out var newStatus))
        {
            return BadRequest(new { error = $"'{request.Status}' is not a valid order status" });
        }

        var command = new UpdateOrderStatusCommand(id, newStatus, request.Reason, request.ChangedBy);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("Order {OrderId} status updated to {Status}", id, request.Status);
        return Ok(result.Order);
    }
}
