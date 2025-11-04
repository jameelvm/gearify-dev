using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.AuthService.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// SMS service implementation using AWS SNS
/// </summary>
public class SmsService : ISmsService
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<SmsService> _logger;
    private readonly string _fromNumber;

    public SmsService(
        IAmazonSimpleNotificationService snsClient,
        IConfiguration configuration,
        ILogger<SmsService> logger)
    {
        _snsClient = snsClient;
        _logger = logger;
        _fromNumber = configuration["Sms:FromNumber"] ?? "+1234567890";
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            var request = new PublishRequest
            {
                PhoneNumber = phoneNumber,
                Message = message,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        "AWS.SNS.SMS.SenderID",
                        new MessageAttributeValue
                        {
                            StringValue = "Gearify",
                            DataType = "String"
                        }
                    },
                    {
                        "AWS.SNS.SMS.SMSType",
                        new MessageAttributeValue
                        {
                            StringValue = "Transactional",
                            DataType = "String"
                        }
                    }
                }
            };

            var response = await _snsClient.PublishAsync(request);

            _logger.LogInformation("SMS sent successfully to {PhoneNumber}. MessageId: {MessageId}",
                MaskPhoneNumber(phoneNumber), response.MessageId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string code, int expiryMinutes)
    {
        var message = $"Your Gearify verification code is: {code}\n\n" +
                     $"This code will expire in {expiryMinutes} minutes.\n\n" +
                     $"If you didn't request this code, please ignore this message.";

        return await SendSmsAsync(phoneNumber, message);
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        // Mask phone number for logging (show only last 4 digits)
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
        {
            return "****";
        }

        return "****" + phoneNumber.Substring(phoneNumber.Length - 4);
    }
}
