using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ReportService(
  IAmazonS3 s3Client,
  IUserRepository userRepository,
  IAccessGrantRepository accessGrantRepository,
  IReportRepository reportRepository,
  IAuditLoggingService auditLoggingService,
  IPatientNotificationService patientNotificationService,
  IUnitOfWork unitOfWork,
  IOptions<AwsOptions> awsOptions,
  IUtcClock clock)
  : IReportService
{
  private readonly AwsOptions _awsOptions = awsOptions.Value;

  public async Task<ReportListResponse> GetForUserAsync(
    string userSub,
    ReportListQueryRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated user is not provisioned in the database.");

    var reportTypeFilter = TryParseReportTypeOrNull(request.ReportType);
    var statusFilter = TryParseStatusOrNull(request.Status);

    IReadOnlyList<Report> accessibleReports;
    if (user.Role == UserRole.LabTechnician)
    {
      accessibleReports = await reportRepository.ListAsync(
        new ReportsByUploaderSpecification(user.Id),
        cancellationToken);
    }
    else if (user.Role == UserRole.Doctor)
    {
      var now = clock.UtcNow;
      var grants = await accessGrantRepository.ListAsync(
        new ActiveAccessGrantsForDoctorSpecification(user.Id, now),
        cancellationToken);

      if (grants.Count == 0)
      {
        return new ReportListResponse(request.Page, request.PageSize, 0, []);
      }

      var grantsByPatient = grants
        .GroupBy(grant => grant.PatientId)
        .ToDictionary(group => group.Key, group => group.ToArray());
      var patientIds = grantsByPatient.Keys.ToArray();

      var allGrantedReports = new List<Report>();
      foreach (var patientId in patientIds)
      {
        var patientReports = await reportRepository.ListByPatientAsync(patientId, cancellationToken);
        var patientGrants = grantsByPatient[patientId];
        allGrantedReports.AddRange(
          patientReports.Where(report => IsReportAllowedByGrant(patientGrants, report, requiresDownloadAccess: false)));
      }

      accessibleReports = allGrantedReports
        .GroupBy(report => report.Id)
        .Select(group => group.First())
        .ToArray();
    }
    else
    {
      accessibleReports = await reportRepository.ListByPatientAsync(user.Id, cancellationToken);
    }

    var filtered = accessibleReports
      .Where(report => !reportTypeFilter.HasValue || report.ReportType == reportTypeFilter.Value)
      .Where(report => !statusFilter.HasValue || report.Status == statusFilter.Value)
      .Where(report => !request.FromDate.HasValue || report.UploadedAt >= request.FromDate.Value)
      .Where(report => !request.ToDate.HasValue || report.UploadedAt <= request.ToDate.Value)
      .OrderByDescending(report => report.UploadedAt)
      .ThenByDescending(report => report.CreatedAt)
      .ToArray();

    var totalCount = filtered.Length;
    var page = Math.Max(1, request.Page);
    var pageSize = Math.Clamp(request.PageSize, 1, 100);
    var skip = (page - 1) * pageSize;
    var paged = filtered.Skip(skip).Take(pageSize);

    var items = paged
      .Select(report => new ReportSummaryResponse(
        report.Id,
        BuildSummaryTitle(report),
        ToStatusString(report.Status),
        report.CreatedAt))
      .ToArray();

    await auditLoggingService.LogDataAccessAsync(
      user,
      "report.listed",
      "report",
      null,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["totalCount"] = totalCount.ToString(CultureInfo.InvariantCulture),
        ["page"] = page.ToString(CultureInfo.InvariantCulture),
        ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
      },
      cancellationToken);

    return new ReportListResponse(page, pageSize, totalCount, items);
  }

  public async Task<ReportDetailResponse> GetDetailForUserAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated user is not provisioned in the database.");

    var report = await reportRepository.FirstOrDefaultAsync(
      new ReportByIdSpecification(reportId),
      cancellationToken)
      ?? throw new KeyNotFoundException("Report not found.");

    var canAccess = await CanAccessReportAsync(user, report, cancellationToken);
    if (!canAccess)
    {
      throw new UnauthorizedAccessException("You do not have access to this report.");
    }

    if (string.IsNullOrWhiteSpace(report.FileStorageKey))
    {
      throw new InvalidOperationException("Report file reference is missing.");
    }

    var download = await CreateSignedDownloadUrlAsync(report.FileStorageKey, null, cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      user,
      "report.viewed",
      "report",
      report.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["reportType"] = ToReportTypeCode(report.ReportType)
      },
      cancellationToken);

    report.Metadata.Tags.TryGetValue("lab-name", out var labName);
    report.Metadata.Tags.TryGetValue("lab-code", out var labCode);

    var parameters = report.Parameters
      .OrderBy(parameter => parameter.ParameterName, StringComparer.OrdinalIgnoreCase)
      .Select(parameter => new ReportDetailParameterResponse(
        parameter.ParameterCode,
        parameter.ParameterName,
        parameter.MeasuredValueNumeric,
        parameter.MeasuredValueText,
        parameter.Unit,
        parameter.ReferenceRangeText,
        parameter.IsAbnormal))
      .ToArray();

    return new ReportDetailResponse(
      report.Id,
      report.ReportNumber,
      ToReportTypeCode(report.ReportType),
      ToStatusString(report.Status),
      report.UploadedAt,
      report.CreatedAt,
      labName,
      labCode,
      report.CollectedAt,
      report.ReportedAt,
      report.Results.Notes,
      parameters,
      download);
  }

  public async Task<ReportSummaryResponse> AddForUserAsync(
    string userSub,
    CreateReportRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var uploader = await ResolveUploaderAsync(userSub, cancellationToken);

    var patient = await ResolvePatientAsync(
      uploader,
      request.PatientSub,
      request.PatientPhone,
      request.PatientAadhaar,
      cancellationToken);

    EnsureObjectKeyBelongsToUploader(userSub, request.ObjectKey);
    var objectMetadata = await GetObjectMetadataAsync(request.ObjectKey, cancellationToken);

    var now = clock.UtcNow;
    var reportType = ParseReportType(request.ReportType);
    var sourceSystem = ResolveSourceSystem(uploader.Role, request.SourceSystem);
    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = await GenerateReportNumberAsync(cancellationToken),
      PatientId = patient.Id,
      UploadedByUserId = uploader.Id,
      ReportType = reportType,
      Status = ReportStatus.Uploaded,
      SourceSystem = sourceSystem,
      CollectedAt = request.CollectedAt,
      ReportedAt = request.ReportedAt,
      UploadedAt = now,
      FileStorageKey = request.ObjectKey.Trim(),
      ChecksumSha256 = ResolveChecksum(objectMetadata),
      Results = BuildResults(request),
      Metadata = BuildMetadata(request, objectMetadata, reportType, sourceSystem),
      Parameters = BuildParameters(request.Parameters, now),
      CreatedAt = now,
      UpdatedAt = now
    };

    await reportRepository.AddAsync(report, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      uploader,
      "report.created",
      "report",
      report.Id,
      201,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["reportType"] = ToReportTypeCode(report.ReportType),
        ["patientUserId"] = report.PatientId.ToString("D")
      },
      cancellationToken);

    if (uploader.Role == UserRole.LabTechnician)
    {
      await patientNotificationService.NotifyReportUploadedAsync(patient, report, cancellationToken);
    }

    return new ReportSummaryResponse(
      report.Id,
      BuildSummaryTitle(report),
      ToStatusString(report.Status),
      report.CreatedAt);
  }

  public async Task<ReportSignedUploadUrlResponse> GetSignedUploadUrlAsync(
    string userSub,
    CreateReportUploadUrlRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var now = clock.UtcNow;
    var expiresAt = now.AddMinutes(GetExpiryMinutes(request.ExpiryMinutes));
    var objectKey = BuildObjectKey(userSub, request.FileName, now);

    var presignRequest = new GetPreSignedUrlRequest
    {
      BucketName = _awsOptions.S3.BucketName,
      Key = objectKey,
      Verb = HttpVerb.PUT,
      Expires = expiresAt.UtcDateTime,
      ContentType = request.ContentType,
      Protocol = ResolveProtocol()
    };

    var url = await s3Client.GetPreSignedURLAsync(presignRequest);
    return new ReportSignedUploadUrlResponse(objectKey, new Uri(url, UriKind.Absolute), expiresAt);
  }

  public async Task<ReportSignedDownloadUrlResponse> GetSignedDownloadUrlAsync(
    string userSub,
    CreateReportDownloadUrlRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var expectedPrefix = $"reports/{userSub}/";
    if (!request.ObjectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("Object key does not belong to the authenticated user.");
    }

    var report = await reportRepository.GetByFileStorageKeyAsync(request.ObjectKey.Trim(), cancellationToken);
    if (report is null || report.IsDeleted)
    {
      throw new InvalidOperationException("Report file is not available.");
    }

    return await CreateSignedDownloadUrlAsync(request.ObjectKey, request.ExpiryMinutes, cancellationToken);
  }

  public async Task<bool> SoftDeleteForUserAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated user is not provisioned in the database.");

    var report = await reportRepository.GetByIdAsync(reportId, cancellationToken);
    if (report is null || report.IsDeleted)
    {
      return false;
    }

    if (!CanDeleteReport(user, report))
    {
      throw new UnauthorizedAccessException("You do not have access to delete this report.");
    }

    var now = clock.UtcNow;
    report.IsDeleted = true;
    report.DeletedAt = now;
    report.HardDeletedAt = null;

    reportRepository.Update(report);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      user,
      "report.deleted",
      "report",
      report.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["reportType"] = ToReportTypeCode(report.ReportType)
      },
      cancellationToken);

    return true;
  }

  private static string BuildSummaryTitle(Report report)
  {
    if (report.Metadata.Tags.TryGetValue("lab-name", out var labName) && !string.IsNullOrWhiteSpace(labName))
    {
      return $"{report.ReportType} - {labName}";
    }

    return $"{report.ReportType} - {report.ReportNumber}";
  }

  private async Task<bool> CanAccessReportAsync(
    User user,
    Report report,
    CancellationToken cancellationToken)
  {
    if (user.Role == UserRole.Patient)
    {
      return report.PatientId == user.Id;
    }

    if (user.Role == UserRole.LabTechnician)
    {
      return report.UploadedByUserId == user.Id;
    }

    if (user.Role != UserRole.Doctor)
    {
      return false;
    }

    var now = clock.UtcNow;
    var grants = await accessGrantRepository.ListAsync(
      new ActiveAccessGrantsForDoctorSpecification(user.Id, now),
      cancellationToken);

    var patientGrants = grants
      .Where(grant => grant.PatientId == report.PatientId)
      .Where(grant => grant.Scope.CanReadReports)
      .ToArray();

    return IsReportAllowedByGrant(patientGrants, report, requiresDownloadAccess: true);
  }

  private static bool IsReportAllowedByGrant(
    IReadOnlyList<AccessGrant> grants,
    Report report,
    bool requiresDownloadAccess)
  {
    if (grants.Count == 0)
    {
      return false;
    }

    return grants.Any(grant =>
    {
      if (!grant.Scope.CanReadReports)
      {
        return false;
      }

      if (requiresDownloadAccess && !grant.Scope.CanDownloadReports)
      {
        return false;
      }

      var allowedTypes = grant.Scope.AllowedReportTypes
        .Where(type => !string.IsNullOrWhiteSpace(type))
        .Select(type => type.Trim())
        .ToArray();
      if (allowedTypes.Length > 0
        && !allowedTypes.Contains(ToReportTypeCode(report.ReportType), StringComparer.OrdinalIgnoreCase))
      {
        return false;
      }

      var allowedReportIds = grant.Scope.AllowedReportIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
      if (allowedReportIds.Length > 0 && !allowedReportIds.Contains(report.Id))
      {
        return false;
      }

      return true;
    });
  }

  private static bool CanDeleteReport(User user, Report report)
  {
    if (user.Role == UserRole.Admin)
    {
      return true;
    }

    if (user.Role == UserRole.Patient)
    {
      return report.PatientId == user.Id;
    }

    if (user.Role == UserRole.LabTechnician)
    {
      return report.UploadedByUserId == user.Id;
    }

    return false;
  }

  private async Task<ReportSignedDownloadUrlResponse> CreateSignedDownloadUrlAsync(
    string objectKey,
    int? requestedExpiryMinutes,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var now = clock.UtcNow;
    var expiresAt = now.AddMinutes(GetExpiryMinutes(requestedExpiryMinutes));

    if (TryGetCloudFrontSignedUrl(objectKey, expiresAt, out var cloudFrontUrl))
    {
      return new ReportSignedDownloadUrlResponse(objectKey, new Uri(cloudFrontUrl, UriKind.Absolute), expiresAt, "cloudfront");
    }

    var presignRequest = new GetPreSignedUrlRequest
    {
      BucketName = _awsOptions.S3.BucketName,
      Key = objectKey,
      Verb = HttpVerb.GET,
      Expires = expiresAt.UtcDateTime,
      Protocol = ResolveProtocol()
    };

    var s3Url = await s3Client.GetPreSignedURLAsync(presignRequest);
    return new ReportSignedDownloadUrlResponse(objectKey, new Uri(s3Url, UriKind.Absolute), expiresAt, "s3");
  }

  private async Task<User> ResolvePatientAsync(
    User uploader,
    string? requestedPatientSub,
    string? requestedPatientPhone,
    string? requestedPatientAadhaar,
    CancellationToken cancellationToken)
  {
    if (uploader.Role != UserRole.LabTechnician)
    {
      if (string.IsNullOrWhiteSpace(requestedPatientSub))
      {
        return uploader;
      }

      var normalizedPatientSub = requestedPatientSub.Trim();
      if (!string.Equals(normalizedPatientSub, uploader.ExternalAuthId, StringComparison.Ordinal))
      {
        throw new InvalidOperationException("Only lab technicians can upload reports for another patient.");
      }

      return uploader;
    }

    if (!string.IsNullOrWhiteSpace(requestedPatientSub))
    {
      var patientBySub = await userRepository.GetByExternalAuthIdAsync(requestedPatientSub.Trim(), cancellationToken)
        ?? throw new InvalidOperationException("PatientSub does not match a provisioned user.");

      return EnsurePatientRole(patientBySub, "PatientSub must belong to a patient user.");
    }

    if (!string.IsNullOrWhiteSpace(requestedPatientPhone))
    {
      var patientByPhone = await ResolvePatientByPhoneAsync(requestedPatientPhone, cancellationToken)
        ?? throw new InvalidOperationException("PatientPhone does not match a provisioned patient user.");

      return patientByPhone;
    }

    if (!string.IsNullOrWhiteSpace(requestedPatientAadhaar))
    {
      var patientByAadhaar = await ResolvePatientByAadhaarAsync(requestedPatientAadhaar, cancellationToken)
        ?? throw new InvalidOperationException("PatientAadhaar does not match a provisioned patient user.");

      return patientByAadhaar;
    }

    throw new InvalidOperationException("PatientSub, PatientPhone, or PatientAadhaar is required for lab technician uploads.");
  }

  private async Task<User> ResolveUploaderAsync(string userSub, CancellationToken cancellationToken)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (user is not null)
    {
      return user;
    }

    if (!userSub.StartsWith("lab:", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException("Authenticated user is not provisioned in the database.");
    }

    return await userRepository.GetFirstByRoleAsync(UserRole.LabTechnician, cancellationToken)
      ?? throw new InvalidOperationException("No lab technician user is provisioned for API key uploads.");
  }

  private async Task<User?> ResolvePatientByPhoneAsync(string phoneInput, CancellationToken cancellationToken)
  {
    foreach (var candidate in ExpandPhoneCandidates(phoneInput))
    {
      var user = await userRepository.GetByPhoneAsync(candidate, cancellationToken);
      if (user is not null && user.Role == UserRole.Patient)
      {
        return user;
      }
    }

    return null;
  }

  private async Task<User?> ResolvePatientByAadhaarAsync(string aadhaarInput, CancellationToken cancellationToken)
  {
    var digitsOnly = new string(aadhaarInput.Where(char.IsDigit).ToArray());
    if (digitsOnly.Length != 12)
    {
      throw new InvalidOperationException("PatientAadhaar must contain exactly 12 digits.");
    }

    var aadhaarSha256 = SHA256.HashData(Encoding.UTF8.GetBytes(digitsOnly));
    var user = await userRepository.GetByAadhaarSha256Async(aadhaarSha256, cancellationToken);
    if (user is null)
    {
      return null;
    }

    return EnsurePatientRole(user, "PatientAadhaar must belong to a patient user.");
  }

  private static IEnumerable<string> ExpandPhoneCandidates(string phoneInput)
  {
    if (InMemoryPhoneOtpService.TryNormalizeIndianPhone(phoneInput, out var normalizedIndian))
    {
      yield return normalizedIndian;
      yield return normalizedIndian[3..];
      yield break;
    }

    var digitsOnly = new string(phoneInput.Where(char.IsDigit).ToArray());
    if (digitsOnly.Length == 10)
    {
      yield return digitsOnly;
      yield return $"+91{digitsOnly}";
      yield break;
    }

    if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91", StringComparison.Ordinal))
    {
      yield return digitsOnly;
      yield return $"+{digitsOnly}";
      yield return digitsOnly[2..];
      yield break;
    }

    if (!string.IsNullOrWhiteSpace(phoneInput))
    {
      yield return phoneInput.Trim();
    }
  }

  private static User EnsurePatientRole(User user, string message)
  {
    if (user.Role != UserRole.Patient)
    {
      throw new InvalidOperationException(message);
    }

    return user;
  }

  private static string ResolveSourceSystem(UserRole uploaderRole, string? requestedSourceSystem)
  {
    if (uploaderRole == UserRole.LabTechnician)
    {
      return "lab-upload";
    }

    if (!string.IsNullOrWhiteSpace(requestedSourceSystem))
    {
      return requestedSourceSystem.Trim();
    }

    return "api-upload";
  }

  private static void EnsureObjectKeyBelongsToUploader(string userSub, string objectKey)
  {
    var expectedPrefix = $"reports/{userSub}/";
    if (!objectKey.Trim().StartsWith(expectedPrefix, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("Object key does not belong to the uploader scope.");
    }
  }

  private async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(
    string objectKey,
    CancellationToken cancellationToken)
  {
    try
    {
      return await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
      {
        BucketName = _awsOptions.S3.BucketName,
        Key = objectKey.Trim()
      }, cancellationToken);
    }
    catch (AmazonS3Exception ex)
    {
      throw new InvalidOperationException("Uploaded file reference is invalid or unreachable in S3.", ex);
    }
  }

  private static string? ResolveChecksum(GetObjectMetadataResponse metadata)
  {
    var explicitChecksum = metadata.Metadata["x-amz-meta-sha256"];
    if (string.IsNullOrWhiteSpace(explicitChecksum))
    {
      explicitChecksum = metadata.Metadata["sha256"];
    }

    if (!string.IsNullOrWhiteSpace(explicitChecksum))
    {
      return explicitChecksum;
    }

    return null;
  }

  private static ReportResults BuildResults(CreateReportRequest request)
  {
    return new ReportResults
    {
      ReportVersion = 1,
      Notes = InputSanitizer.SanitizeNullablePlainText(request.Notes),
      Parameters = request.Parameters.Select(parameter => new ReportResultParameter
      {
        Code = InputSanitizer.SanitizePlainText(parameter.Code),
        Name = InputSanitizer.SanitizePlainText(parameter.Name),
        Value = parameter.Value,
        Unit = InputSanitizer.SanitizeNullablePlainText(parameter.Unit),
        ReferenceRange = InputSanitizer.SanitizeNullablePlainText(parameter.ReferenceRange),
        AbnormalFlag = parameter.IsAbnormal
      }).ToArray()
    };
  }

  private static ReportMetadata BuildMetadata(
    CreateReportRequest request,
    GetObjectMetadataResponse objectMetadata,
    ReportType reportType,
    string sourceSystem)
  {
    var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["object-key"] = request.ObjectKey.Trim(),
      ["report-type"] = ToReportTypeCode(reportType),
      ["content-type"] = objectMetadata.Headers.ContentType ?? string.Empty,
      ["size-bytes"] = objectMetadata.ContentLength.ToString(CultureInfo.InvariantCulture)
    };

    if (!string.IsNullOrWhiteSpace(request.LabName))
    {
      tags["lab-name"] = InputSanitizer.SanitizePlainText(request.LabName);
    }

    if (!string.IsNullOrWhiteSpace(request.LabCode))
    {
      tags["lab-code"] = InputSanitizer.SanitizePlainText(request.LabCode);
    }

    if (sourceSystem.Equals("lab-upload", StringComparison.OrdinalIgnoreCase))
    {
      tags["upload-origin"] = "lab";
    }

    return new ReportMetadata
    {
      SourceSystem = sourceSystem,
      Tags = tags
    };
  }

  private static List<ReportParameter> BuildParameters(
    IReadOnlyList<CreateReportParameterRequest> parameters,
    DateTimeOffset createdAt)
  {
    return parameters.Select(parameter => new ReportParameter
    {
      Id = Guid.NewGuid(),
      ParameterCode = InputSanitizer.SanitizePlainText(parameter.Code),
      ParameterName = InputSanitizer.SanitizePlainText(parameter.Name),
      MeasuredValueNumeric = parameter.Value,
      MeasuredValueText = InputSanitizer.SanitizeNullablePlainText(parameter.ValueText),
      Unit = InputSanitizer.SanitizeNullablePlainText(parameter.Unit),
      ReferenceRangeText = InputSanitizer.SanitizeNullablePlainText(parameter.ReferenceRange),
      IsAbnormal = parameter.IsAbnormal,
      RawParameter = new ReportParameterRaw
      {
        Attributes = InputSanitizer.SanitizeStringDictionary(parameter.Attributes)
      },
      CreatedAt = createdAt
    }).ToList();
  }

  private static ReportType ParseReportType(string reportType)
  {
    var normalized = reportType.Trim();
    if (normalized.Equals("blood_test", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.BloodTest;
    }

    if (normalized.Equals("urine_test", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.UrineTest;
    }

    if (normalized.Equals("radiology", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.Radiology;
    }

    if (normalized.Equals("cardiology", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.Cardiology;
    }

    if (normalized.Equals("other", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.Other;
    }

    throw new InvalidOperationException("Unsupported report type.");
  }

  private static ReportType? TryParseReportTypeOrNull(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    return ParseReportType(value);
  }

  private static ReportStatus? TryParseStatusOrNull(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var normalized = value.Trim();
    if (normalized.Equals("draft", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Draft;
    }

    if (normalized.Equals("uploaded", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Uploaded;
    }

    if (normalized.Equals("processing", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Processing;
    }

    if (normalized.Equals("clean", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Clean;
    }

    if (normalized.Equals("infected", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Infected;
    }

    if (normalized.Equals("validated", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Validated;
    }

    if (normalized.Equals("published", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Published;
    }

    if (normalized.Equals("archived", StringComparison.OrdinalIgnoreCase))
    {
      return ReportStatus.Archived;
    }

    throw new InvalidOperationException("Unsupported report status.");
  }

  private static string ToStatusString(ReportStatus status)
  {
    return status switch
    {
      ReportStatus.Draft => "draft",
      ReportStatus.Uploaded => "uploaded",
      ReportStatus.Processing => "processing",
      ReportStatus.Clean => "clean",
      ReportStatus.Infected => "infected",
      ReportStatus.Validated => "validated",
      ReportStatus.Published => "published",
      ReportStatus.Archived => "archived",
      _ => "uploaded"
    };
  }

  private static string ToReportTypeCode(ReportType reportType)
  {
    return reportType switch
    {
      ReportType.BloodTest => "blood_test",
      ReportType.UrineTest => "urine_test",
      ReportType.Radiology => "radiology",
      ReportType.Cardiology => "cardiology",
      ReportType.Other => "other",
      _ => "other"
    };
  }

  private async Task<string> GenerateReportNumberAsync(CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < 5; attempt++)
    {
      var candidate = $"RPT-{Guid.NewGuid():N}".ToUpperInvariant()[..14];
      var existing = await reportRepository.GetByReportNumberAsync(candidate, cancellationToken);
      if (existing is null)
      {
        return candidate;
      }
    }

    throw new InvalidOperationException("Could not allocate a unique report number.");
  }

  private int GetExpiryMinutes(int? requestedMinutes)
  {
    var configuredDefault = _awsOptions.S3.PresignedUrlExpiryMinutes;
    var candidate = requestedMinutes ?? configuredDefault;
    return Math.Clamp(candidate, 1, 10080);
  }

  private static string BuildObjectKey(string userSub, string fileName, DateTimeOffset now)
  {
    var extension = Path.GetExtension(fileName)?.Trim() ?? string.Empty;
    var extensionPart = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
    var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    var timestamp = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    return $"reports/{userSub}/{timestamp}-{random}{extensionPart}";
  }

  private Protocol ResolveProtocol()
  {
    if (_awsOptions.UseLocalStack)
    {
      return Protocol.HTTP;
    }

    return Protocol.HTTPS;
  }

  private bool TryGetCloudFrontSignedUrl(string objectKey, DateTimeOffset expiresAt, out string url)
  {
    var cloudFront = _awsOptions.S3.CloudFront;
    if (_awsOptions.UseLocalStack
      || !cloudFront.Enabled
      || string.IsNullOrWhiteSpace(cloudFront.DistributionDomain)
      || string.IsNullOrWhiteSpace(cloudFront.KeyPairId)
      || string.IsNullOrWhiteSpace(cloudFront.PrivateKeyPem))
    {
      url = string.Empty;
      return false;
    }

    var resourceUrl = $"https://{cloudFront.DistributionDomain.TrimEnd('/')}/{objectKey}";
    url = BuildCloudFrontSignedUrl(resourceUrl, cloudFront.KeyPairId, cloudFront.PrivateKeyPem, expiresAt);
    return true;
  }

  private static string BuildCloudFrontSignedUrl(
    string resourceUrl,
    string keyPairId,
    string privateKeyPemBase64,
    DateTimeOffset expiresAt)
  {
    var expiresAtEpoch = expiresAt.ToUnixTimeSeconds();
    var policy = $"{{\"Statement\":[{{\"Resource\":\"{resourceUrl}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{expiresAtEpoch}}}}}}}]}}";

    var privateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyPemBase64));
    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem);

    var policyBytes = Encoding.UTF8.GetBytes(policy);
    var signatureBytes = rsa.SignData(policyBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    var signature = ToCloudFrontSafeBase64(signatureBytes);

    var separator = resourceUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return $"{resourceUrl}{separator}Expires={expiresAtEpoch}&Signature={signature}&Key-Pair-Id={Uri.EscapeDataString(keyPairId)}";
  }

  private static string ToCloudFrontSafeBase64(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
      .Replace('+', '-')
      .Replace('=', '_')
      .Replace('/', '~');
  }
}
