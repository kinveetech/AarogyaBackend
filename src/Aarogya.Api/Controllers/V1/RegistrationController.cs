using System.Diagnostics.CodeAnalysis;
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
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class RegistrationController(
  IUserRegistrationService userRegistrationService)
  : ControllerBase
{
  [HttpPost("register")]
  [AllowAnonymous]
  [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> RegisterAsync(
    [FromBody] RegisterUserRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var result = await userRegistrationService.RegisterAsync(userSub, request, cancellationToken);
      return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["registration"] = [ex.Message]
        }));
    }
  }

  [HttpGet("me/registration-status")]
  [AllowAnonymous]
  [ProducesResponseType(typeof(RegistrationStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetRegistrationStatusAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await userRegistrationService.GetRegistrationStatusAsync(userSub, cancellationToken);
    return Ok(result);
  }
}
