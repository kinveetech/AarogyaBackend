using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/consents")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class ConsentsController(IConsentService consentService) : ControllerBase
{
  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<ConsentRecordResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> GetCurrentUserConsentsAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await consentService.GetCurrentForUserAsync(userSub, cancellationToken);
    return Ok(result);
  }

  [HttpPut("{purpose}")]
  [ProducesResponseType(typeof(ConsentRecordResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> UpsertConsentAsync(
    string purpose,
    [FromBody] UpsertConsentRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var result = await consentService.UpsertForUserAsync(userSub, purpose, request, cancellationToken);
      return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["purpose"] = [ex.Message]
        }));
    }
  }
}
