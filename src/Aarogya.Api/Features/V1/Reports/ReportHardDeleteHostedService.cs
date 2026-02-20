using System.Net;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ReportHardDeleteHostedService(
  IReportRepository reportRepository,
  IUnitOfWork unitOfWork,
  IAmazonS3 s3Client,
  IOptions<AwsOptions> awsOptions,
  IOptions<FileDeletionOptions> fileDeletionOptions,
  IUtcClock clock,
  ILogger<ReportHardDeleteHostedService> logger)
  : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var options = fileDeletionOptions.Value;
    if (!options.EnableHardDeleteWorker)
    {
      logger.LogInformation("Report hard-delete worker is disabled.");
      return;
    }

    var interval = TimeSpan.FromMinutes(Math.Max(1, options.WorkerIntervalMinutes));
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RunHardDeleteCycleAsync(options, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Unexpected error while processing report hard deletion.");
      }

      await Task.Delay(interval, stoppingToken);
    }
  }

  private async Task RunHardDeleteCycleAsync(
    FileDeletionOptions options,
    CancellationToken cancellationToken)
  {
    var threshold = clock.UtcNow.AddDays(-options.RetentionDays);
    var dueReports = await reportRepository.ListDueForHardDeleteAsync(
      threshold,
      IReportRepository.HardDeleteBatchSize,
      cancellationToken);

    if (dueReports.Count == 0)
    {
      return;
    }

    var now = clock.UtcNow;
    var hardDeletedCount = 0;

    foreach (var report in dueReports)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!await TryDeleteObjectFromStorageAsync(report.FileStorageKey, cancellationToken))
      {
        continue;
      }

      report.HardDeletedAt = now;
      report.FileStorageKey = null;
      report.ChecksumSha256 = null;
      reportRepository.Update(report);
      hardDeletedCount++;
    }

    if (hardDeletedCount > 0)
    {
      await unitOfWork.SaveChangesAsync(cancellationToken);
      logger.LogInformation(
        "Hard-deleted {Count} reports from storage older than {Threshold}.",
        hardDeletedCount,
        threshold);
    }
  }

  private async Task<bool> TryDeleteObjectFromStorageAsync(string? fileStorageKey, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(fileStorageKey))
    {
      return true;
    }

    try
    {
      await s3Client.DeleteObjectAsync(new DeleteObjectRequest
      {
        BucketName = awsOptions.Value.S3.BucketName,
        Key = fileStorageKey
      }, cancellationToken);
      return true;
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      logger.LogWarning(
        ex,
        "Object already missing during hard delete: bucket={BucketName}, key={ObjectKey}",
        awsOptions.Value.S3.BucketName,
        fileStorageKey);
      return true;
    }
    catch (Exception ex)
    {
      logger.LogError(
        ex,
        "Failed to hard-delete report object: bucket={BucketName}, key={ObjectKey}",
        awsOptions.Value.S3.BucketName,
        fileStorageKey);
      return false;
    }
  }
}
