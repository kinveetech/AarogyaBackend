using Aarogya.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Aarogya application.
/// DbSet properties will be added as domain entities are created.
/// </summary>
public sealed class AarogyaDbContext(DbContextOptions<AarogyaDbContext> options)
  : DbContext(options)
{
  public DbSet<User> Users => Set<User>();

  public DbSet<Report> Reports => Set<Report>();

  public DbSet<ReportParameter> ReportParameters => Set<ReportParameter>();

  public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

  public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    base.OnModelCreating(modelBuilder);

    // Enable pgcrypto extension for UUID generation
    modelBuilder.HasPostgresExtension("pgcrypto");

    // Apply all IEntityTypeConfiguration<T> implementations from this assembly
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AarogyaDbContext).Assembly);
  }

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    ApplyAuditTimestamps();
    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    ApplyAuditTimestamps();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  private void ApplyAuditTimestamps()
  {
    var now = DateTimeOffset.UtcNow;

    foreach (var entry in ChangeTracker.Entries())
    {
      if (entry.State == EntityState.Added)
      {
        SetPropertyWhenDefault(entry, "CreatedAt", now);
        SetPropertyWhenDefault(entry, "UpdatedAt", now);
        SetPropertyWhenDefault(entry, "OccurredAt", now);
        SetPropertyWhenDefault(entry, "UploadedAt", now);
        SetPropertyWhenDefault(entry, "StartsAt", now);
      }
      else if (entry.State == EntityState.Modified)
      {
        SetProperty(entry, "UpdatedAt", now);
      }
    }
  }

  private static void SetPropertyWhenDefault(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTimeOffset value)
  {
    var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
    if (property is null)
    {
      return;
    }

    if (property.CurrentValue is null
      || (property.CurrentValue is DateTimeOffset dateTimeOffset && dateTimeOffset == default))
    {
      property.CurrentValue = value;
    }
  }

  private static void SetProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTimeOffset value)
  {
    var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
    if (property is not null)
    {
      property.CurrentValue = value;
    }
  }
}
