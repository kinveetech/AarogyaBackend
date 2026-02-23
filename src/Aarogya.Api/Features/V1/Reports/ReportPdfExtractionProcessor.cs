using Aarogya.Api.Configuration;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ReportPdfExtractionProcessor(
  IReportRepository reportRepository,
  IUnitOfWork unitOfWork,
  [FromKeyedServices("pdfpig")] ITextExtractionProvider pdfPigProvider,
  [FromKeyedServices("textract")] ITextExtractionProvider textractProvider,
  IReportParameterExtractor llmExtractor,
  IAmazonS3 s3Client,
  IOptions<AwsOptions> awsOptions,
  IOptions<PdfExtractionOptions> extractionOptions,
  ILogger<ReportPdfExtractionProcessor> logger) : IReportPdfExtractionProcessor
{
  public async Task ProcessReportAsync(
    Guid reportId,
    bool forceReprocess = false,
    CancellationToken cancellationToken = default)
  {
    var options = extractionOptions.Value;
    if (!options.EnableExtraction)
    {
      logger.LogInformation("PDF extraction is disabled, skipping report {ReportId}", reportId);
      return;
    }

    var report = await reportRepository.FirstOrDefaultAsync(
      new ReportByIdForUpdateSpecification(reportId), cancellationToken);

    if (report is null)
    {
      logger.LogWarning("Report {ReportId} not found for extraction", reportId);
      return;
    }

    if (string.IsNullOrEmpty(report.FileStorageKey))
    {
      logger.LogWarning("Report {ReportId} has no file storage key", reportId);
      return;
    }

    if (!forceReprocess && report.Status != ReportStatus.Clean)
    {
      logger.LogInformation(
        "Report {ReportId} is in status {Status}, skipping extraction",
        reportId,
        report.Status);
      return;
    }

    if (!forceReprocess && report.Extraction?.AttemptCount >= options.MaxRetryAttempts)
    {
      logger.LogInformation(
        "Report {ReportId} has reached max retry attempts ({AttemptCount}), skipping",
        reportId,
        report.Extraction.AttemptCount);
      return;
    }

    report.Extraction ??= new ExtractionMetadata();
    report.Extraction.AttemptCount++;

    try
    {
      report.Status = ReportStatus.Extracting;
      await unitOfWork.SaveChangesAsync(cancellationToken);

      var textResult = await ExtractTextAsync(report, options, cancellationToken);

      report.Extraction.ExtractionMethod = textResult.Method;
      report.Extraction.PageCount = textResult.PageCount;
      report.Extraction.ExtractedAt = DateTimeOffset.UtcNow;

      if (options.StoreRawExtractedText)
      {
        report.Extraction.RawExtractedText = textResult.ExtractedText;
      }

      foreach (var (key, value) in textResult.ProviderMetadata)
      {
        report.Extraction.ProviderMetadata[key] = value;
      }

      if (string.IsNullOrWhiteSpace(textResult.ExtractedText))
      {
        logger.LogWarning("No text extracted from report {ReportId}", reportId);
        report.Status = ReportStatus.ExtractionFailed;
        report.Extraction.ErrorMessage = "No text could be extracted from the PDF.";
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return;
      }

      var reportTypeCode = report.ReportType.ToString().ToUpperInvariant();
      var structuredResult = await llmExtractor.ExtractParametersAsync(
        textResult.ExtractedText,
        reportTypeCode,
        cancellationToken);

      report.Extraction.StructuringModel = structuredResult.ModelId;
      report.Extraction.OverallConfidence = structuredResult.OverallConfidence;
      report.Extraction.StructuredAt = DateTimeOffset.UtcNow;

      if (forceReprocess)
      {
        RemoveExtractedParameters(report);
      }

      var addedCount = AddExtractedParameters(report, structuredResult);
      report.Extraction.ExtractedParameterCount = addedCount;

      report.Status = ReportStatus.Extracted;
      report.Extraction.ErrorMessage = null;

      await unitOfWork.SaveChangesAsync(cancellationToken);

      logger.LogInformation(
        "Successfully extracted {ParameterCount} parameters from report {ReportId} with confidence {Confidence:F2}",
        addedCount,
        reportId,
        structuredResult.OverallConfidence);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(
        ex,
        "Extraction failed for report {ReportId} (attempt {Attempt})",
        reportId,
        report.Extraction.AttemptCount);

      report.Status = ReportStatus.ExtractionFailed;
      report.Extraction.ErrorMessage = ex.Message;

      await unitOfWork.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<TextExtractionResult> ExtractTextAsync(
    Report report,
    PdfExtractionOptions options,
    CancellationToken cancellationToken)
  {
    using var objectResponse = await s3Client.GetObjectAsync(new GetObjectRequest
    {
      BucketName = awsOptions.Value.S3.BucketName,
      Key = report.FileStorageKey
    }, cancellationToken);

    using var memoryStream = new MemoryStream();
    await objectResponse.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
    memoryStream.Position = 0;

    var pdfPigResult = await pdfPigProvider.ExtractTextAsync(
      memoryStream, report.FileStorageKey!, cancellationToken);

    var textPerPage = pdfPigResult.PageCount > 0
      ? pdfPigResult.ExtractedText.Length / pdfPigResult.PageCount
      : 0;

    if (textPerPage >= options.MinTextLengthPerPage)
    {
      return pdfPigResult;
    }

    logger.LogInformation(
      "PdfPig yielded insufficient text ({TextPerPage} chars/page) for report with key {ObjectKey}, falling back to Textract",
      textPerPage,
      report.FileStorageKey);

    memoryStream.Position = 0;
    var textractResult = await textractProvider.ExtractTextAsync(
      memoryStream, report.FileStorageKey!, cancellationToken);

    // If Textract also yields little text but more than PdfPig, use Textract
    if (textractResult.ExtractedText.Length > pdfPigResult.ExtractedText.Length)
    {
      return textractResult with { Method = "pdfpig+textract" };
    }

    // Otherwise return PdfPig result (even if sparse)
    return pdfPigResult;
  }

  private static void RemoveExtractedParameters(Report report)
  {
    var extractedParams = report.Parameters
      .Where(p => p.Source == "extracted")
      .ToList();

    foreach (var param in extractedParams)
    {
      report.Parameters.Remove(param);
    }
  }

  private static int AddExtractedParameters(Report report, StructuredExtractionResult result)
  {
    var existingCodes = report.Parameters
      .Where(p => p.Source != "extracted")
      .Select(p => p.ParameterCode)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var added = 0;
    foreach (var param in result.Parameters)
    {
      if (existingCodes.Contains(param.Code))
      {
        continue;
      }

      report.Parameters.Add(new ReportParameter
      {
        ReportId = report.Id,
        ParameterCode = param.Code,
        ParameterName = param.Name,
        MeasuredValueNumeric = param.NumericValue,
        MeasuredValueText = param.TextValue,
        Unit = param.Unit,
        ReferenceRangeText = param.ReferenceRange,
        IsAbnormal = param.IsAbnormal,
        Source = "extracted",
        Confidence = param.Confidence
      });

      added++;
    }

    return added;
  }
}
