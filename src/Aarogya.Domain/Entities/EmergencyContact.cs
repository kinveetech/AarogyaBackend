using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Domain.Entities;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core bytea columns are mapped as byte[] properties.")]
public sealed class EmergencyContact : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public byte[] NameEncrypted { get; set; } = [];

  public string Relationship { get; set; } = string.Empty;

  public byte[] PhoneEncrypted { get; set; } = [];

  public byte[]? PhoneHash { get; set; }

  public bool IsPrimary { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User User { get; set; } = null!;
}
