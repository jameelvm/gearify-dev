using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gearify.NotificationService.Infrastructure.Clients;
using Gearify.NotificationService.Infrastructure.Email;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.NotificationService.Infrastructure.Messaging;

/// <summary>
/// Handles payment events from Payment Service.
/// Routes to appropriate email notification based on EventType.
/// </summary>
public class PaymentEventHandler : IEventHandler<PaymentEventMessage>
{
    private readonly IAuthServiceClient _authServiceClient;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentEventHandler> _logger;
    private readonly string _webAppUrl;

    public PaymentEventHandler(
        IAuthServiceClient authServiceClient,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PaymentEventHandler> logger)
    {
        _authServiceClient = authServiceClient;
        _emailService = emailService;
        _logger = logger;
        _webAppUrl = configuration["WebAppUrl"] ?? "http://localhost:4200";
    }

    public async Task<bool> HandleAsync(PaymentEventMessage evt, CancellationToken cancellationToken = default)
    {
        return evt.EventType switch
        {
            "PaymentCompletedEvent" => await HandlePaymentCompletedAsync(evt, cancellationToken),
            "PaymentFailedEvent" => await HandlePaymentFailedAsync(evt, cancellationToken),
            _ =>
                LogAndSkip(evt)
        };
    }

    private bool LogAndSkip(PaymentEventMessage evt)
    {
        _logger.LogWarning("Unknown payment event type: {EventType} for Order {OrderId}", evt.EventType, evt.OrderId);
        return true;
    }

    private async Task<bool> HandlePaymentCompletedAsync(PaymentEventMessage evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing PaymentCompletedEvent for Order {OrderId} ({OrderNumber}), User {UserId}",
            evt.OrderId, evt.OrderNumber, evt.UserId);

        try
        {
            var user = await _authServiceClient.GetUserByIdAsync(evt.UserId, evt.TenantId, cancellationToken);

            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("Could not retrieve user {UserId} from Auth Service. Skipping email notification.",
                    evt.UserId);
                return true;
            }

            var templateData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "OrderNumber", evt.OrderNumber },
                { "Amount", evt.Amount.ToString("F2") },
                { "Currency", evt.Currency },
                { "OrderLink", $"{_webAppUrl}/account/orders" }
            };

            await _emailService.SendTemplatedEmailAsync(
                user.Email,
                "PaymentSucceeded",
                templateData,
                cancellationToken);

            _logger.LogInformation("Payment success notification email sent to {Email} for Order {OrderNumber}",
                user.Email, evt.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment success notification for Order {OrderId}", evt.OrderId);
        }

        return true;
    }

    private async Task<bool> HandlePaymentFailedAsync(PaymentEventMessage evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing PaymentFailedEvent for Order {OrderId} ({OrderNumber}), User {UserId}",
            evt.OrderId, evt.OrderNumber, evt.UserId);

        try
        {
            var user = await _authServiceClient.GetUserByIdAsync(evt.UserId, evt.TenantId, cancellationToken);

            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("Could not retrieve user {UserId} from Auth Service. Skipping email notification.",
                    evt.UserId);
                return true;
            }

            var templateData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "OrderNumber", evt.OrderNumber },
                { "Amount", evt.Amount.ToString("F2") },
                { "Currency", evt.Currency },
                { "ErrorMessage", evt.ErrorMessage ?? "Payment processing failed" },
                { "RetryLink", $"{_webAppUrl}/account/orders" }
            };

            await _emailService.SendTemplatedEmailAsync(
                user.Email,
                "PaymentFailed",
                templateData,
                cancellationToken);

            _logger.LogInformation("Payment failed notification email sent to {Email} for Order {OrderNumber}",
                user.Email, evt.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment failed notification for Order {OrderId}", evt.OrderId);
        }

        return true;
    }
}
