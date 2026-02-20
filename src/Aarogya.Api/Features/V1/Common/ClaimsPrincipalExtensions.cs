using System.Security.Claims;

namespace Aarogya.Api.Features.V1.Common;

internal static class ClaimsPrincipalExtensions
{
  public static string? GetSubjectOrNull(this ClaimsPrincipal principal)
  {
    var sub = principal.FindFirstValue("sub");
    return string.IsNullOrWhiteSpace(sub) ? null : sub;
  }
}
