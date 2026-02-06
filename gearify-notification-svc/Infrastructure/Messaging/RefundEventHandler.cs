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
/// Handles refund events from Payment Service.
/// Sends appropriate email notifications for refund completion or failure.
/// </summary>
public class RefundEventHandler : IEventHandler<RefundEventMessage>
{
    private readonly IAuthServiceClient _authServiceClient;
    private readonly IEmailService _emailService;
    private readonly ILogger<RefundEventHandler> _logger;
    private readonly string _webAppUrl;
    private readonly string _supportUrl;

    public RefundEventHandler(
        IAuthServiceClient authServiceClient,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<RefundEventHandler> logger)
    {
        _authServiceClient = authServiceClient;
        _emailService = emailService;
        _logger = logger;
        _webAppUrl = configuration["WebAppUrl"] ?? "http://localhost:4200";
        _supportUrl = configuration["SupportUrl"] ?? "http://localhost:4200/support";
    }

    public async Task<bool> HandleAsync(RefundEventMessage evt, CancellationToken cancellationToken = default)
    {
        return evt.EventType switch
        {
            "RefundCompletedEvent" => await HandleRefundCompletedAsync(evt, cancellationToken),
            "RefundFailedEvent" => await HandleRefundFailedAsync(evt, cancellationToken),
            _ => LogAndSkip(evt)
        };
    }

    private bool LogAndSkip(RefundEventMessage evt)
    {
        _logger.LogWarning("Unknown refund event type: {EventType} for Order {OrderId}", evt.EventType, evt.OrderId);
        return true;
    }

    /// <summary>
    /// Handles RefundCompletedEvent - sends confirmation email to customer.
    /// </summary>
    private async Task<bool> HandleRefundCompletedAsync(RefundEventMessage evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing RefundCompletedEvent for Order {OrderId} ({OrderNumber}), User {UserId}, Amount {Amount} {Currency}",
            evt.OrderId, evt.OrderNumber, evt.UserId, evt.RefundAmount, evt.Currency);

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
                { "RefundAmount", evt.RefundAmount.ToString("F2") },
                { "Currency", evt.Currency },
                { "CancellationReason", evt.Reason },
                { "RefundId", evt.RefundId?.ToString() ?? evt.TransactionId.ToString() },
                { "OrderLink", $"{_webAppUrl}/account/orders" }
            };

            await _emailService.SendTemplatedEmailAsync(
                user.Email,
                "OrderCancelledRefunded",
                templateData,
                cancellationToken);

            _logger.LogInformation(
                "Refund confirmation email sent to {Email} for Order {OrderNumber}. Refund: {Amount} {Currency}",
                user.Email, evt.OrderNumber, evt.RefundAmount, evt.Currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending refund confirmation email for Order {OrderId}", evt.OrderId);
        }

        return true;
    }

    /// <summary>
    /// Handles RefundFailedEvent - sends alert email to customer and admin.
    /// </summary>
    private async Task<bool> HandleRefundFailedAsync(RefundEventMessage evt, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Processing RefundFailedEvent for Order {OrderId} ({OrderNumber}), User {UserId}. Error: {ErrorMessage}",
            evt.OrderId, evt.OrderNumber, evt.UserId, evt.ErrorMessage);

        try
        {
            // Send email to customer
            await SendRefundFailedEmailToCustomerAsync(evt, cancellationToken);

            // Log for admin alerting (could be extended to send admin email or Slack notification)
            _logger.LogError(
                "ADMIN ALERT: Refund failed for Order {OrderNumber} (ID: {OrderId}). " +
                "Amount: {Amount} {Currency}. Error: {ErrorCode} - {ErrorMessage}. Retry count: {RetryCount}",
                evt.OrderNumber, evt.OrderId, evt.Amount, evt.Currency,
                evt.ErrorCode, evt.ErrorMessage, evt.RetryCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending refund failed notification for Order {OrderId}", evt.OrderId);
        }

        return true;
    }

    private async Task SendRefundFailedEmailToCustomerAsync(RefundEventMessage evt, CancellationToken cancellationToken)
    {
        var user = await _authServiceClient.GetUserByIdAsync(evt.UserId, evt.TenantId, cancellationToken);

        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Could not retrieve user {UserId} from Auth Service. Skipping customer email.",
                evt.UserId);
            return;
        }

        var templateData = new Dictionary<string, string>
        {
            { "FirstName", user.FirstName },
            { "OrderNumber", evt.OrderNumber },
            { "Amount", evt.Amount.ToString("F2") },
            { "Currency", evt.Currency },
            { "ErrorMessage", GetUserFriendlyErrorMessage(evt.ErrorCode, evt.ErrorMessage) },
            { "SupportLink", _supportUrl },
            { "OrderLink", $"{_webAppUrl}/account/orders" }
        };

        await _emailService.SendTemplatedEmailAsync(
            user.Email,
            "RefundFailed",
            templateData,
            cancellationToken);

        _logger.LogInformation(
            "Refund failed notification email sent to {Email} for Order {OrderNumber}",
            user.Email, evt.OrderNumber);
    }

    /// <summary>
    /// Converts technical error codes/messages to user-friendly messages.
    /// </summary>
    private static string GetUserFriendlyErrorMessage(string? errorCode, string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorCode) && string.IsNullOrEmpty(errorMessage))
        {
            return "We encountered a technical issue while processing your refund.";
        }

        return errorCode?.ToLower() switch
        {
            "card_expired" => "The original payment method has expired. Please contact support.",
            "insufficient_funds" => "We were unable to process the refund due to a payment provider issue.",
            "provider_error" => "Our payment provider encountered an issue. Our team is working to resolve this.",
            _ => errorMessage ?? "We encountered an issue while processing your refund."
        };
    }
}
