using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using FluentAssertions;
using Gearify.SharedKernel.IntegrationTests.Fixtures;
using Gearify.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

[Collection("MessagingInfrastructure")]
public class OutboxPublisherIntegrationTests
{
    private readonly MessagingInfrastructureFixture _infra;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public OutboxPublisherIntegrationTests(MessagingInfrastructureFixture infra)
    {
        _infra = infra;
    }

    [Fact]
    public async Task PublishesToSns_And_MarksAsPublished()
    {
        // Arrange
        var (topicArn, queueUrl) = await _infra.LocalStack.SetupTopicWithQueue(
            $"outbox-topic-{Guid.NewGuid():N}",
            $"outbox-queue-{Guid.NewGuid():N}");

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var outboxMessage = new OutboxMessage
        {
            EventType = "TestEvent",
            TopicArn = topicArn,
            Payload = JsonSerializer.Serialize(new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType = "TestEvent",
                tenantId = "tenant-1",
                correlationId = Guid.NewGuid().ToString(),
                timestamp = DateTime.UtcNow,
                payload = new { orderId = Guid.NewGuid(), total = 100m }
            }, JsonOptions),
            MessageAttributes = JsonSerializer.Serialize(
                new Dictionary<string, string> { ["EventType"] = "TestEvent" }, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Set<OutboxMessage>().Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new OutboxPublisherOptions
        {
            PollingInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10,
            MaxRetries = 3
        });

        var scopeFactory = CreateScopeFactory(dbContext);

        var publisher = new OutboxPublisher<TestOutboxDbContext>(
            scopeFactory,
            _infra.LocalStack.SnsClient,
            options,
            NullLogger<OutboxPublisher<TestOutboxDbContext>>.Instance);

        // Act — run publisher briefly
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await publisher.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await publisher.StopAsync(CancellationToken.None); }

        // Assert — message arrived in SQS
        var sqsResponse = await _infra.LocalStack.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 5
        });
        sqsResponse.Messages.Should().NotBeEmpty();

        // Assert — PublishedAt is set in DB
        await using var verifyContext = CreateDbContext();
        var published = await verifyContext.Set<OutboxMessage>().FindAsync(outboxMessage.Id);
        published!.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SkipsAlreadyPublished()
    {
        // Arrange
        var (topicArn, queueUrl) = await _infra.LocalStack.SetupTopicWithQueue(
            $"outbox-skip-topic-{Guid.NewGuid():N}",
            $"outbox-skip-queue-{Guid.NewGuid():N}");

        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var alreadyPublished = new OutboxMessage
        {
            EventType = "TestEvent",
            TopicArn = topicArn,
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow // Already published
        };

        dbContext.Set<OutboxMessage>().Add(alreadyPublished);
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new OutboxPublisherOptions
        {
            PollingInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10
        });

        var scopeFactory = CreateScopeFactory(dbContext);

        var publisher = new OutboxPublisher<TestOutboxDbContext>(
            scopeFactory,
            _infra.LocalStack.SnsClient,
            options,
            NullLogger<OutboxPublisher<TestOutboxDbContext>>.Instance);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await publisher.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await publisher.StopAsync(CancellationToken.None); }

        // Assert — nothing new in queue
        var sqsResponse = await _infra.LocalStack.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 1
        });
        sqsResponse.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task RetriesOnFailure_WithExponentialBackoff()
    {
        // Arrange — use an invalid topic ARN to force failure
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var outboxMessage = new OutboxMessage
        {
            EventType = "TestEvent",
            TopicArn = "arn:aws:sns:us-east-1:000000000000:non-existent-topic",
            Payload = "{}",
            MessageAttributes = JsonSerializer.Serialize(
                new Dictionary<string, string> { ["EventType"] = "TestEvent" }, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Set<OutboxMessage>().Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new OutboxPublisherOptions
        {
            PollingInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10,
            MaxRetries = 5,
            BackoffBase = 2.0
        });

        var scopeFactory = CreateScopeFactory(dbContext);

        var publisher = new OutboxPublisher<TestOutboxDbContext>(
            scopeFactory,
            _infra.LocalStack.SnsClient,
            options,
            NullLogger<OutboxPublisher<TestOutboxDbContext>>.Instance);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await publisher.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await publisher.StopAsync(CancellationToken.None); }

        // Assert — retry count increased, NextRetryAt set
        await using var verifyContext = CreateDbContext();
        var msg = await verifyContext.Set<OutboxMessage>().FindAsync(outboxMessage.Id);
        msg!.RetryCount.Should().BeGreaterThan(0);
        msg.LastError.Should().NotBeNullOrEmpty();
        msg.PublishedAt.Should().BeNull();
    }

    #region Test Infrastructure

    private TestOutboxDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestOutboxDbContext>()
            .UseNpgsql(_infra.Postgres.ConnectionString)
            .Options;
        return new TestOutboxDbContext(options);
    }

    private static IServiceScopeFactory CreateScopeFactory(TestOutboxDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestOutboxDbContext>(opts =>
            opts.UseNpgsql(dbContext.Database.GetConnectionString()));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    public class TestOutboxDbContext : DbContext
    {
        public TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : base(options) { }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.ToTable("outbox_messages");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventType).IsRequired();
                entity.Property(e => e.TopicArn).IsRequired();
                entity.Property(e => e.Payload).IsRequired();
            });
        }
    }

    #endregion
}
