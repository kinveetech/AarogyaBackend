using System.Security.Cryptography;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class S3ReportChecksumVerificationService(
  IAmazonS3 s3Client,
  IUserRepository userRepository,
  IReportRepository reportRepository,
  IOptions<AwsOptions> awsOptions,
  IUtcClock clock,
  ILogger<S3ReportChecksumVerificationService> logger)
  : IReportChecksumVerificationService
{
  public async Task<VerifiedReportDownloadResponse> CreateVerifiedDownloadUrlAsync(
    string userSub,
    CreateVerifiedReportDownloadRequest request,
    CancellationToken cancellationToken = default)
  {
    if (request.ReportId == Guid.Empty)
    {
      throw new InvalidOperationException("ReportId is required.");
    }

    var requester = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (requester is null)
    {
      throw new InvalidOperationException("Authenticated user is not provisioned in the database.");
    }

    var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
    if (report is null)
    {
      throw new KeyNotFoundException("Report not found.");
    }

    if (!CanAccessReport(requester.Id, requester.Role, report.PatientId, report.UploadedByUserId))
    {
      throw new UnauthorizedAccessException("You are not authorized to access this report.");
    }

    if (string.IsNullOrWhiteSpace(report.FileStorageKey))
    {
      throw new InvalidOperationException("Report does not have a file storage key.");
    }

    var expectedChecksum = report.ChecksumSha256?.Trim();
    if (string.IsNullOrWhiteSpace(expectedChecksum))
    {
      throw new InvalidOperationException("Report checksum is missing.");
    }

    var actualChecksum = await ComputeChecksumFromStorageAsync(report.FileStorageKey, cancellationToken);
    var checksumVerified = string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase);
    if (!checksumVerified)
    {
      logger.LogError(
        "Checksum mismatch detected for report {ReportId}. Expected {ExpectedChecksum}, actual {ActualChecksum}.",
        report.Id,
        expectedChecksum,
        actualChecksum);

      throw new InvalidOperationException("Checksum verification failed. Download blocked.");
    }

    var expiresAt = clock.UtcNow.AddMinutes(GetExpiryMinutes(request.ExpiryMinutes));
    var downloadUrl = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
    {
      BucketName = awsOptions.Value.S3.BucketName,
      Key = report.FileStorageKey,
      Verb = HttpVerb.GET,
      Expires = expiresAt.UtcDateTime,
      Protocol = awsOptions.Value.UseLocalStack ? Protocol.HTTP : Protocol.HTTPS
    });

    return new VerifiedReportDownloadResponse(
      report.Id,
      report.FileStorageKey,
      new Uri(downloadUrl, UriKind.Absolute),
      expiresAt,
      checksumVerified);
  }

  private static bool CanAccessReport(Guid requesterId, UserRole role, Guid patientId, Guid uploadedByUserId)
  {
    if (requesterId == patientId || requesterId == uploadedByUserId)
    {
      return true;
    }

    return role is UserRole.Admin or UserRole.Doctor;
  }

  private int GetExpiryMinutes(int? requestedMinutes)
  {
    var configured = awsOptions.Value.S3.PresignedUrlExpiryMinutes;
    return Math.Clamp(requestedMinutes ?? configured, 1, 10080);
  }

  private async Task<string> ComputeChecksumFromStorageAsync(string objectKey, CancellationToken cancellationToken)
  {
    using var response = await s3Client.GetObjectAsync(new GetObjectRequest
    {
      BucketName = awsOptions.Value.S3.BucketName,
      Key = objectKey
    }, cancellationToken);

    await using var stream = response.ResponseStream;
    using var sha256 = SHA256.Create();
    var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
    return Convert.ToHexString(hash);
  }
}
