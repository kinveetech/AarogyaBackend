using System.Security.Claims;

namespace Aarogya.Api.Features.V1.Common;

internal static class ClaimsPrincipalExtensions
{
  public static string? GetSubjectOrNull(this ClaimsPrincipal principal)
  {
    var sub = principal.FindFirstValue("sub");
    return string.IsNullOrWhiteSpace(sub) ? null : sub;
  }

  /// <summary>
  /// Checks whether the principal has a <see cref="ClaimTypes.Role"/> claim with the given value.
  /// Unlike <see cref="ClaimsPrincipal.IsInRole"/>, this always checks <see cref="ClaimTypes.Role"/>
  /// regardless of the identity's <c>RoleClaimType</c> (which may be set to <c>cognito:groups</c>).
  /// </summary>
  public static bool HasRole(this ClaimsPrincipal principal, string role)
  {
    return principal.HasClaim(ClaimTypes.Role, role);
  }
}
