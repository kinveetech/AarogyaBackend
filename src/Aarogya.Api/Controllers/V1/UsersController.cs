using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class UsersController(IUserProfileService userProfileService) : ControllerBase
{
  [HttpGet("me")]
  [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<UserProfileResponse> GetCurrentUserProfile()
    => Ok(userProfileService.GetCurrentUser(User));
}
