using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IUserProfileService
{
  public UserProfileResponse GetCurrentUser(ClaimsPrincipal principal);
}
