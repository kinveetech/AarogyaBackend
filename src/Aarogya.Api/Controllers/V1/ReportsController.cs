using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
[SuppressMessage(
  "Major Code Smell",
  "S6960",
  Justification = "This controller groups related report operations intentionally to keep route discovery cohesive.")]
public sealed class ReportsController : ControllerBase
{
  private readonly IReportService _reportService;
  private readonly IReportFileUploadService _reportFileUploadService;
  private readonly IReportChecksumVerificationService _reportChecksumVerificationService;

  public ReportsController(
    IReportService reportService,
    IReportFileUploadService reportFileUploadService,
    IReportChecksumVerificationService reportChecksumVerificationService)
  {
    _reportService = reportService;
    _reportFileUploadService = reportFileUploadService;
    _reportChecksumVerificationService = reportChecksumVerificationService;
  }

  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<ReportSummaryResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListReportsAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await _reportService.GetForUserAsync(userSub, cancellationToken);
    return Ok(result);
  }

  [HttpPost]
  [ProducesResponseType(typeof(ReportSummaryResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> CreateReportAsync(
    [FromBody] CreateReportRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var canUpload = User.IsInRole(AarogyaRoles.Patient) || User.IsInRole(AarogyaRoles.LabTechnician);
    if (!canUpload)
    {
      return Forbid();
    }

    try
    {
      var created = await _reportService.AddForUserAsync(userSub, request, cancellationToken);
      return Created(new Uri($"/api/v1/reports/{created.ReportId}", UriKind.Relative), created);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["report"] = [ex.Message]
        }));
    }
  }

  [HttpPost("upload-url")]
  [ProducesResponseType(typeof(ReportSignedUploadUrlResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> CreateUploadUrlAsync(
    [FromBody] CreateReportUploadUrlRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await _reportService.GetSignedUploadUrlAsync(userSub, request, cancellationToken);
    return Ok(result);
  }

  [HttpPost("download-url")]
  [ProducesResponseType(typeof(ReportSignedDownloadUrlResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> CreateDownloadUrlAsync(
    [FromBody] CreateReportDownloadUrlRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }
    try
    {
      var result = await _reportService.GetSignedDownloadUrlAsync(userSub, request, cancellationToken);
      return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["ObjectKey"] = [ex.Message]
        }));
    }
  }

  [HttpPost("upload")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(ReportUploadResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> UploadReportFileAsync(
    [FromForm] IFormFile file,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var uploaded = await _reportFileUploadService.UploadAsync(userSub, file, cancellationToken);
      return Created(new Uri($"/api/v1/reports/{uploaded.ReportId}", UriKind.Relative), uploaded);
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["file"] = [ex.Message]
        }));
    }
  }

  [HttpPost("download-url/verified")]
  [ProducesResponseType(typeof(VerifiedReportDownloadResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> CreateVerifiedDownloadUrlAsync(
    [FromBody] CreateVerifiedReportDownloadRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      var result = await _reportChecksumVerificationService.CreateVerifiedDownloadUrlAsync(userSub, request, cancellationToken);
      return Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
      return NotFound(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["report"] = [ex.Message]
        }));
    }
    catch (UnauthorizedAccessException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["authorization"] = [ex.Message]
        }));
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["checksum"] = [ex.Message]
        }));
    }
  }
}
