using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Mappers;
using Gearify.PaymentService.Domain.Entities;
using Gearify.PaymentService.Infrastructure.PaymentProviders;
using Gearify.PaymentService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Application.Commands;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IIdempotencyService _idempotency;
    private readonly IStripePaymentProvider _stripeProvider;
    private readonly IPayPalPaymentProvider _paypalProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        IIdempotencyService idempotency,
        IStripePaymentProvider stripeProvider,
        IPayPalPaymentProvider paypalProvider,
        ITenantContext tenantContext,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _idempotency = idempotency;
        _stripeProvider = stripeProvider;
        _paypalProvider = paypalProvider;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var tenantId = _tenantContext.TenantId;

            // Check idempotency
            var existingTransaction = await unitOfWork.Payments.GetByIdempotencyKeyAsync(request.IdempotencyKey);
            if (existingTransaction != null)
            {
                _logger.LogInformation("Returning existing result for idempotency key {Key}", request.IdempotencyKey);
                return new ProcessPaymentResult(
                    existingTransaction.Status == PaymentStatus.Succeeded,
                    PaymentMapper.ToDto(existingTransaction));
            }

            // Create transaction record
            var transaction = new PaymentTransaction
            {
                TenantId = tenantId,
                OrderId = request.OrderId,
                UserId = request.UserId,
                Amount = request.Amount,
                Currency = request.Currency,
                Provider = request.Provider,
                Status = PaymentStatus.Processing,
                IdempotencyKey = request.IdempotencyKey
            };

            await unitOfWork.Payments.CreateTransactionAsync(transaction);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Process payment based on provider
            string? providerTransactionId;
            bool success;

            if (request.Provider == PaymentProvider.Stripe)
            {
                (success, providerTransactionId) = await _stripeProvider.ProcessPaymentAsync(
                    request.Amount,
                    request.Currency,
                    request.PaymentMethodToken,
                    request.OrderId
                );
            }
            else
            {
                (success, providerTransactionId) = await _paypalProvider.ProcessPaymentAsync(
                    request.Amount,
                    request.Currency,
                    request.PaymentMethodToken,
                    request.OrderId
                );
            }

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
                    Description = $"Payment for order {request.OrderId}"
                });
            }

            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Payment processed: {TransactionId}, Status: {Status}",
                transaction.Id, transaction.Status);

            return new ProcessPaymentResult(success, PaymentMapper.ToDto(transaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);
            return new ProcessPaymentResult(false, null, ex.Message);
        }
    }
}
