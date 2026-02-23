namespace Aarogya.Domain.Entities;

public sealed class ApiKey : IAuditableEntity
{
  public Guid Id { get; set; }

  public string KeyHash { get; set; } = string.Empty;

  public string KeyPrefix { get; set; } = string.Empty;

  public string PartnerId { get; set; } = string.Empty;

  public string PartnerName { get; set; } = string.Empty;

  public DateTimeOffset ExpiresAt { get; set; }

  public bool IsRevoked { get; set; }

  public DateTimeOffset? RevokedAt { get; set; }

  public DateTimeOffset? OverlapExpiresAt { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}
