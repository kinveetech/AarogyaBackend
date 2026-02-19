using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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

    return new AarogyaDbContext(optionsBuilder.Options);
  }
}
