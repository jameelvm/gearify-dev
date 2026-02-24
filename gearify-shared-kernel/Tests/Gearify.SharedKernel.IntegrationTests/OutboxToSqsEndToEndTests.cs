using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SQS.Model;
using FluentAssertions;
using Gearify.SharedKernel.Correlation;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.IntegrationTests.Fixtures;
using Gearify.SharedKernel.Messaging;
using Gearify.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

[Collection("MessagingInfrastructure")]
public class OutboxToSqsEndToEndTests
{
    private readonly MessagingInfrastructureFixture _infra;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private record TestOrderEvent : IDomainEvent
    {
        public Guid OrderId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string OrderNumber { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    private record TestOrderPayload
    {
        public Guid OrderId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string OrderNumber { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime OccurredAt { get; init; }
    }

    public OutboxToSqsEndToEndTests(MessagingInfrastructureFixture infra)
    {
        _infra = infra;
    }

    [Fact]
    public async Task FullFlow_OutboxFactory_ToPublisher_ToSns_ToSqs_PreservesCorrelationAndEventId()
    {
        // Arrange — setup infrastructure
        var (topicArn, queueUrl) = await _infra.LocalStack.SetupTopicWithQueue(
            $"e2e-topic-{Guid.NewGuid():N}",
            $"e2e-queue-{Guid.NewGuid():N}");

        // Setup CorrelationContext
        var expectedCorrelationId = Guid.NewGuid().ToString();
        CorrelationContext.Set(expectedCorrelationId);

        try
        {
            // Step 1: OutboxMessageFactory creates the outbox message
            var topicArnResolver = Substitute.For<ITopicArnResolver>();
            topicArnResolver.GetTopicArn("TestOrderEvent").Returns(topicArn);

            var factory = new OutboxMessageFactory(topicArnResolver);

            var domainEvent = new TestOrderEvent
            {
                OrderId = Guid.NewGuid(),
                TenantId = "tenant-e2e",
                OrderNumber = "ORD-E2E-001",
                Total = 299.99m,
                OccurredAt = DateTime.UtcNow
            };

            var outboxMessage = factory.CreateOutboxMessage(domainEvent);

            // Step 2: Save to Postgres (simulates transactional write)
            await using var dbContext = CreateDbContext();
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.Set<OutboxMessage>().Add(outboxMessage);
            await dbContext.SaveChangesAsync();

            // Step 3: OutboxPublisher polls and publishes to SNS
            var options = Options.Create(new OutboxPublisherOptions
            {
                PollingInterval = TimeSpan.FromMilliseconds(100),
                BatchSize = 10,
                MaxRetries = 3
            });

            var scopeFactory = CreateScopeFactory(dbContext);
            var publisher = new OutboxPublisher<TestE2EDbContext>(
                scopeFactory,
                _infra.LocalStack.SnsClient,
                options,
                NullLogger<OutboxPublisher<TestE2EDbContext>>.Instance);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await publisher.StartAsync(cts.Token);
                await Task.Delay(3000, cts.Token);
            }
            catch (OperationCanceledException) { }
            finally { await publisher.StopAsync(CancellationToken.None); }

            // Step 4: SqsEventQueue receives and deserializes from SQS
            var queue = new SqsEventQueue<TestOrderPayload>(
                _infra.LocalStack.SqsClient,
                queueUrl,
                NullLogger<SqsEventQueue<TestOrderPayload>>.Instance);

            var messages = await queue.ReceiveMessagesAsync(10, 5);

            // Assert — full chain verification
            messages.Should().ContainSingle();
            var received = messages[0];

            // CorrelationId survived the full journey
            received.CorrelationId.Should().Be(expectedCorrelationId);

            // EventId is present and valid
            received.EventId.Should().NotBeNullOrEmpty();

            // Payload data is intact
            received.Body.OrderId.Should().Be(domainEvent.OrderId);
            received.Body.TenantId.Should().Be("tenant-e2e");
            received.Body.OrderNumber.Should().Be("ORD-E2E-001");
            received.Body.Total.Should().Be(299.99m);

            // Outbox message marked as published
            await using var verifyContext = CreateDbContext();
            var publishedMsg = await verifyContext.Set<OutboxMessage>().FindAsync(outboxMessage.Id);
            publishedMsg!.PublishedAt.Should().NotBeNull();
        }
        finally
        {
            CorrelationContext.Set(null!);
        }
    }

    #region Test Infrastructure

    private TestE2EDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestE2EDbContext>()
            .UseNpgsql(_infra.Postgres.ConnectionString)
            .Options;
        return new TestE2EDbContext(options);
    }

    private static IServiceScopeFactory CreateScopeFactory(TestE2EDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestE2EDbContext>(opts =>
            opts.UseNpgsql(dbContext.Database.GetConnectionString()));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    public class TestE2EDbContext : DbContext
    {
        public TestE2EDbContext(DbContextOptions<TestE2EDbContext> options) : base(options) { }

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
