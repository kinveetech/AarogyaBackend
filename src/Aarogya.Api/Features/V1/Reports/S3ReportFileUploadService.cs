using System.Globalization;
using System.Security.Cryptography;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.ValueObjects;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class S3ReportFileUploadService(
  IAmazonS3 s3Client,
  IUserRepository userRepository,
  IReportRepository reportRepository,
  IUnitOfWork unitOfWork,
  IOptions<AwsOptions> awsOptions,
  IUtcClock clock)
  : IReportFileUploadService
{
  private const int MaxFileSizeBytes = 50 * 1024 * 1024;
  private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    "application/pdf",
    "image/jpeg",
    "image/png"
  };

  public async Task<ReportUploadResponse> UploadAsync(
    string userSub,
    IFormFile file,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(file);

    ValidateFile(file);

    var uploader = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (uploader is null)
    {
      throw new InvalidOperationException("Authenticated user is not provisioned in the database.");
    }

    var uploadedAt = clock.UtcNow;
    var objectKey = BuildStorageKey(userSub, file.FileName, uploadedAt);

    await using var fileReadStream = file.OpenReadStream();
    await using var uploadStream = new MemoryStream(capacity: (int)file.Length);
    await fileReadStream.CopyToAsync(uploadStream, cancellationToken);
    uploadStream.Position = 0;

    var checksum = ComputeSha256(uploadStream);
    uploadStream.Position = 0;

    await s3Client.PutObjectAsync(new PutObjectRequest
    {
      BucketName = awsOptions.Value.S3.BucketName,
      Key = objectKey,
      InputStream = uploadStream,
      AutoCloseStream = false,
      AutoResetStreamPosition = false,
      ContentType = file.ContentType.Trim(),
      Metadata =
      {
        ["uploaded-by-sub"] = userSub,
        ["original-file-name"] = file.FileName,
        ["sha256"] = checksum
      }
    }, cancellationToken);

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = await GenerateReportNumberAsync(cancellationToken),
      PatientId = uploader.Id,
      UploadedByUserId = uploader.Id,
      ReportType = ResolveReportType(file.ContentType),
      Status = ReportStatus.Processing,
      SourceSystem = "api-upload",
      UploadedAt = uploadedAt,
      FileStorageKey = objectKey,
      ChecksumSha256 = checksum,
      Metadata = new ReportMetadata
      {
        SourceSystem = "api-upload",
        Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["content-type"] = file.ContentType.Trim(),
          ["size-bytes"] = file.Length.ToString(CultureInfo.InvariantCulture),
          ["original-file-name"] = file.FileName,
          ["scan-status"] = "pending"
        }
      }
    };

    await reportRepository.AddAsync(report, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return new ReportUploadResponse(
      report.Id,
      report.ReportNumber,
      objectKey,
      file.ContentType.Trim(),
      file.Length,
      checksum,
      uploadedAt);
  }

  private static void ValidateFile(IFormFile file)
  {
    if (file.Length <= 0)
    {
      throw new InvalidOperationException("Uploaded file is empty.");
    }

    if (file.Length > MaxFileSizeBytes)
    {
      throw new InvalidOperationException("Uploaded file exceeds the maximum size limit of 50 MB.");
    }

    var contentType = file.ContentType.Trim();
    if (!AllowedContentTypes.Contains(contentType))
    {
      throw new InvalidOperationException("Only PDF, JPEG, and PNG files are supported.");
    }
  }

  private static string BuildStorageKey(string userSub, string fileName, DateTimeOffset uploadedAt)
  {
    var extension = Path.GetExtension(fileName);
    var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
    return $"reports/{userSub}/{uploadedAt:yyyy/MM/dd}/{Guid.NewGuid():N}{normalizedExtension}";
  }

  private static string ComputeSha256(Stream stream)
  {
    using var sha256 = SHA256.Create();
    var hashBytes = sha256.ComputeHash(stream);
    return Convert.ToHexString(hashBytes);
  }

  private static ReportType ResolveReportType(string contentType)
  {
    var normalized = contentType.Trim();
    if (normalized.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
      || normalized.Equals("image/png", StringComparison.OrdinalIgnoreCase))
    {
      return ReportType.Radiology;
    }

    return ReportType.Other;
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
}
