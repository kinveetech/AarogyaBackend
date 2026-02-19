using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;

namespace Aarogya.Domain.Entities;

public sealed class AccessGrant
{
  public Guid Id { get; set; }

  public Guid PatientId { get; set; }

  public Guid GrantedToUserId { get; set; }

  public Guid GrantedByUserId { get; set; }

  public string? GrantReason { get; set; }

  public AccessGrantScope Scope { get; set; } = new();

  public AccessGrantStatus Status { get; set; } = AccessGrantStatus.Active;

  public DateTimeOffset StartsAt { get; set; }

  public DateTimeOffset? ExpiresAt { get; set; }

  public DateTimeOffset? RevokedAt { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public User Patient { get; set; } = null!;

  public User GrantedToUser { get; set; } = null!;

  public User GrantedByUser { get; set; } = null!;
}
