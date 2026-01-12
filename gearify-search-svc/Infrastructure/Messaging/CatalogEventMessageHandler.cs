using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.SearchService.Application.Events;
using Gearify.SearchService.Domain.Events;
using Gearify.SearchService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Gearify.SearchService.Infrastructure.Messaging;

/// <summary>
/// Background service that continuously polls SQS for catalog events
/// and processes them to keep the search index in sync
/// </summary>
public class CatalogEventMessageHandler : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly MessagingSettings _settings;
    private readonly ILogger<CatalogEventMessageHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CatalogEventMessageHandler(
        IAmazonSQS sqsClient,
        IServiceProvider serviceProvider,
        IOptions<MessagingSettings> settings,
        ILogger<CatalogEventMessageHandler> logger)
    {
        _sqsClient = sqsClient;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Catalog event consumer started. Polling queue: {QueueUrl}",
            _settings.SQS.CatalogEventsQueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                _logger.LogInformation("Catalog event consumer stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling SQS queue. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Catalog event consumer stopped");
    }

    private async Task PollAndProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _settings.SQS.CatalogEventsQueueUrl,
            MaxNumberOfMessages = _settings.SQS.MaxNumberOfMessages,
            WaitTimeSeconds = _settings.SQS.WaitTimeSeconds,
            VisibilityTimeout = _settings.SQS.VisibilityTimeoutSeconds,
            AttributeNames = ["All"],
            MessageAttributeNames = ["All"]
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

        if (response.Messages.Count == 0)
        {
            _logger.LogDebug("No messages received from queue");
            return;
        }

        _logger.LogDebug("Received {Count} messages from queue", response.Messages.Count);

        foreach (var message in response.Messages)
        {
            await ProcessMessageAsync(message, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing message: MessageId={MessageId}", message.MessageId);

        try
        {
            // Parse the catalog event from the message
            var catalogEvent = ParseMessage(message.Body);

            if (catalogEvent == null)
            {
                _logger.LogWarning(
                    "Failed to parse message: MessageId={MessageId}",
                    message.MessageId);

                // Delete invalid message to prevent infinite retry
                await DeleteMessageAsync(message, cancellationToken);
                return;
            }

            // Process the event using a scoped service
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ICatalogEventHandler>();

            var success = await handler.HandleAsync(catalogEvent, cancellationToken);

            if (success)
            {
                // Delete message from queue on successful processing
                await DeleteMessageAsync(message, cancellationToken);
                _logger.LogDebug(
                    "Successfully processed and deleted message: MessageId={MessageId}",
                    message.MessageId);
            }
            else
            {
                // Message will return to queue after visibility timeout
                _logger.LogWarning(
                    "Failed to process message, will retry: MessageId={MessageId}",
                    message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing message: MessageId={MessageId}",
                message.MessageId);
            // Message will return to queue after visibility timeout for retry
        }
    }

    private CatalogEvent? ParseMessage(string messageBody)
    {
        try
        {
            // First, try to parse as SNS wrapper (if message came through SNS)
            var snsWrapper = JsonSerializer.Deserialize<SnsMessageWrapper>(messageBody, JsonOptions);

            if (!string.IsNullOrEmpty(snsWrapper?.Message))
            {
                // Message came through SNS, unwrap it
                _logger.LogDebug("Unwrapping SNS message");
                return JsonSerializer.Deserialize<CatalogEvent>(snsWrapper.Message, JsonOptions);
            }

            // Try to parse directly as CatalogEvent (direct SQS message)
            return JsonSerializer.Deserialize<CatalogEvent>(messageBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message: {Body}", messageBody);
            return null;
        }
    }

    private async Task DeleteMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await _sqsClient.DeleteMessageAsync(
                _settings.SQS.CatalogEventsQueueUrl,
                message.ReceiptHandle,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete message: MessageId={MessageId}",
                message.MessageId);
        }
    }
}
