using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.AccessGrants;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IAccessGrantService
{
  public Task<IReadOnlyList<AccessGrantResponse>> GetForPatientAsync(string patientSub, CancellationToken cancellationToken = default);

  public Task<AccessGrantResponse> CreateAsync(string patientSub, CreateAccessGrantRequest request, CancellationToken cancellationToken = default);

  public Task<bool> RevokeAsync(string patientSub, Guid grantId, CancellationToken cancellationToken = default);
}
