using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class ReportPdfExtractionProcessorTests
{
  private readonly Mock<IReportRepository> _reportRepoMock;
  private readonly Mock<IUnitOfWork> _unitOfWorkMock;
  private readonly Mock<ITextExtractionProvider> _pdfPigMock;
  private readonly Mock<ITextExtractionProvider> _textractMock;
  private readonly Mock<IReportParameterExtractor> _llmExtractorMock;
  private readonly Mock<IAmazonS3> _s3Mock;
  private readonly ReportPdfExtractionProcessor _processor;

  private static readonly PdfExtractionOptions DefaultOptions = new()
  {
    EnableExtraction = true,
    MinTextLengthPerPage = 50,
    MaxRetryAttempts = 3,
    StoreRawExtractedText = true,
    LlmProvider = "ollama",
    OllamaModelId = "test-model",
    MinConfidenceThreshold = 0.5
  };

  public ReportPdfExtractionProcessorTests()
  {
    _reportRepoMock = new Mock<IReportRepository>();
    _unitOfWorkMock = new Mock<IUnitOfWork>();
    _s3Mock = new Mock<IAmazonS3>();
    _pdfPigMock = new Mock<ITextExtractionProvider>();
    _textractMock = new Mock<ITextExtractionProvider>();
    _llmExtractorMock = new Mock<IReportParameterExtractor>();

    var awsOptions = Options.Create(new AwsOptions { S3 = new S3Options { BucketName = "test-bucket" } });
    var extractionOptions = Options.Create(DefaultOptions);
    var logger = new Mock<ILogger<ReportPdfExtractionProcessor>>();

    _processor = new ReportPdfExtractionProcessor(
      _reportRepoMock.Object,
      _unitOfWorkMock.Object,
      _pdfPigMock.Object,
      _textractMock.Object,
      _llmExtractorMock.Object,
      _s3Mock.Object,
      awsOptions,
      extractionOptions,
      logger.Object);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSkipWhenExtractionDisabledAsync()
  {
    var awsOptions = Options.Create(new AwsOptions { S3 = new S3Options { BucketName = "test-bucket" } });
    var disabledOptions = Options.Create(new PdfExtractionOptions { EnableExtraction = false });
    var logger = new Mock<ILogger<ReportPdfExtractionProcessor>>();

    var processor = new ReportPdfExtractionProcessor(
      _reportRepoMock.Object,
      _unitOfWorkMock.Object,
      _pdfPigMock.Object,
      _textractMock.Object,
      _llmExtractorMock.Object,
      _s3Mock.Object,
      awsOptions,
      disabledOptions,
      logger.Object);

    await processor.ProcessReportAsync(Guid.NewGuid());

    _reportRepoMock.Verify(
      x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSkipWhenReportNotFoundAsync()
  {
    _reportRepoMock
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdForUpdateSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    await _processor.ProcessReportAsync(Guid.NewGuid());

    _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSkipWhenNoFileStorageKeyAsync()
  {
    var report = CreateReport(fileStorageKey: null);
    SetupReportLookup(report);

    await _processor.ProcessReportAsync(report.Id);

    _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSkipWhenStatusNotCleanAsync()
  {
    var report = CreateReport(status: ReportStatus.Uploaded);
    SetupReportLookup(report);

    await _processor.ProcessReportAsync(report.Id);

    _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSkipWhenMaxRetriesReachedAsync()
  {
    var report = CreateReport();
    report.Extraction = new ExtractionMetadata { AttemptCount = 3 };
    SetupReportLookup(report);

    await _processor.ProcessReportAsync(report.Id);

    _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldExtractAndSaveParametersAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("Hemoglobin: 14.5 g/dL");
    SetupPdfPigExtraction("Hemoglobin: 14.5 g/dL Reference: 12.0 - 17.5 Normal result in report", 1);
    SetupLlmExtraction(
    [
      new ExtractedParameter("HGB", "Hemoglobin", 14.5m, null, "g/dL", "12.0-17.5", false, 0.95)
    ]);

    await _processor.ProcessReportAsync(report.Id);

    report.Status.Should().Be(ReportStatus.Extracted);
    report.Parameters.Should().HaveCount(1);
    report.Parameters.First().ParameterCode.Should().Be("HGB");
    report.Parameters.First().Source.Should().Be("extracted");
    report.Extraction.Should().NotBeNull();
    report.Extraction!.ExtractionMethod.Should().Be("pdfpig");
    report.Extraction.ExtractedParameterCount.Should().Be(1);
    _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldFallbackToTextractWhenPdfPigInsufficientAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("x");
    SetupPdfPigExtraction("x", 1);
    SetupTextractExtraction("Hemoglobin: 14.5 g/dL Reference: 12.0-17.5 Normal result in report", 1);
    SetupLlmExtraction(
    [
      new ExtractedParameter("HGB", "Hemoglobin", 14.5m, null, "g/dL", "12.0-17.5", false, 0.9)
    ]);

    await _processor.ProcessReportAsync(report.Id);

    report.Status.Should().Be(ReportStatus.Extracted);
    report.Extraction!.ExtractionMethod.Should().Be("pdfpig+textract");
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSetExtractionFailedOnEmptyTextAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("");
    SetupPdfPigExtraction("", 0);
    SetupTextractExtraction("", 0);

    await _processor.ProcessReportAsync(report.Id);

    report.Status.Should().Be(ReportStatus.ExtractionFailed);
    report.Extraction!.ErrorMessage.Should().Contain("No text");
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldHandleExceptionGracefullyAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    _s3Mock
      .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("S3 error"));

    await _processor.ProcessReportAsync(report.Id);

    report.Status.Should().Be(ReportStatus.ExtractionFailed);
    report.Extraction!.ErrorMessage.Should().Be("S3 error");
    report.Extraction.AttemptCount.Should().Be(1);
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldNotOverwriteManualParametersAsync()
  {
    var report = CreateReport();
    report.Parameters.Add(new ReportParameter
    {
      Id = Guid.NewGuid(),
      ReportId = report.Id,
      ParameterCode = "HGB",
      ParameterName = "Hemoglobin",
      MeasuredValueNumeric = 15.0m,
      Source = "manual"
    });
    SetupReportLookup(report);
    SetupS3Download("Hemoglobin: 14.5 g/dL WBC: 7500 /uL");
    SetupPdfPigExtraction("Hemoglobin: 14.5 g/dL WBC: 7500 /uL plus more content for threshold", 1);
    SetupLlmExtraction(
    [
      new ExtractedParameter("HGB", "Hemoglobin", 14.5m, null, "g/dL", "12.0-17.5", false, 0.95),
      new ExtractedParameter("WBC", "White Blood Cells", 7500m, null, "/uL", "4000-11000", false, 0.9)
    ]);

    await _processor.ProcessReportAsync(report.Id);

    report.Parameters.Should().HaveCount(2);
    var manualParam = report.Parameters.Single(p => p.Source == "manual");
    manualParam.MeasuredValueNumeric.Should().Be(15.0m);
    var extractedParam = report.Parameters.Single(p => p.Source == "extracted");
    extractedParam.ParameterCode.Should().Be("WBC");
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldRemoveExtractedParamsOnForceReprocessAsync()
  {
    var report = CreateReport(status: ReportStatus.Extracted);
    report.Extraction = new ExtractionMetadata { AttemptCount = 1 };
    report.Parameters.Add(new ReportParameter
    {
      Id = Guid.NewGuid(),
      ReportId = report.Id,
      ParameterCode = "OLD",
      ParameterName = "Old Extracted",
      Source = "extracted"
    });
    report.Parameters.Add(new ReportParameter
    {
      Id = Guid.NewGuid(),
      ReportId = report.Id,
      ParameterCode = "MANUAL",
      ParameterName = "Manual Entry",
      Source = "manual"
    });
    SetupReportLookup(report);
    SetupS3Download("Fresh text");
    SetupPdfPigExtraction("Fresh text for extraction with enough content per page to pass threshold", 1);
    SetupLlmExtraction(
    [
      new ExtractedParameter("NEW", "New Extracted", 42m, null, "mg/dL", "10-50", false, 0.85)
    ]);

    await _processor.ProcessReportAsync(report.Id, forceReprocess: true);

    report.Parameters.Should().HaveCount(2);
    report.Parameters.Should().Contain(p => p.ParameterCode == "MANUAL");
    report.Parameters.Should().Contain(p => p.ParameterCode == "NEW");
    report.Parameters.Should().NotContain(p => p.ParameterCode == "OLD");
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldStoreRawTextWhenConfiguredAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("Raw extracted text from PDF document");
    SetupPdfPigExtraction("Raw extracted text from PDF document with lots of content that exceeds min threshold", 1);
    SetupLlmExtraction([]);

    await _processor.ProcessReportAsync(report.Id);

    report.Extraction!.RawExtractedText.Should().NotBeNull();
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldSetExtractionFailed_WhenLlmTimesOutAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("Hemoglobin: 14.5 g/dL with enough text to pass min threshold per page");
    SetupPdfPigExtraction("Hemoglobin: 14.5 g/dL with enough text to pass min threshold per page", 1);

    var timeoutException = new TaskCanceledException(
      "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
      new TimeoutException("The operation was canceled."));

    _llmExtractorMock
      .Setup(x => x.ExtractParametersAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(timeoutException);

    await _processor.ProcessReportAsync(report.Id);

    report.Status.Should().Be(ReportStatus.ExtractionFailed);
    report.Extraction!.ErrorMessage.Should().Contain("timed out");
  }

  [Fact]
  public async Task ProcessReportAsync_ShouldRethrow_WhenCancellationIsRequestedAsync()
  {
    var report = CreateReport();
    SetupReportLookup(report);
    SetupS3Download("Hemoglobin: 14.5 g/dL with enough text to pass min threshold per page");
    SetupPdfPigExtraction("Hemoglobin: 14.5 g/dL with enough text to pass min threshold per page", 1);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    _llmExtractorMock
      .Setup(x => x.ExtractParametersAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException(cts.Token));

    var act = () => _processor.ProcessReportAsync(report.Id, cancellationToken: cts.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();
    report.Status.Should().Be(ReportStatus.Extracting);
  }

  private static Report CreateReport(
    ReportStatus status = ReportStatus.Clean,
    string? fileStorageKey = "reports/test.pdf")
  {
    return new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-001",
      PatientId = Guid.NewGuid(),
      UploadedByUserId = Guid.NewGuid(),
      ReportType = ReportType.BloodTest,
      Status = status,
      FileStorageKey = fileStorageKey
    };
  }

  private void SetupReportLookup(Report report)
  {
    _reportRepoMock
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdForUpdateSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);
  }

  private void SetupS3Download(string content)
  {
    var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
    _s3Mock
      .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetObjectResponse { ResponseStream = stream });
  }

  private void SetupPdfPigExtraction(string text, int pageCount)
  {
    _pdfPigMock
      .Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TextExtractionResult(text, "pdfpig", pageCount, false, new Dictionary<string, string>()));
  }

  private void SetupTextractExtraction(string text, int pageCount)
  {
    _textractMock
      .Setup(x => x.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TextExtractionResult(text, "textract", pageCount, true, new Dictionary<string, string>()));
  }

  private void SetupLlmExtraction(IReadOnlyList<ExtractedParameter> parameters)
  {
    _llmExtractorMock
      .Setup(x => x.ExtractParametersAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new StructuredExtractionResult(parameters, null, 0.9, "test-model"));
  }
}
