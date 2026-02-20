using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateReportRequest(
  string ReportType,
  string ObjectKey,
  string? LabName,
  string? LabCode,
  DateTimeOffset? CollectedAt,
  DateTimeOffset? ReportedAt,
  string? Notes,
  string? PatientSub,
  IReadOnlyList<CreateReportParameterRequest> Parameters,
  string? SourceSystem = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateReportParameterRequest(
  string Code,
  string Name,
  decimal? Value,
  string? ValueText,
  string? Unit,
  string? ReferenceRange,
  bool? IsAbnormal,
  IReadOnlyDictionary<string, string>? Attributes = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportSummaryResponse(Guid ReportId, string Title, string Status, DateTimeOffset CreatedAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for query binding.")]
public sealed record ReportListQueryRequest(
  string? ReportType = null,
  string? Status = null,
  DateTimeOffset? FromDate = null,
  DateTimeOffset? ToDate = null,
  int Page = 1,
  int PageSize = 20);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportListResponse(
  int Page,
  int PageSize,
  int TotalCount,
  IReadOnlyList<ReportSummaryResponse> Items);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportDetailParameterResponse(
  string Code,
  string Name,
  decimal? NumericValue,
  string? TextValue,
  string? Unit,
  string? ReferenceRange,
  bool? IsAbnormal);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportDetailResponse(
  Guid ReportId,
  string ReportNumber,
  string ReportType,
  string Status,
  DateTimeOffset UploadedAt,
  DateTimeOffset CreatedAt,
  string? LabName,
  string? LabCode,
  DateTimeOffset? CollectedAt,
  DateTimeOffset? ReportedAt,
  string? Notes,
  IReadOnlyList<ReportDetailParameterResponse> Parameters,
  ReportSignedDownloadUrlResponse Download);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record CreateReportUploadUrlRequest(
  string FileName,
  string ContentType,
  int? ExpiryMinutes = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record CreateReportDownloadUrlRequest(
  string ObjectKey,
  int? ExpiryMinutes = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportSignedUploadUrlResponse(
  string ObjectKey,
  Uri UploadUrl,
  DateTimeOffset ExpiresAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportSignedDownloadUrlResponse(
  string ObjectKey,
  Uri DownloadUrl,
  DateTimeOffset ExpiresAt,
  string Provider);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportUploadResponse(
  Guid ReportId,
  string ReportNumber,
  string ObjectKey,
  string ContentType,
  long SizeBytes,
  string ChecksumSha256,
  DateTimeOffset UploadedAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
[SuppressMessage(
  "Major Code Smell",
  "S6964",
  Justification = "FluentValidation enforces ReportId non-empty for this request model.")]
public sealed record CreateVerifiedReportDownloadRequest(
  Guid ReportId,
  int? ExpiryMinutes = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record VerifiedReportDownloadResponse(
  Guid ReportId,
  string ObjectKey,
  Uri DownloadUrl,
  DateTimeOffset ExpiresAt,
  bool ChecksumVerified);
