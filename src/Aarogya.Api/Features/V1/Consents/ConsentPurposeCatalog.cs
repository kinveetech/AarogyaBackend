namespace Aarogya.Api.Features.V1.Consents;

internal static class ConsentPurposeCatalog
{
  public const string ProfileManagement = "profile_management";
  public const string EmergencyContactManagement = "emergency_contact_management";
  public const string MedicalDataSharing = "medical_data_sharing";
  public const string MedicalRecordsProcessing = "medical_records_processing";

  public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    ProfileManagement,
    EmergencyContactManagement,
    MedicalDataSharing,
    MedicalRecordsProcessing
  };

  public static bool IsSupported(string purpose)
    => All.Contains(purpose.Trim());
}
