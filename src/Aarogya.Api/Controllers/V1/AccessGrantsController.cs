using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/access-grants")]
[Authorize(Policy = AarogyaPolicies.Patient)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class AccessGrantsController(IAccessGrantService accessGrantService) : ControllerBase
{
  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<AccessGrantResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> ListAccessGrantsAsync(CancellationToken cancellationToken)
  {
    var patientSub = User.GetSubjectOrNull();
    if (patientSub is null)
    {
      return Unauthorized();
    }

    var result = await accessGrantService.GetForPatientAsync(patientSub, cancellationToken);
    return Ok(result);
  }

  [HttpPost]
  [ProducesResponseType(typeof(AccessGrantResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> CreateAccessGrantAsync(
    [FromBody] CreateAccessGrantRequest request,
    CancellationToken cancellationToken)
  {
    var patientSub = User.GetSubjectOrNull();
    if (patientSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var created = await accessGrantService.CreateAsync(patientSub, request, cancellationToken);
      return Created(new Uri($"/api/v1/access-grants/{created.GrantId}", UriKind.Relative), created);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["grant"] = [ex.Message]
        }));
    }
  }

  [HttpDelete("{grantId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeAccessGrantAsync(Guid grantId, CancellationToken cancellationToken)
  {
    var patientSub = User.GetSubjectOrNull();
    if (patientSub is null)
    {
      return Unauthorized();
    }

    var revoked = await accessGrantService.RevokeAsync(patientSub, grantId, cancellationToken);
    return revoked ? NoContent() : NotFound();
  }
}
