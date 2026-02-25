namespace Aarogya.Domain.Entities;

public sealed class DoctorProfile : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string MedicalLicenseNumber { get; set; } = string.Empty;

  public string Specialization { get; set; } = string.Empty;

  public string? ClinicOrHospitalName { get; set; }

  public string? ClinicAddress { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User User { get; set; } = null!;
}
