using System.Globalization;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ReportVirusScanProcessor(
  IReportRepository reportRepository,
  IUnitOfWork unitOfWork,
  IReportVirusScanner virusScanner,
  IAmazonS3 s3Client,
  IOptions<AwsOptions> awsOptions,
  IOptions<VirusScanningOptions> scanOptions,
  ILogger<ReportVirusScanProcessor> logger)
  : IReportVirusScanProcessor
{
  public async Task ProcessUploadAsync(
    S3UploadEventRecord record,
    CancellationToken cancellationToken = default)
  {
    if (!scanOptions.Value.EnableScanning)
    {
      logger.LogDebug("Virus scanning is disabled; skipping object {Bucket}/{Key}.", record.BucketName, record.ObjectKey);
      return;
    }

    var report = await reportRepository.GetByFileStorageKeyAsync(record.ObjectKey, cancellationToken);
    if (report is null)
    {
      logger.LogWarning("No report matched uploaded object key {ObjectKey}.", record.ObjectKey);
      return;
    }

    report.Status = ReportStatus.Processing;
    report.UpdatedAt = DateTimeOffset.UtcNow;
    reportRepository.Update(report);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    var scanResult = await virusScanner.ScanObjectAsync(record.BucketName, record.ObjectKey, cancellationToken);

    if (scanResult.IsInfected)
    {
      var quarantineKey = await MoveToQuarantineAsync(record, cancellationToken);
      report.Status = ReportStatus.Infected;
      report.Metadata.Tags["scan-status"] = "infected";
      report.Metadata.Tags["scan-engine"] = scanResult.Engine;
      report.Metadata.Tags["scan-signature"] = scanResult.Signature ?? "unknown";
      report.Metadata.Tags["quarantine-key"] = quarantineKey;
    }
    else
    {
      report.Status = ReportStatus.Clean;
      report.Metadata.Tags["scan-status"] = "clean";
      report.Metadata.Tags["scan-engine"] = scanResult.Engine;
    }

    report.Metadata.Tags["scan-time-utc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    report.UpdatedAt = DateTimeOffset.UtcNow;
    reportRepository.Update(report);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    logger.LogInformation(
      "Virus scan completed for report {ReportId}: status={Status}, infected={IsInfected}, signature={Signature}, objectKey={ObjectKey}",
      report.Id,
      report.Status,
      scanResult.IsInfected,
      scanResult.Signature ?? "none",
      record.ObjectKey);
  }

  private async Task<string> MoveToQuarantineAsync(
    S3UploadEventRecord record,
    CancellationToken cancellationToken)
  {
    var quarantineBucket = ResolveQuarantineBucketName();
    var quarantinePrefix = scanOptions.Value.QuarantinePrefix.Trim('/');
    var quarantineKey = string.IsNullOrWhiteSpace(quarantinePrefix)
      ? record.ObjectKey
      : $"{quarantinePrefix}/{record.ObjectKey}";

    await s3Client.CopyObjectAsync(new CopyObjectRequest
    {
      SourceBucket = record.BucketName,
      SourceKey = record.ObjectKey,
      DestinationBucket = quarantineBucket,
      DestinationKey = quarantineKey
    }, cancellationToken);

    await s3Client.DeleteObjectAsync(new DeleteObjectRequest
    {
      BucketName = record.BucketName,
      Key = record.ObjectKey
    }, cancellationToken);

    return quarantineKey;
  }

  private string ResolveQuarantineBucketName()
  {
    if (!string.IsNullOrWhiteSpace(scanOptions.Value.QuarantineBucketName))
    {
      return scanOptions.Value.QuarantineBucketName;
    }

    var sourceBucket = awsOptions.Value.S3.BucketName;
    if (string.IsNullOrWhiteSpace(sourceBucket))
    {
      throw new InvalidOperationException("Quarantine bucket is not configured.");
    }

    return $"{sourceBucket}-quarantine";
  }
}
