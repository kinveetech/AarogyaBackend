using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Entities;

public sealed class User : IAuditableEntity
{
  public Guid Id { get; set; }

  public string? ExternalAuthId { get; set; }

  public UserRole Role { get; set; }

  public byte[] FirstNameEncrypted { get; set; } = [];

  public byte[] LastNameEncrypted { get; set; } = [];

  public byte[] EmailEncrypted { get; set; } = [];

  public byte[]? PhoneEncrypted { get; set; }

  public DateOnly? DateOfBirth { get; set; }

  public string? Gender { get; set; }

  public byte[]? EmailHash { get; set; }

  public byte[]? PhoneHash { get; set; }

  public Guid? AadhaarRefToken { get; set; }

  public byte[]? AadhaarSha256 { get; set; }

  public bool IsActive { get; set; } = true;

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public ICollection<Report> PatientReports { get; set; } = [];

  public ICollection<Report> UploadedReports { get; set; } = [];

  public ICollection<Report> DoctorReports { get; set; } = [];

  public ICollection<AccessGrant> PatientAccessGrants { get; set; } = [];

  public ICollection<AccessGrant> GrantedAccessGrants { get; set; } = [];

  public ICollection<AccessGrant> CreatedAccessGrants { get; set; } = [];

  public ICollection<EmergencyContact> EmergencyContacts { get; set; } = [];

  public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
