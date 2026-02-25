namespace Aarogya.Domain.Entities;

public sealed class LabTechnicianProfile : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string LabName { get; set; } = string.Empty;

  public string? LabLicenseNumber { get; set; }

  public string? NablAccreditationId { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User User { get; set; } = null!;
}
