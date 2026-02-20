using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IUserDataRightsService
{
  public Task<DataExportResponse> ExportCurrentUserDataAsync(
    string userSub,
    CancellationToken cancellationToken = default);

  public Task<DataDeletionResponse> DeleteCurrentUserDataAsync(
    string userSub,
    DataDeletionRequest request,
    CancellationToken cancellationToken = default);
}
