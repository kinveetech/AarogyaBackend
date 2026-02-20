using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Consents;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public ASP.NET Core controllers.")]
public interface IConsentService
{
  public Task<IReadOnlyList<ConsentRecordResponse>> GetCurrentForUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<ConsentRecordResponse> UpsertForUserAsync(
    string userSub,
    string purpose,
    UpsertConsentRequest request,
    CancellationToken cancellationToken = default);

  public Task EnsureGrantedAsync(string userSub, string purpose, CancellationToken cancellationToken = default);
}
