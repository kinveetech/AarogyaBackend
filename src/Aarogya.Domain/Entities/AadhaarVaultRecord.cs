using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Domain.Entities;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core bytea columns are mapped as byte[] properties.")]
public sealed class AadhaarVaultRecord : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid ReferenceToken { get; set; }

  public string AadhaarNumber { get; set; } = string.Empty;

  public byte[] AadhaarSha256 { get; set; } = [];

  public string? ProviderRequestId { get; set; }

  public string? VerificationProvider { get; set; }

  public string? DemographicsJson { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public ICollection<AadhaarVaultAccessLog> AccessLogs { get; set; } = [];
}
