using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

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
  private const string ReportListCacheControlValue = "private, max-age=30, must-revalidate";

  private readonly IReportService _reportService;
  private readonly IReportFileUploadService _reportFileUploadService;
  private readonly IReportChecksumVerificationService _reportChecksumVerificationService;
  private readonly IReportExtractionService _reportExtractionService;
  private readonly IConsentService _consentService;

  public ReportsController(
    IReportService reportService,
    IReportFileUploadService reportFileUploadService,
    IReportChecksumVerificationService reportChecksumVerificationService,
    IReportExtractionService reportExtractionService,
    IConsentService consentService)
  {
    _reportService = reportService;
    _reportFileUploadService = reportFileUploadService;
    _reportChecksumVerificationService = reportChecksumVerificationService;
    _reportExtractionService = reportExtractionService;
    _consentService = consentService;
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetReportDetailAsync(Guid id, CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportService.GetDetailForUserAsync(userSub, id, cancellationToken);
      return Ok(result);
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
          ["report"] = [ex.Message]
        }));
    }
    catch (UnauthorizedAccessException)
    {
      return Forbid();
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

  [HttpGet]
  [ProducesResponseType(typeof(ReportListResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListReportsAsync(
    [FromQuery] ReportListQueryRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportService.GetForUserAsync(userSub, request, cancellationToken);
      var etag = BuildReportListEtag(userSub, request, result);
      SetReportListCachingHeaders(etag);

      if (MatchesIfNoneMatch(etag))
      {
        return StatusCode(StatusCodes.Status304NotModified);
      }

      return Ok(result);
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
          ["query"] = [ex.Message]
        }));
    }
  }

  [HttpPost]
  [ProducesResponseType(typeof(ReportSummaryResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var created = await _reportService.AddForUserAsync(userSub, request, cancellationToken);
      return Created(new Uri($"/api/v1/reports/{created.ReportId}", UriKind.Relative), created);
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
          ["report"] = [ex.Message]
        }));
    }
  }

  [HttpPost("upload-url")]
  [ProducesResponseType(typeof(ReportSignedUploadUrlResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportService.GetSignedUploadUrlAsync(userSub, request, cancellationToken);
      return Ok(result);
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
  }

  [HttpPost("download-url")]
  [ProducesResponseType(typeof(ReportSignedDownloadUrlResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportService.GetSignedDownloadUrlAsync(userSub, request, cancellationToken);
      return Ok(result);
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
          ["ObjectKey"] = [ex.Message]
        }));
    }
  }

  [HttpDelete("{id:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteReportAsync(Guid id, CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var deleted = await _reportService.SoftDeleteForUserAsync(userSub, id, cancellationToken);
      return deleted ? NoContent() : NotFound();
    }
    catch (ConsentRequiredException ex)
    {
      return ForbidWithConsentError(ex.Purpose);
    }
    catch (UnauthorizedAccessException)
    {
      return Forbid();
    }
  }

  [HttpPost("upload")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(ReportUploadResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var uploaded = await _reportFileUploadService.UploadAsync(userSub, file, cancellationToken);
      return Created(new Uri($"/api/v1/reports/{uploaded.ReportId}", UriKind.Relative), uploaded);
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
          ["file"] = [ex.Message]
        }));
    }
  }

  [HttpPost("download-url/verified")]
  [ProducesResponseType(typeof(VerifiedReportDownloadResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
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
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportChecksumVerificationService.CreateVerifiedDownloadUrlAsync(userSub, request, cancellationToken);
      return Ok(result);
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

  [HttpGet("{id:guid}/extraction")]
  [ProducesResponseType(typeof(ExtractionStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetExtractionStatusAsync(Guid id, CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      var result = await _reportExtractionService.GetExtractionStatusAsync(userSub, id, cancellationToken);
      return result is null ? NotFound() : Ok(result);
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
          ["report"] = [ex.Message]
        }));
    }
    catch (UnauthorizedAccessException)
    {
      return Forbid();
    }
  }

  [HttpPost("{id:guid}/extract")]
  [ProducesResponseType(StatusCodes.Status202Accepted)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> TriggerExtractionAsync(
    Guid id,
    [FromBody] TriggerExtractionRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    try
    {
      await _consentService.EnsureGrantedAsync(userSub, ConsentPurposeCatalog.MedicalRecordsProcessing, cancellationToken);
      await _reportExtractionService.TriggerExtractionAsync(userSub, id, request.ForceReprocess, cancellationToken);
      return Accepted();
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
          ["report"] = [ex.Message]
        }));
    }
    catch (UnauthorizedAccessException)
    {
      return Forbid();
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new ValidationErrorResponse(
        "Validation failed.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
          ["extraction"] = [ex.Message]
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

  private void SetReportListCachingHeaders(string etag)
  {
    Response.Headers[HeaderNames.ETag] = etag;
    Response.Headers[HeaderNames.CacheControl] = ReportListCacheControlValue;
    Response.Headers[HeaderNames.Vary] = HeaderNames.Authorization;
  }

  private bool MatchesIfNoneMatch(string etag)
  {
    if (!Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var values))
    {
      return false;
    }

    foreach (var rawValue in values)
    {
      if (string.IsNullOrWhiteSpace(rawValue))
      {
        continue;
      }

      if (string.Equals(rawValue.Trim(), "*", StringComparison.Ordinal))
      {
        return true;
      }

      foreach (var token in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        var normalized = token.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
          ? token[2..].Trim()
          : token.Trim();
        if (string.Equals(normalized, etag, StringComparison.Ordinal))
        {
          return true;
        }
      }
    }

    return false;
  }

  private static string BuildReportListEtag(
    string userSub,
    ReportListQueryRequest request,
    ReportListResponse response)
  {
    var builder = new StringBuilder();
    builder.Append(userSub);
    builder.Append('|').Append(request.ReportType);
    builder.Append('|').Append(request.Status);
    builder.Append('|').Append(request.FromDate?.ToUniversalTime().ToString("O"));
    builder.Append('|').Append(request.ToDate?.ToUniversalTime().ToString("O"));
    builder.Append('|').Append(response.Page);
    builder.Append('|').Append(response.PageSize);
    builder.Append('|').Append(response.TotalCount);

    foreach (var item in response.Items)
    {
      builder.Append('|').Append(item.ReportId.ToString("D"));
      builder.Append('|').Append(item.Title);
      builder.Append('|').Append(item.Status);
      builder.Append('|').Append(item.CreatedAt.ToUniversalTime().ToString("O"));
    }

    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    var hash = Convert.ToHexString(hashBytes);
    return $"\"{hash}\"";
  }
}
