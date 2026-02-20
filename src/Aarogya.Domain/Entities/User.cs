using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Entities;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core bytea hash columns are mapped as byte[] properties.")]
public sealed class User : IAuditableEntity
{
  public Guid Id { get; set; }

  public string? ExternalAuthId { get; set; }

  public UserRole Role { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string LastName { get; set; } = string.Empty;

  public string Email { get; set; } = string.Empty;

  public string? Phone { get; set; }

  public string? Address { get; set; }

  public string? BloodGroup { get; set; }

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

  public ICollection<ConsentRecord> ConsentRecords { get; set; } = [];

  public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
