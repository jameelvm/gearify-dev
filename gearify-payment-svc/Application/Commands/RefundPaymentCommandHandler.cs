using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Mappers;
using Gearify.PaymentService.Domain.Entities;
using Gearify.PaymentService.Events;
using Gearify.PaymentService.Infrastructure.PaymentProviders;
using Gearify.PaymentService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Application.Commands;

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, RefundPaymentResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IStripePaymentProvider _stripeProvider;
    private readonly IPayPalPaymentProvider _paypalProvider;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        IStripePaymentProvider stripeProvider,
        IPayPalPaymentProvider paypalProvider,
        ISnsEventPublisher eventPublisher,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _stripeProvider = stripeProvider;
        _paypalProvider = paypalProvider;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<RefundPaymentResult> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var transaction = await unitOfWork.Payments.GetTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                return new RefundPaymentResult(false, null, "Transaction not found");
            }

            // Verify tenant matches (use request.TenantId for event-driven context)
            if (transaction.TenantId != request.TenantId)
            {
                _logger.LogWarning(
                    "Tenant mismatch for refund: request TenantId={RequestTenantId}, transaction TenantId={TransactionTenantId}",
                    request.TenantId, transaction.TenantId);
                return new RefundPaymentResult(false, null, "Transaction not found");
            }

            if (transaction.Status != PaymentStatus.Succeeded)
            {
                return new RefundPaymentResult(false, null, "Only successful payments can be refunded");
            }

            // Check if refund amount is valid
            var totalRefunded = await unitOfWork.Payments.GetTotalRefundedAmountAsync(request.TransactionId);
            var availableForRefund = transaction.Amount - totalRefunded;

            if (request.Amount > availableForRefund)
            {
                return new RefundPaymentResult(false, null,
                    $"Refund amount exceeds available amount. Available: {availableForRefund}");
            }

            // Create refund record
            var refund = new Refund
            {
                TransactionId = request.TransactionId,
                TenantId = request.TenantId,
                Amount = request.Amount,
                Currency = transaction.Currency,
                Status = RefundStatus.Processing,
                Reason = request.Reason
            };

            await unitOfWork.Payments.CreateRefundAsync(refund);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Process refund with provider
            bool success;
            if (transaction.Provider == PaymentProvider.Stripe)
            {
                success = await _stripeProvider.RefundPaymentAsync(
                    transaction.ProviderTransactionId!,
                    request.Amount
                );
            }
            else
            {
                success = await _paypalProvider.RefundPaymentAsync(
                    transaction.ProviderTransactionId!,
                    request.Amount
                );
            }

            // Update refund status
            refund.Status = success ? RefundStatus.Succeeded : RefundStatus.Failed;
            if (success)
            {
                refund.CompletedAt = DateTime.UtcNow;
            }

            await unitOfWork.Payments.UpdateRefundAsync(refund);

            // Update transaction status if fully refunded
            if (success)
            {
                var newTotalRefunded = totalRefunded + request.Amount;
                if (newTotalRefunded >= transaction.Amount)
                {
                    transaction.Status = PaymentStatus.Refunded;
                }
                else
                {
                    transaction.Status = PaymentStatus.PartiallyRefunded;
                }
                await unitOfWork.Payments.UpdateTransactionAsync(transaction);

                // Record ledger entry
                await unitOfWork.Payments.CreateLedgerEntryAsync(new PaymentLedgerEntry
                {
                    TransactionId = transaction.Id,
                    TenantId = request.TenantId,
                    AccountType = "debit",
                    Amount = request.Amount,
                    Currency = transaction.Currency,
                    Description = $"Refund for order {transaction.OrderId}: {request.Reason}"
                });
            }

            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Refund processed: {RefundId}, Status: {Status}",
                refund.Id, refund.Status);

            // Publish RefundCompletedEvent on success
            if (success)
            {
                await PublishRefundCompletedEventAsync(
                    refund, transaction, request, cancellationToken);
            }

            return new RefundPaymentResult(success, PaymentMapper.ToRefundDto(refund));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process refund for transaction {TransactionId}", request.TransactionId);
            return new RefundPaymentResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Publishes RefundCompletedEvent to notify Order Service and Notification Service.
    /// </summary>
    private async Task PublishRefundCompletedEventAsync(
        Refund refund,
        PaymentTransaction transaction,
        RefundPaymentCommand request,
        CancellationToken cancellationToken)
    {
        // Parse OrderId - transaction stores it as string but event needs Guid
        var orderId = request.OrderId ??
            (Guid.TryParse(transaction.OrderId, out var parsedOrderId) ? parsedOrderId : Guid.Empty);

        var evt = new RefundCompletedEvent
        {
            TenantId = transaction.TenantId,
            RefundId = refund.Id,
            OriginalTransactionId = transaction.Id,
            OrderId = orderId,
            OrderNumber = request.OrderNumber ?? "",
            UserId = transaction.UserId,
            RefundAmount = refund.Amount,
            OriginalAmount = transaction.Amount,
            Currency = refund.Currency,
            Reason = refund.Reason,
            ProviderRefundId = refund.ProviderRefundId,
            OccurredAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(evt, cancellationToken);

        _logger.LogInformation(
            "Published RefundCompletedEvent. RefundId: {RefundId}, OrderId: {OrderId}, Amount: {Amount} {Currency}",
            evt.RefundId, evt.OrderId, evt.RefundAmount, evt.Currency);
    }
}
