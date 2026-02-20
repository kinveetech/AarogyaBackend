using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IUserProfileService
{
  public Task<UserProfileResponse> GetCurrentUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<UserProfileResponse> UpdateCurrentUserAsync(
    string userSub,
    UpdateUserProfileRequest request,
    CancellationToken cancellationToken = default);

  public Task<AadhaarVerificationResponse> VerifyCurrentUserAadhaarAsync(
    string userSub,
    VerifyAadhaarRequest request,
    CancellationToken cancellationToken = default);
}
