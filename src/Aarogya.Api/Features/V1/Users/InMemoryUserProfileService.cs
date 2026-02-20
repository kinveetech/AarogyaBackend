using System.Security.Claims;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class InMemoryUserProfileService : IUserProfileService
{
  public UserProfileResponse GetCurrentUser(ClaimsPrincipal principal)
  {
    var sub = principal.FindFirstValue("sub") ?? string.Empty;
    var email = principal.FindFirstValue("email") ?? string.Empty;
    var roles = principal.Claims
      .Where(claim => claim.Type is ClaimTypes.Role or "role" or "cognito:groups")
      .Select(claim => claim.Value)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return new UserProfileResponse(sub, email, roles);
  }
}
