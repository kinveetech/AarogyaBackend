using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aarogya.Infrastructure.Tests;

[Collection(PostgreSqlIntegrationFixtureGroup.CollectionName)]
public sealed class DatabaseIntegrationTests(PostgreSqlContainerFixture fixture)
{
  [Fact]
  public async Task AddInfrastructure_ShouldResolveDbContext_FromDependencyInjectionAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AarogyaDbContext>();

    dbContext.Should().NotBeNull();
  }

  [Fact]
  public async Task SaveChanges_ShouldPersistEncryptedEntityAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    var referenceToken = Guid.NewGuid();

    using (var arrangeScope = serviceProvider.CreateScope())
    {
      var dbContext = arrangeScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var record = new AadhaarVaultRecord
      {
        Id = Guid.NewGuid(),
        ReferenceToken = referenceToken,
        AadhaarNumber = "123456789012",
        AadhaarSha256 = [1, 2, 3, 4]
      };

      dbContext.AadhaarVaultRecords.Add(record);
      await dbContext.SaveChangesAsync();
    }

    using (var assertScope = serviceProvider.CreateScope())
    {
      var dbContext = assertScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var record = await dbContext.AadhaarVaultRecords.SingleAsync(x => x.ReferenceToken == referenceToken);

      record.AadhaarNumber.Should().Be("123456789012");
      record.AadhaarSha256.Should().Equal(1, 2, 3, 4);
    }
  }
}
