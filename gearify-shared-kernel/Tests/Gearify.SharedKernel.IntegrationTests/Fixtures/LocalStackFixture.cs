using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Testcontainers.LocalStack;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests.Fixtures;

public class LocalStackFixture : IAsyncLifetime
{
    private readonly LocalStackContainer _container = new LocalStackBuilder()
        .WithImage("localstack/localstack:3.0")
        .Build();

    public IAmazonSimpleNotificationService SnsClient { get; private set; } = null!;
    public IAmazonSQS SqsClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var credentials = new BasicAWSCredentials("test", "test");
        var endpoint = _container.GetConnectionString();

        SnsClient = new AmazonSimpleNotificationServiceClient(credentials, new AmazonSimpleNotificationServiceConfig
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1"
        });

        SqsClient = new AmazonSQSClient(credentials, new AmazonSQSConfig
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1"
        });
    }

    /// <summary>
    /// Creates an SNS topic, SQS queue, and subscribes the queue to the topic
    /// with an optional EventType filter policy.
    /// Returns (topicArn, queueUrl).
    /// </summary>
    public async Task<(string TopicArn, string QueueUrl)> SetupTopicWithQueue(
        string topicName,
        string queueName,
        string? eventTypeFilter = null)
    {
        var topicResponse = await SnsClient.CreateTopicAsync(new CreateTopicRequest { Name = topicName });
        var topicArn = topicResponse.TopicArn;

        var queueResponse = await SqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName });
        var queueUrl = queueResponse.QueueUrl;

        // Get queue ARN for subscription
        var queueAttrs = await SqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = ["QueueArn"]
        });
        var queueArn = queueAttrs.Attributes["QueueArn"];

        var subscribeRequest = new SubscribeRequest
        {
            TopicArn = topicArn,
            Protocol = "sqs",
            Endpoint = queueArn,
            Attributes = new Dictionary<string, string>
            {
                ["RawMessageDelivery"] = "false"
            }
        };

        if (!string.IsNullOrEmpty(eventTypeFilter))
        {
            subscribeRequest.Attributes["FilterPolicy"] =
                $"{{\"EventType\":[\"{eventTypeFilter}\"]}}";
        }

        await SnsClient.SubscribeAsync(subscribeRequest);

        return (topicArn, queueUrl);
    }

    public async Task DisposeAsync()
    {
        SnsClient.Dispose();
        SqsClient.Dispose();
        await _container.DisposeAsync();
    }
}
