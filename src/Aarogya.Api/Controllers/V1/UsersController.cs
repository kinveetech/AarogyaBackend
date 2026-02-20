using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
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
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetCurrentUserProfileAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var profile = await userProfileService.GetCurrentUserAsync(userSub, cancellationToken);
      return Ok(profile);
    }
    catch (KeyNotFoundException ex)
    {
      return NotFound(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["user"] = [ex.Message]
        }));
    }
  }

  [HttpPut("me")]
  [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> UpdateCurrentUserProfileAsync(
    [FromBody] UpdateUserProfileRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var profile = await userProfileService.UpdateCurrentUserAsync(userSub, request, cancellationToken);
      return Ok(profile);
    }
    catch (KeyNotFoundException ex)
    {
      return NotFound(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["user"] = [ex.Message]
        }));
    }
  }

  [HttpPost("me/aadhaar/verify")]
  [Authorize(Policy = AarogyaPolicies.Patient)]
  [ProducesResponseType(typeof(AadhaarVerificationResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> VerifyCurrentUserAadhaarAsync(
    [FromBody] VerifyAadhaarRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var response = await userProfileService.VerifyCurrentUserAadhaarAsync(userSub, request, cancellationToken);
      return Ok(response);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["aadhaar"] = [ex.Message]
        }));
    }
    catch (KeyNotFoundException ex)
    {
      return NotFound(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["user"] = [ex.Message]
        }));
    }
  }
}
