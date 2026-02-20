using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/emergency-contacts")]
[Authorize(Policy = AarogyaPolicies.Patient)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class EmergencyContactsController(
  IEmergencyContactService emergencyContactService,
  IConsentService consentService)
  : ControllerBase
{
  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<EmergencyContactResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListEmergencyContactsAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.EmergencyContactManagement, cancellationToken);
      var result = await emergencyContactService.GetForUserAsync(userSub, cancellationToken);
      return Ok(result);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  [HttpPost]
  [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> CreateEmergencyContactAsync(
    [FromBody] CreateEmergencyContactRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.EmergencyContactManagement, cancellationToken);
      var created = await emergencyContactService.AddForUserAsync(userSub, request, cancellationToken);
      return Created(new Uri($"/api/v1/emergency-contacts/{created.ContactId}", UriKind.Relative), created);
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
          ["contact"] = [ex.Message]
        }));
    }
  }

  [HttpPut("{contactId:guid}")]
  [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> UpdateEmergencyContactAsync(
    Guid contactId,
    [FromBody] UpdateEmergencyContactRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.EmergencyContactManagement, cancellationToken);
      var updated = await emergencyContactService.UpdateForUserAsync(userSub, contactId, request, cancellationToken);
      return updated is null ? NotFound() : Ok(updated);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  [HttpDelete("{contactId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteEmergencyContactAsync(Guid contactId, CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.EmergencyContactManagement, cancellationToken);
      var deleted = await emergencyContactService.DeleteForUserAsync(userSub, contactId, cancellationToken);
      return deleted ? NoContent() : NotFound();
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
