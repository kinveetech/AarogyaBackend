using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Aarogya.Api.Authorization;

internal sealed class AarogyaRoleClaimsTransformation(IRoleAssignmentService roleAssignmentService) : IClaimsTransformation
{
  private static readonly HashSet<string> SupportedRoleNames = new(AarogyaRoles.All, StringComparer.OrdinalIgnoreCase);

  public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
  {
    ArgumentNullException.ThrowIfNull(principal);

    if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
    {
      return Task.FromResult(principal);
    }

    var tokenRoles = principal.Claims
      .Where(claim => claim.Type == "cognito:groups")
      .Select(claim => claim.Value.Trim())
      .Where(role => SupportedRoleNames.Contains(role))
      .Select(NormalizeRoleName)
      .Distinct(StringComparer.OrdinalIgnoreCase);

    var assignedRoles = roleAssignmentService.GetAssignedRoles(principal.FindFirstValue("sub") ?? string.Empty);
    var normalizedRoles = tokenRoles
      .Concat(assignedRoles)
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

    var missingRoleClaims = normalizedRoles.Where(role =>
      !principal.Claims.Any(claim =>
        claim.Type == ClaimTypes.Role
        && string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)));

    foreach (var role in missingRoleClaims)
    {
      identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }

    return Task.FromResult(principal);
  }

  private static string NormalizeRoleName(string role)
  {
    return AarogyaRoles.All.First(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
  }
}
