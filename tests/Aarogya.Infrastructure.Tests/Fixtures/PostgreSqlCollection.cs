using Xunit;

namespace Aarogya.Infrastructure.Tests.Fixtures;

[CollectionDefinition(CollectionName)]
public sealed class PostgreSqlIntegrationFixtureGroup : ICollectionFixture<PostgreSqlContainerFixture>
{
  public const string CollectionName = "postgresql-integration";
}
