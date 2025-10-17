using Gearify.PaymentService.Domain.Entities;
using Gearify.PaymentService.Infrastructure.PaymentProviders;
using Gearify.PaymentService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Application.Commands;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private readonly IPaymentRepository _repository;
    private readonly IIdempotencyService _idempotency;
    private readonly IStripePaymentProvider _stripeProvider;
    private readonly IPayPalPaymentProvider _paypalProvider;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IPaymentRepository repository,
        IIdempotencyService idempotency,
        IStripePaymentProvider stripeProvider,
        IPayPalPaymentProvider paypalProvider,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _repository = repository;
        _idempotency = idempotency;
        _stripeProvider = stripeProvider;
        _paypalProvider = paypalProvider;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check idempotency
            var existingResult = await _idempotency.GetResultAsync(request.IdempotencyKey);
            if (existingResult != null)
            {
                _logger.LogInformation("Returning cached result for idempotency key {Key}", request.IdempotencyKey);
                return existingResult;
            }

            // Create transaction record
            var transaction = new PaymentTransaction
            {
                TenantId = request.TenantId,
                OrderId = request.OrderId,
                UserId = request.UserId,
                Amount = request.Amount,
                Currency = request.Currency,
                Provider = request.Provider,
                Status = PaymentStatus.Processing,
                IdempotencyKey = request.IdempotencyKey
            };

            await _repository.CreateTransactionAsync(transaction);

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
            else // PayPal
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
            transaction.UpdatedAt = DateTime.UtcNow;

            if (!success)
            {
                transaction.ErrorMessage = "Payment processing failed";
            }

            await _repository.UpdateTransactionAsync(transaction);

            // Record ledger entry if successful
            if (success)
            {
                await _repository.CreateLedgerEntryAsync(new PaymentLedgerEntry
                {
                    TransactionId = transaction.Id,
                    TenantId = transaction.TenantId,
                    AccountType = "credit",
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    Description = $"Payment for order {request.OrderId}"
                });
            }

            var result = new ProcessPaymentResult(success, transaction.Id, transaction.Status);

            // Cache result for idempotency
            await _idempotency.SaveResultAsync(request.IdempotencyKey, result, TimeSpan.FromHours(24));

            _logger.LogInformation("Payment processed: {TransactionId}, Status: {Status}", transaction.Id, transaction.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);
            return new ProcessPaymentResult(false, null, PaymentStatus.Failed, ex.Message);
        }
    }
}
