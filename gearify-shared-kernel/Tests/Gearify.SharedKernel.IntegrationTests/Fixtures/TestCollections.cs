using Xunit;

namespace Gearify.SharedKernel.IntegrationTests.Fixtures;

[CollectionDefinition("LocalStack")]
public class LocalStackCollection : ICollectionFixture<LocalStackFixture>;

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisFixture>;

[CollectionDefinition("MessagingInfrastructure")]
public class MessagingInfrastructureCollection : ICollectionFixture<MessagingInfrastructureFixture>;
