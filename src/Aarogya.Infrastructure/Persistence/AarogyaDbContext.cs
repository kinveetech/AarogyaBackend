using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Aarogya application.
/// DbSet properties will be added as domain entities are created.
/// </summary>
public sealed class AarogyaDbContext(DbContextOptions<AarogyaDbContext> options)
  : DbContext(options)
{
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    base.OnModelCreating(modelBuilder);

    // Enable pgcrypto extension for UUID generation
    modelBuilder.HasPostgresExtension("pgcrypto");

    // Apply all IEntityTypeConfiguration<T> implementations from this assembly
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AarogyaDbContext).Assembly);
  }
}
