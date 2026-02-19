using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aarogya.Api.Controllers;

[ApiController]
[Route("api/auth")]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class AuthController : ControllerBase
{
  [Authorize]
  [HttpGet("me")]
  [ProducesResponseType(typeof(AuthClaimsResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<AuthClaimsResponse> GetCurrentUserClaims()
  {
    var claimsPrincipal = User;
    var sub = claimsPrincipal.FindFirstValue("sub");
    var email = claimsPrincipal.FindFirstValue("email");

    var roles = claimsPrincipal.Claims
      .Where(claim => claim.Type is "cognito:groups" or ClaimTypes.Role or "role")
      .Select(claim => claim.Value)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();

    return Ok(new AuthClaimsResponse(sub, email, roles));
  }
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Response DTO is referenced by a public controller action signature.")]
public sealed record AuthClaimsResponse(string? Sub, string? Email, IReadOnlyList<string> Roles);
