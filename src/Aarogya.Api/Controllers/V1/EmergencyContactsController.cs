using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/emergency-contacts")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class EmergencyContactsController(IEmergencyContactService emergencyContactService) : ControllerBase
{
  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<EmergencyContactResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListEmergencyContactsAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await emergencyContactService.GetForUserAsync(userSub, cancellationToken);
    return Ok(result);
  }

  [HttpPost]
  [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
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

    var created = await emergencyContactService.AddForUserAsync(userSub, request, cancellationToken);
    return Created(new Uri($"/api/v1/emergency-contacts/{created.ContactId}", UriKind.Relative), created);
  }

  [HttpDelete("{contactId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteEmergencyContactAsync(Guid contactId, CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var deleted = await emergencyContactService.DeleteForUserAsync(userSub, contactId, cancellationToken);
    return deleted ? NoContent() : NotFound();
  }
}
