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
  IConsentService consentService,
  IUserRegistrationService userRegistrationService,
  IRegistrationApprovalService registrationApprovalService)
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

  [HttpGet("pending-registrations")]
  [Authorize(Policy = AarogyaPolicies.Admin)]
  [ProducesResponseType(typeof(IReadOnlyList<PendingRegistrationResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<IActionResult> ListPendingRegistrationsAsync(CancellationToken cancellationToken)
  {
    var result = await registrationApprovalService.ListPendingAsync(cancellationToken);
    return Ok(result);
  }

  [HttpPost("{sub}/approve-registration")]
  [Authorize(Policy = AarogyaPolicies.Admin)]
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
  [Authorize(Policy = AarogyaPolicies.Admin)]
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
