using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Aarogya.Infrastructure.Security;

namespace Aarogya.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by dotnet-ef for migration scaffolding.
/// </summary>
public sealed class AarogyaDbContextFactory : IDesignTimeDbContextFactory<AarogyaDbContext>
{
  public AarogyaDbContext CreateDbContext(string[] args)
  {
    var connectionString =
      Environment.GetEnvironmentVariable("AAROGYA_ConnectionStrings__DefaultConnection")
      ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
      ?? "Host=localhost;Port=5432;Database=aarogya;Username=aarogya;Password=aarogya_dev_password";

    var optionsBuilder = new DbContextOptionsBuilder<AarogyaDbContext>();
    optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
    {
      npgsqlOptions.MigrationsAssembly(typeof(AarogyaDbContext).Assembly.FullName);
      npgsqlOptions.CommandTimeout(30);
    });

    var encryptionOptions = Options.Create(new EncryptionOptions
    {
      UseAwsKms = false,
      LocalDataKey = "design-time-local-encryption-key",
      BlindIndexKey = "design-time-local-blind-index-key"
    });

    var encryptionService = new PiiFieldEncryptionService(encryptionOptions);
    var blindIndexService = new BlindIndexService(encryptionOptions);

    return new AarogyaDbContext(optionsBuilder.Options, encryptionService, blindIndexService);
  }
}
