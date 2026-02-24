using Xunit;

namespace Gearify.SharedKernel.IntegrationTests.Fixtures;

public class MessagingInfrastructureFixture : IAsyncLifetime
{
    public LocalStackFixture LocalStack { get; } = new();
    public PostgresFixture Postgres { get; } = new();
    public RedisFixture Redis { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            LocalStack.InitializeAsync(),
            Postgres.InitializeAsync(),
            Redis.InitializeAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            LocalStack.DisposeAsync(),
            Postgres.DisposeAsync(),
            Redis.DisposeAsync());
    }
}
