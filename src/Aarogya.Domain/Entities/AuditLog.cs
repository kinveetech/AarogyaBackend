using System.Net;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;

namespace Aarogya.Domain.Entities;

public sealed class AuditLog
{
  public Guid Id { get; set; }

  public DateTimeOffset OccurredAt { get; set; }

  public Guid? ActorUserId { get; set; }

  public UserRole? ActorRole { get; set; }

  public string Action { get; set; } = string.Empty;

  public string EntityType { get; set; } = string.Empty;

  public Guid? EntityId { get; set; }

  public Guid? CorrelationId { get; set; }

  public string? RequestPath { get; set; }

  public string? RequestMethod { get; set; }

  public IPAddress? ClientIp { get; set; }

  public string? UserAgent { get; set; }

  public int? ResultStatus { get; set; }

  public AuditLogDetails Details { get; set; } = new();

  public User? ActorUser { get; set; }
}
