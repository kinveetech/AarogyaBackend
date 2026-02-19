namespace Aarogya.Infrastructure.Aadhaar;

public interface IAadhaarVaultService
{
  public Task<Guid> CreateOrGetReferenceTokenAsync(string aadhaarNumber, Guid? actorUserId = null, CancellationToken cancellationToken = default);

  public Task<string?> GetAadhaarByReferenceTokenAsync(Guid referenceToken, Guid? actorUserId = null, CancellationToken cancellationToken = default);
}
