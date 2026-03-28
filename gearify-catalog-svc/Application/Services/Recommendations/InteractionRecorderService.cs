using Amazon.PersonalizeEvents;
using Amazon.PersonalizeEvents.Model;
using Gearify.SharedKernel.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class InteractionRecorderService : IInteractionRecorderService
{
    private readonly IAmazonPersonalizeEvents _personalizeEvents;
    private readonly ILogger<InteractionRecorderService> _logger;
    private readonly AIServiceConfiguration _config;

    public InteractionRecorderService(
        IAmazonPersonalizeEvents personalizeEvents,
        ILogger<InteractionRecorderService> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _personalizeEvents = personalizeEvents;
        _logger = logger;
        _config = config.Value;
    }

    public async Task RecordInteractionAsync(
        string userId, string itemId, string eventType, decimal? eventValue = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.PersonalizeEventTrackerArn))
            return;

        try
        {
            await _personalizeEvents.PutEventsAsync(new PutEventsRequest
            {
                TrackingId = _config.PersonalizeEventTrackerArn,
                UserId = userId,
                SessionId = Guid.NewGuid().ToString(),
                EventList = new List<Event>
                {
                    new()
                    {
                        EventType = eventType,
                        ItemId = itemId,
                        SentAt = DateTime.UtcNow,
                        EventValue = eventValue.HasValue ? (float)eventValue.Value : 0f
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record Personalize interaction for user {UserId}", userId);
        }
    }
}
