using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;

namespace Aarogya.Domain.Entities;

public sealed class Report : IAuditableEntity
{
  public Guid Id { get; set; }

  public string ReportNumber { get; set; } = string.Empty;

  public Guid PatientId { get; set; }

  public Guid? DoctorId { get; set; }

  public Guid UploadedByUserId { get; set; }

  public ReportType ReportType { get; set; }

  public ReportStatus Status { get; set; } = ReportStatus.Uploaded;

  public string? SourceSystem { get; set; }

  public DateTimeOffset? CollectedAt { get; set; }

  public DateTimeOffset? ReportedAt { get; set; }

  public DateTimeOffset UploadedAt { get; set; }

  public string? FileStorageKey { get; set; }

  public string? ChecksumSha256 { get; set; }

  public ReportResults Results { get; set; } = new();

  public ReportMetadata Metadata { get; set; } = new();

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User Patient { get; set; } = null!;

  public User? Doctor { get; set; }

  public User UploadedByUser { get; set; } = null!;

  public ICollection<ReportParameter> Parameters { get; set; } = [];
}
