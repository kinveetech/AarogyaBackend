using Aarogya.Api.Features.V1.Reports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class ClamAvDefinitionsUpdaterHostedServiceTests
{
  [Fact]
  public async Task ExecuteAsync_ShouldSkipRefresh_WhenScanningDisabledAsync()
  {
    var scanner = new Mock<IReportVirusScanner>();
    var options = Options.Create(new VirusScanningOptions { EnableScanning = false });
    var sut = new ClamAvDefinitionsUpdaterHostedService(
      scanner.Object,
      options,
      NullLogger<ClamAvDefinitionsUpdaterHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(50);
    await sut.StopAsync(cts.Token);

    scanner.Verify(
      x => x.RefreshDefinitionsAsync(It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldRefreshDefinitionsOnce_WhenCancelledImmediatelyAsync()
  {
    var scanner = new Mock<IReportVirusScanner>();
    scanner
      .Setup(x => x.RefreshDefinitionsAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var options = Options.Create(new VirusScanningOptions
    {
      EnableScanning = true,
      DefinitionsRefreshIntervalMinutes = 60
    });

    var sut = new ClamAvDefinitionsUpdaterHostedService(
      scanner.Object,
      options,
      NullLogger<ClamAvDefinitionsUpdaterHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    scanner.Verify(
      x => x.RefreshDefinitionsAsync(It.IsAny<CancellationToken>()),
      Times.AtLeastOnce);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldCallInitialRefresh_WhenScanningEnabledAsync()
  {
    var refreshCalled = false;
    var scanner = new Mock<IReportVirusScanner>();
    scanner
      .Setup(x => x.RefreshDefinitionsAsync(It.IsAny<CancellationToken>()))
      .Callback(() => refreshCalled = true)
      .Returns(Task.CompletedTask);

    var options = Options.Create(new VirusScanningOptions
    {
      EnableScanning = true,
      DefinitionsRefreshIntervalMinutes = 60
    });

    var sut = new ClamAvDefinitionsUpdaterHostedService(
      scanner.Object,
      options,
      NullLogger<ClamAvDefinitionsUpdaterHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    refreshCalled.Should().BeTrue();
  }
}
