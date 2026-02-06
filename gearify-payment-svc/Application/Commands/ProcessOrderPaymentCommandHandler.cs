using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Domain.Entities;
using Gearify.PaymentService.Events;
using Gearify.PaymentService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Gearify.PaymentService.Infrastructure.PaymentProviders;
using Gearify.PaymentService.Infrastructure.UnitOfWork;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.PaymentService.Application.Commands;

/// <summary>
/// Handles payment processing triggered by OrderCreatedEvent.
/// </summary>
public class ProcessOrderPaymentCommandHandler : IRequestHandler<ProcessOrderPaymentCommand, ProcessOrderPaymentResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IStripePaymentProvider _stripeProvider;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<ProcessOrderPaymentCommandHandler> _logger;
    private readonly PaymentProviderConfiguration _config;

    public ProcessOrderPaymentCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        IStripePaymentProvider stripeProvider,
        ISnsEventPublisher eventPublisher,
        ILogger<ProcessOrderPaymentCommandHandler> logger,
        IOptions<PaymentProviderConfiguration> config)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _stripeProvider = stripeProvider;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<ProcessOrderPaymentResult> Handle(ProcessOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            // Check if payment already exists for this order (idempotency)
            var existingTransaction = await unitOfWork.Payments.GetTransactionByOrderIdAsync(request.OrderId.ToString());
            if (existingTransaction != null)
            {
                _logger.LogInformation("Payment already exists for order {OrderId}: {TransactionId}",
                    request.OrderId, existingTransaction.Id);

                // Re-publish event if payment was already processed
                if (existingTransaction.Status == PaymentStatus.Succeeded)
                {
                    await PublishPaymentCompletedEvent(existingTransaction, request, cancellationToken);
                }

                return new ProcessOrderPaymentResult(
                    existingTransaction.Status == PaymentStatus.Succeeded,
                    existingTransaction.Id,
                    existingTransaction.ProviderTransactionId);
            }

            // Create transaction record
            var transaction = new PaymentTransaction
            {
                TenantId = request.TenantId,
                OrderId = request.OrderId.ToString(),
                UserId = request.UserId,
                Amount = request.Amount,
                Currency = request.Currency,
                Provider = PaymentProvider.Stripe, // Default to Stripe for auto-processing
                Status = PaymentStatus.Processing,
                IdempotencyKey = $"order-{request.OrderId}"
            };

            await unitOfWork.Payments.CreateTransactionAsync(transaction);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Processing payment for order {OrderId} ({OrderNumber}), amount: {Amount} {Currency}",
                request.OrderId, request.OrderNumber, request.Amount, request.Currency);

            // Process payment via provider
            // In real implementation, we'd need payment method from checkout session
            // For now, using mock provider which auto-approves
            // Configuration: PaymentTestingMode="Slow" triggers 30s delay (for testing deferred cancellation)
            var isSlowMode = _config.PaymentTestingMode.Equals("Slow", StringComparison.OrdinalIgnoreCase);
            var paymentToken = isSlowMode
                ? "test-slow-7777"
                : $"auto-payment-{request.OrderId}";

            if (isSlowMode)
            {
                _logger.LogWarning("Payment testing mode is SLOW - payment will take 30 seconds for order {OrderId}", request.OrderId);
            }

            var (success, providerTransactionId) = await _stripeProvider.ProcessPaymentAsync(
                request.Amount,
                request.Currency,
                paymentToken,
                request.OrderId.ToString()
            );

            // Update transaction
            transaction.Status = success ? PaymentStatus.Succeeded : PaymentStatus.Failed;
            transaction.ProviderTransactionId = providerTransactionId;

            if (!success)
            {
                transaction.ErrorMessage = "Payment processing failed";
            }

            await unitOfWork.Payments.UpdateTransactionAsync(transaction);

            // Record ledger entry if successful
            if (success)
            {
                await unitOfWork.Payments.CreateLedgerEntryAsync(new PaymentLedgerEntry
                {
                    TransactionId = transaction.Id,
                    TenantId = transaction.TenantId,
                    AccountType = "credit",
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    Description = $"Payment for order {request.OrderNumber}"
                });
            }

            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Payment {Status} for order {OrderId}: TransactionId={TransactionId}",
                success ? "completed" : "failed", request.OrderId, transaction.Id);

            // Publish payment event
            if (success)
            {
                await PublishPaymentCompletedEvent(transaction, request, cancellationToken);
            }
            else
            {
                await PublishPaymentFailedEvent(transaction, request, cancellationToken);
            }

            return new ProcessOrderPaymentResult(
                success,
                transaction.Id,
                providerTransactionId,
                success ? null : PaymentErrorCode.PaymentFailed,
                success ? null : "Payment processing failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);

            // Publish failure event
            await _eventPublisher.PublishAsync(new PaymentFailedEvent
            {
                TransactionId = Guid.Empty,
                TenantId = request.TenantId,
                OrderId = request.OrderId,
                OrderNumber = request.OrderNumber,
                UserId = request.UserId,
                Amount = request.Amount,
                Currency = request.Currency,
                Provider = PaymentProvider.Stripe,
                ErrorCode = PaymentErrorCode.Exception,
                ErrorMessage = ex.Message
            }, cancellationToken);

            return new ProcessOrderPaymentResult(false, null, null, PaymentErrorCode.Exception, ex.Message);
        }
    }

    private async Task PublishPaymentCompletedEvent(
        PaymentTransaction transaction,
        ProcessOrderPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var completedEvent = new PaymentCompletedEvent
        {
            TransactionId = transaction.Id,
            TenantId = request.TenantId,
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            UserId = request.UserId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Provider = transaction.Provider,
            ProviderTransactionId = transaction.ProviderTransactionId ?? string.Empty
        };

        await _eventPublisher.PublishAsync(completedEvent, cancellationToken);
    }

    private async Task PublishPaymentFailedEvent(
        PaymentTransaction transaction,
        ProcessOrderPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var failedEvent = new PaymentFailedEvent
        {
            TransactionId = transaction.Id,
            TenantId = request.TenantId,
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            UserId = request.UserId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Provider = transaction.Provider,
            ErrorCode = PaymentErrorCode.PaymentFailed,
            ErrorMessage = transaction.ErrorMessage ?? "Payment processing failed"
        };

        await _eventPublisher.PublishAsync(failedEvent, cancellationToken);
    }
}
