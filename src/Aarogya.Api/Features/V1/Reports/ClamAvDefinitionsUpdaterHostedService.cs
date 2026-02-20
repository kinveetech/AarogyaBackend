using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ClamAvDefinitionsUpdaterHostedService(
  IReportVirusScanner virusScanner,
  IOptions<VirusScanningOptions> options,
  ILogger<ClamAvDefinitionsUpdaterHostedService> logger)
  : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!options.Value.EnableScanning)
    {
      logger.LogInformation("ClamAV definitions updater is disabled.");
      return;
    }

    await virusScanner.RefreshDefinitionsAsync(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.DefinitionsRefreshIntervalMinutes));
    while (!stoppingToken.IsCancellationRequested
      && await timer.WaitForNextTickAsync(stoppingToken))
    {
      try
      {
        await virusScanner.RefreshDefinitionsAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Failed to refresh ClamAV definitions.");
      }
    }
  }
}
