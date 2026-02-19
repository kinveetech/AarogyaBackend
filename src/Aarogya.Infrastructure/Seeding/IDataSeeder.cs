namespace Aarogya.Infrastructure.Seeding;

public interface IDataSeeder
{
  public Task SeedAsync(CancellationToken cancellationToken = default);
}
