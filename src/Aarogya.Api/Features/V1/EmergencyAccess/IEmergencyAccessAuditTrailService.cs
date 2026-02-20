using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IEmergencyAccessAuditTrailService
{
  public Task<EmergencyAccessAuditTrailResponse> QueryAsync(
    EmergencyAccessAuditQueryRequest request,
    CancellationToken cancellationToken = default);
}
