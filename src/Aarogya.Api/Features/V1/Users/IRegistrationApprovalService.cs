using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IRegistrationApprovalService
{
  public Task<IReadOnlyList<PendingRegistrationResponse>> ListPendingAsync(
    CancellationToken cancellationToken = default);

  public Task<RegistrationStatusResponse> ApproveAsync(
    string adminSub,
    string targetSub,
    ApproveRegistrationRequest request,
    CancellationToken cancellationToken = default);

  public Task<RegistrationStatusResponse> RejectAsync(
    string adminSub,
    string targetSub,
    RejectRegistrationRequest request,
    CancellationToken cancellationToken = default);
}
