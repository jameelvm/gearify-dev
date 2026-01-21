using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.API.Models;
using Gearify.PaymentService.Application.Commands;
using Gearify.PaymentService.Application.DTOs;
using Gearify.PaymentService.Application.Queries;
using Gearify.PaymentService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.API;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Process a new payment
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(ModelState));
        }

        if (!Enum.TryParse<PaymentProvider>(request.Provider, true, out var provider))
        {
            return BadRequest(new { error = $"'{request.Provider}' is not a valid payment provider. Use 'Stripe' or 'PayPal'." });
        }

        var command = new ProcessPaymentCommand(
            request.OrderId,
            request.UserId,
            request.Amount,
            request.Currency,
            provider,
            request.PaymentMethodToken,
            request.IdempotencyKey
        );

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) == true ||
                result.ErrorMessage?.Contains("already processed", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new { error = result.ErrorMessage });
            }
            return UnprocessableEntity(new { error = result.ErrorMessage });
        }

        _logger.LogInformation(
            "Payment {PaymentId} processed for order {OrderId}. Provider: {Provider}, Amount: {Amount} {Currency}",
            result.Payment!.Id, request.OrderId, request.Provider, request.Amount, request.Currency);

        return CreatedAtAction(nameof(GetPaymentById), new { id = result.Payment.Id }, result.Payment);
    }

    /// <summary>
    /// Get a payment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPaymentById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPaymentByIdQuery(id);
        var payment = await _mediator.Send(query, cancellationToken);

        if (payment == null)
        {
            return NotFound(new { error = $"Payment with ID '{id}' was not found" });
        }

        return Ok(payment);
    }

    /// <summary>
    /// Get payment for a specific order
    /// </summary>
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetPaymentByOrderId(string orderId, CancellationToken cancellationToken)
    {
        var query = new GetPaymentByOrderIdQuery(orderId);
        var payment = await _mediator.Send(query, cancellationToken);

        if (payment == null)
        {
            return NotFound(new { error = $"No payment found for order '{orderId}'" });
        }

        return Ok(payment);
    }

    /// <summary>
    /// Get payments for a specific user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "User ID is required" });
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = new GetPaymentsByUserQuery(userId, page, pageSize);
        var payments = await _mediator.Send(query, cancellationToken);

        return Ok(payments);
    }

    /// <summary>
    /// Process a refund for a payment
    /// </summary>
    [HttpPost("{id:guid}/refunds")]
    public async Task<IActionResult> ProcessRefund(
        Guid id,
        [FromBody] ProcessRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(ModelState));
        }

        var command = new RefundPaymentCommand(id, request.Amount, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            if (result.ErrorMessage?.Contains("exceeds", StringComparison.OrdinalIgnoreCase) == true ||
                result.ErrorMessage?.Contains("invalid amount", StringComparison.OrdinalIgnoreCase) == true)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }
            return UnprocessableEntity(new { error = result.ErrorMessage });
        }

        _logger.LogInformation(
            "Refund {RefundId} processed for payment {PaymentId}. Amount: {Amount}. Reason: {Reason}",
            result.Refund!.Id, id, request.Amount, request.Reason);

        return Created($"/api/payments/{id}/refunds/{result.Refund.Id}", result.Refund);
    }
}
