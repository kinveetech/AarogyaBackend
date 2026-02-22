using System.ComponentModel;

namespace Aarogya.Domain.Enums;

public enum UserRole
{
  [Description("patient")] Patient,
  [Description("doctor")] Doctor,
  [Description("lab_technician")] LabTechnician,
  [Description("admin")] Admin
}
