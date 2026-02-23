using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class ReportPdfExtractionHostedServiceTests
{
  [Fact]
  public async Task ExecuteAsync_ShouldReturnImmediately_WhenWorkerDisabledAsync()
  {
    var scopeFactory = new Mock<IServiceScopeFactory>();
    var options = Options.Create(new PdfExtractionOptions { EnableAutoExtractionWorker = false });

    var sut = new ReportPdfExtractionHostedService(
      scopeFactory.Object,
      options,
      NullLogger<ReportPdfExtractionHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    scopeFactory.Verify(
      x => x.CreateScope(),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldProcessCleanReports_WhenWorkerEnabledAsync()
  {
    var reportId = Guid.NewGuid();
    var report = new Report { Id = reportId, Status = ReportStatus.Clean, FileStorageKey = "test.pdf" };

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.ListAsync(
        It.IsAny<CleanReportsAwaitingExtractionSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync([report]);

    var processor = new Mock<IReportPdfExtractionProcessor>();
    processor
      .Setup(x => x.ProcessReportAsync(
        reportId,
        It.IsAny<bool>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportRepository)))
      .Returns(reportRepository.Object);
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportPdfExtractionProcessor)))
      .Returns(processor.Object);

    var scope = new Mock<IServiceScope>();
    scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

    var scopeFactory = new Mock<IServiceScopeFactory>();
    scopeFactory
      .Setup(x => x.CreateScope())
      .Returns(scope.Object);

    var options = Options.Create(new PdfExtractionOptions
    {
      EnableAutoExtractionWorker = true,
      WorkerIntervalMinutes = 60,
      BatchSize = 10
    });

    var sut = new ReportPdfExtractionHostedService(
      scopeFactory.Object,
      options,
      NullLogger<ReportPdfExtractionHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(200);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    processor.Verify(
      x => x.ProcessReportAsync(reportId, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.AtLeastOnce);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldNotCallProcessor_WhenNoCleanReportsAsync()
  {
    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.ListAsync(
        It.IsAny<CleanReportsAwaitingExtractionSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var processor = new Mock<IReportPdfExtractionProcessor>();

    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportRepository)))
      .Returns(reportRepository.Object);
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportPdfExtractionProcessor)))
      .Returns(processor.Object);

    var scope = new Mock<IServiceScope>();
    scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

    var scopeFactory = new Mock<IServiceScopeFactory>();
    scopeFactory
      .Setup(x => x.CreateScope())
      .Returns(scope.Object);

    var options = Options.Create(new PdfExtractionOptions
    {
      EnableAutoExtractionWorker = true,
      WorkerIntervalMinutes = 60,
      BatchSize = 5
    });

    var sut = new ReportPdfExtractionHostedService(
      scopeFactory.Object,
      options,
      NullLogger<ReportPdfExtractionHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(200);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    processor.Verify(
      x => x.ProcessReportAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldContinueProcessing_WhenSingleReportFailsAsync()
  {
    var reportId1 = Guid.NewGuid();
    var reportId2 = Guid.NewGuid();
    var report1 = new Report { Id = reportId1, Status = ReportStatus.Clean, FileStorageKey = "test1.pdf" };
    var report2 = new Report { Id = reportId2, Status = ReportStatus.Clean, FileStorageKey = "test2.pdf" };

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.ListAsync(
        It.IsAny<CleanReportsAwaitingExtractionSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync([report1, report2]);

    var processor = new Mock<IReportPdfExtractionProcessor>();
    processor
      .Setup(x => x.ProcessReportAsync(
        reportId1,
        It.IsAny<bool>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Extraction failed"));
    processor
      .Setup(x => x.ProcessReportAsync(
        reportId2,
        It.IsAny<bool>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportRepository)))
      .Returns(reportRepository.Object);
    serviceProvider
      .Setup(x => x.GetService(typeof(IReportPdfExtractionProcessor)))
      .Returns(processor.Object);

    var scope = new Mock<IServiceScope>();
    scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

    var scopeFactory = new Mock<IServiceScopeFactory>();
    scopeFactory
      .Setup(x => x.CreateScope())
      .Returns(scope.Object);

    var options = Options.Create(new PdfExtractionOptions
    {
      EnableAutoExtractionWorker = true,
      WorkerIntervalMinutes = 60,
      BatchSize = 10
    });

    var sut = new ReportPdfExtractionHostedService(
      scopeFactory.Object,
      options,
      NullLogger<ReportPdfExtractionHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(200);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    processor.Verify(
      x => x.ProcessReportAsync(reportId2, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.AtLeastOnce);
  }
}
