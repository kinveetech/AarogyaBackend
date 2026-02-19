using System.Net;

namespace Aarogya.Domain.Entities;

public sealed class AadhaarVaultAccessLog
{
  public Guid Id { get; set; }

  public Guid ReferenceToken { get; set; }

  public DateTimeOffset OccurredAt { get; set; }

  public Guid? ActorUserId { get; set; }

  public string Action { get; set; } = string.Empty;

  public string? RequestPath { get; set; }

  public string? RequestMethod { get; set; }

  public IPAddress? ClientIp { get; set; }

  public int? ResultStatus { get; set; }

  public string? Details { get; set; }

  public AadhaarVaultRecord AadhaarVaultRecord { get; set; } = null!;
}
