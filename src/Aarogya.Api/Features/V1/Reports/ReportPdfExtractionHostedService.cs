using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Major Code Smell",
  "S3267",
  Justification = "Loop requires per-item async error handling which LINQ Select cannot express.")]
internal sealed class ReportPdfExtractionHostedService(
  IServiceScopeFactory scopeFactory,
  IOptions<PdfExtractionOptions> extractionOptions,
  ILogger<ReportPdfExtractionHostedService> logger)
  : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var options = extractionOptions.Value;
    if (!options.EnableAutoExtractionWorker)
    {
      logger.LogInformation("PDF extraction worker is disabled.");
      return;
    }

    var interval = TimeSpan.FromMinutes(Math.Max(1, options.WorkerIntervalMinutes));
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RunExtractionCycleAsync(options, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Unexpected error during PDF extraction cycle.");
      }

      await Task.Delay(interval, stoppingToken);
    }
  }

  private async Task RunExtractionCycleAsync(
    PdfExtractionOptions options,
    CancellationToken cancellationToken)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    var reportRepository = scope.ServiceProvider.GetRequiredService<IReportRepository>();
    var processor = scope.ServiceProvider.GetRequiredService<IReportPdfExtractionProcessor>();

    var cleanReports = await reportRepository.ListAsync(
      new CleanReportsAwaitingExtractionSpecification(options.BatchSize),
      cancellationToken);

    if (cleanReports.Count == 0)
    {
      return;
    }

    logger.LogInformation(
      "Found {Count} clean reports awaiting PDF extraction.",
      cleanReports.Count);

    foreach (var report in cleanReports)
    {
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        await processor.ProcessReportAsync(report.Id, cancellationToken: cancellationToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.LogError(
          ex,
          "Failed to process report {ReportId} during extraction cycle.",
          report.Id);
      }
    }
  }
}
