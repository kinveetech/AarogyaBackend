using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Domain.Entities;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core bytea hash columns are mapped as byte[] properties.")]
public sealed class EmergencyContact : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string Name { get; set; } = string.Empty;

  public string Relationship { get; set; } = string.Empty;

  public string Phone { get; set; } = string.Empty;

  public byte[]? PhoneHash { get; set; }

  public string? Email { get; set; }

  public bool IsPrimary { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User User { get; set; } = null!;
}
