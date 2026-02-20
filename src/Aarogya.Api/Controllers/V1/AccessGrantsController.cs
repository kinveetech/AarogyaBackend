using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/access-grants")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class AccessGrantsController(
  IAccessGrantService accessGrantService,
  IConsentService consentService)
  : ControllerBase
{
  [HttpGet]
  [Authorize(Policy = AarogyaPolicies.Patient)]
  [ProducesResponseType(typeof(IReadOnlyList<AccessGrantResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListAccessGrantsAsync(CancellationToken cancellationToken)
  {
    var patientSub = User.GetSubjectOrNull();
    if (patientSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(patientSub, ConsentPurposeCatalog.MedicalDataSharing, cancellationToken);
      var result = await accessGrantService.GetForPatientAsync(patientSub, cancellationToken);
      return Ok(result);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  [HttpGet("received")]
  [Authorize(Policy = AarogyaPolicies.Doctor)]
  [ProducesResponseType(typeof(IReadOnlyList<AccessGrantResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListReceivedAccessGrantsAsync(CancellationToken cancellationToken)
  {
    var doctorSub = User.GetSubjectOrNull();
    if (doctorSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(doctorSub, ConsentPurposeCatalog.MedicalDataSharing, cancellationToken);
      var result = await accessGrantService.GetForDoctorAsync(doctorSub, cancellationToken);
      return Ok(result);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  [HttpPost]
  [Authorize(Policy = AarogyaPolicies.Patient)]
  [ProducesResponseType(typeof(AccessGrantResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
      await consentService.EnsureGrantedAsync(patientSub, ConsentPurposeCatalog.MedicalDataSharing, cancellationToken);
      var created = await accessGrantService.CreateAsync(patientSub, request, cancellationToken);
      return Created(new Uri($"/api/v1/access-grants/{created.GrantId}", UriKind.Relative), created);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
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
  [Authorize(Policy = AarogyaPolicies.Patient)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeAccessGrantAsync(Guid grantId, CancellationToken cancellationToken)
  {
    var patientSub = User.GetSubjectOrNull();
    if (patientSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(patientSub, ConsentPurposeCatalog.MedicalDataSharing, cancellationToken);
      var revoked = await accessGrantService.RevokeAsync(patientSub, grantId, cancellationToken);
      return revoked ? NoContent() : NotFound();
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  private ObjectResult ForbidWithConsentError(string purpose)
  {
    return StatusCode(
      StatusCodes.Status403Forbidden,
      new ValidationErrorResponse(
        "Consent required.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["consent"] = [$"Consent for purpose '{purpose}' is required."]
        }));
  }
}
