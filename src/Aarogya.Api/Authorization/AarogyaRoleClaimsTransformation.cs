using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Aarogya.Api.Authorization;

internal sealed class AarogyaRoleClaimsTransformation : IClaimsTransformation
{
  private static readonly HashSet<string> SupportedRoleNames = new(AarogyaRoles.All, StringComparer.OrdinalIgnoreCase);

  public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
  {
    ArgumentNullException.ThrowIfNull(principal);

    if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
    {
      return Task.FromResult(principal);
    }

    var normalizedRoles = principal.Claims
      .Where(claim => claim.Type is "cognito:groups" or "role" or ClaimTypes.Role)
      .Select(claim => claim.Value.Trim())
      .Where(role => SupportedRoleNames.Contains(role))
      .Select(NormalizeRoleName)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Admin inherits other application roles.
    if (normalizedRoles.Contains(AarogyaRoles.Admin))
    {
      normalizedRoles.Add(AarogyaRoles.Patient);
      normalizedRoles.Add(AarogyaRoles.Doctor);
      normalizedRoles.Add(AarogyaRoles.LabTechnician);
    }

    foreach (var role in normalizedRoles)
    {
      if (!principal.Claims.Any(claim => claim.Type == ClaimTypes.Role && string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)))
      {
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
      }
    }

    return Task.FromResult(principal);
  }

  private static string NormalizeRoleName(string role)
  {
    return AarogyaRoles.All.First(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
  }
}
