using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Consents;
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
public sealed class UsersController(
  IUserProfileService userProfileService,
  IUserDataRightsService userDataRightsService,
  IConsentService consentService)
  : ControllerBase
{
  [HttpGet("me")]
  [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.ProfileManagement, cancellationToken);
      var profile = await userProfileService.GetCurrentUserAsync(userSub, cancellationToken);
      return Ok(profile);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
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
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.ProfileManagement, cancellationToken);
      var profile = await userProfileService.UpdateCurrentUserAsync(userSub, request, cancellationToken);
      return Ok(profile);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
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
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.ProfileManagement, cancellationToken);
      var response = await userProfileService.VerifyCurrentUserAadhaarAsync(userSub, request, cancellationToken);
      return Ok(response);
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

  [HttpGet("me/export")]
  [ProducesResponseType(typeof(DataExportResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> ExportCurrentUserDataAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.ProfileManagement, cancellationToken);
      var payload = await userDataRightsService.ExportCurrentUserDataAsync(userSub, cancellationToken);
      return Ok(payload);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
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

  [HttpPost("me/deletion")]
  [ProducesResponseType(typeof(DataDeletionResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteCurrentUserDataAsync(
    [FromBody] DataDeletionRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.ProfileManagement, cancellationToken);
      var response = await userDataRightsService.DeleteCurrentUserDataAsync(userSub, request, cancellationToken);
      return Ok(response);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
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
          ["deletion"] = [ex.Message]
        }));
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
