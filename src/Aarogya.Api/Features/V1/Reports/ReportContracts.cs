using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateReportRequest(string Title);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ReportSummaryResponse(Guid ReportId, string Title, string Status, DateTimeOffset CreatedAt);

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
