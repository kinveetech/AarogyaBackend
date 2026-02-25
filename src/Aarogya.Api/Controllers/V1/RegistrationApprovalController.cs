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
[Authorize(Policy = AarogyaPolicies.Admin)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class RegistrationApprovalController(
  IRegistrationApprovalService registrationApprovalService)
  : ControllerBase
{
  [HttpGet("pending-registrations")]
  [ProducesResponseType(typeof(IReadOnlyList<PendingRegistrationResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> ListPendingRegistrationsAsync(CancellationToken cancellationToken)
  {
    var result = await registrationApprovalService.ListPendingAsync(cancellationToken);
    return Ok(result);
  }

  [HttpPost("{sub}/approve-registration")]
  [ProducesResponseType(typeof(RegistrationStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> ApproveRegistrationAsync(
    [FromRoute] string sub,
    [FromBody] ApproveRegistrationRequest request,
    CancellationToken cancellationToken)
  {
    var adminSub = User.GetSubjectOrNull();
    if (adminSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var result = await registrationApprovalService.ApproveAsync(adminSub, sub, request, cancellationToken);
      return Ok(result);
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

  [HttpPost("{sub}/reject-registration")]
  [ProducesResponseType(typeof(RegistrationStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RejectRegistrationAsync(
    [FromRoute] string sub,
    [FromBody] RejectRegistrationRequest request,
    CancellationToken cancellationToken)
  {
    var adminSub = User.GetSubjectOrNull();
    if (adminSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var result = await registrationApprovalService.RejectAsync(adminSub, sub, request, cancellationToken);
      return Ok(result);
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
}
