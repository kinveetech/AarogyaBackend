namespace Aarogya.Infrastructure.Security;

public interface IDataEncryptionKeyRotationService
{
  public Task<DataEncryptionReEncryptionSummary> ReEncryptAllAsync(CancellationToken cancellationToken = default);
}

public sealed record DataEncryptionReEncryptionSummary(
  string ActiveKeyId,
  int UsersTouched,
  int EmergencyContactsTouched,
  int AadhaarRecordsTouched)
{
  public int TotalTouched => UsersTouched + EmergencyContactsTouched + AadhaarRecordsTouched;
}
