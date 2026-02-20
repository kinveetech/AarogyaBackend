namespace Aarogya.Api.Authorization;

internal static class AarogyaRoles
{
  public const string Patient = "Patient";
  public const string Doctor = "Doctor";
  public const string LabTechnician = "LabTechnician";
  public const string Admin = "Admin";

  public static readonly string[] All = [Patient, Doctor, LabTechnician, Admin];
}
