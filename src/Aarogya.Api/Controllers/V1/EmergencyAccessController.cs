using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/emergency-access")]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
[SuppressMessage(
  "Design",
  "S6960:This controller has multiple responsibilities and could be split into 2 smaller controllers.",
  Justification = "Emergency access request and audit endpoints are part of the same API boundary.")]
public sealed class EmergencyAccessController(
  IEmergencyAccessService emergencyAccessService,
  IEmergencyAccessAuditTrailService emergencyAccessAuditTrailService)
  : ControllerBase
{
  [HttpPost("requests")]
  [AllowAnonymous]
  [ProducesResponseType(typeof(EmergencyAccessResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CreateEmergencyAccessRequestAsync(
    [FromBody] CreateEmergencyAccessRequest request,
    CancellationToken cancellationToken)
  {
    try
    {
      var created = await emergencyAccessService.RequestAsync(request, cancellationToken);
      return Created(new Uri($"/api/v1/access-grants/{created.GrantId}", UriKind.Relative), created);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["emergencyAccess"] = [ex.Message]
        }));
    }
  }

  [HttpGet("audit")]
  [Authorize(Policy = AarogyaPolicies.Admin)]
  [ProducesResponseType(typeof(EmergencyAccessAuditTrailResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> GetEmergencyAccessAuditAsync(
    [FromQuery] EmergencyAccessAuditQueryRequest request,
    CancellationToken cancellationToken)
  {
    try
    {
      var response = await emergencyAccessAuditTrailService.QueryAsync(request, cancellationToken);
      return Ok(response);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["audit"] = [ex.Message]
        }));
    }
  }
}
