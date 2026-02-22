using System.Text;
using Amazon.Textract;
using Amazon.Textract.Model;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class TextractTextExtractionProvider(
  IAmazonTextract textractClient,
  ILogger<TextractTextExtractionProvider> logger) : ITextExtractionProvider
{
  private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);
  private static readonly TimeSpan MaxPollingDuration = TimeSpan.FromMinutes(5);

  public async Task<TextExtractionResult> ExtractTextAsync(
    Stream pdfStream,
    string objectKey,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(pdfStream);

    logger.LogInformation(
      "Starting Textract text extraction for object {ObjectKey}",
      objectKey);

    using var memoryStream = new MemoryStream();
    await pdfStream.CopyToAsync(memoryStream, cancellationToken);
    memoryStream.Position = 0;

    var documentBytes = memoryStream.ToArray();

    // Use the synchronous Textract API for small documents, async API for larger ones.
    if (documentBytes.Length < 5 * 1024 * 1024)
    {
      return await ExtractWithSyncApiAsync(documentBytes, objectKey, cancellationToken);
    }

    return await ExtractWithAsyncApiAsync(objectKey, cancellationToken);
  }

  private async Task<TextExtractionResult> ExtractWithSyncApiAsync(
    byte[] documentBytes,
    string objectKey,
    CancellationToken cancellationToken)
  {
    var request = new DetectDocumentTextRequest
    {
      Document = new Document
      {
        Bytes = new MemoryStream(documentBytes)
      }
    };

    var response = await textractClient.DetectDocumentTextAsync(request, cancellationToken);

    var (text, pageCount) = AssembleTextFromBlocks(response.Blocks);

    var metadata = new Dictionary<string, string>
    {
      ["textract_api"] = "sync",
      ["block_count"] = response.Blocks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    logger.LogInformation(
      "Textract sync extracted {CharCount} characters from {PageCount} pages for object {ObjectKey}",
      text.Length,
      pageCount,
      objectKey);

    return new TextExtractionResult(text, "textract", pageCount, UsedOcr: true, metadata);
  }

  private async Task<TextExtractionResult> ExtractWithAsyncApiAsync(
    string objectKey,
    CancellationToken cancellationToken)
  {
    var startRequest = new StartDocumentTextDetectionRequest
    {
      DocumentLocation = new DocumentLocation
      {
        S3Object = new S3Object
        {
          Bucket = ExtractBucketFromKey(objectKey),
          Name = ExtractNameFromKey(objectKey)
        }
      }
    };

    var startResponse = await textractClient.StartDocumentTextDetectionAsync(startRequest, cancellationToken);
    var jobId = startResponse.JobId;

    logger.LogInformation(
      "Started Textract async job {JobId} for object {ObjectKey}",
      jobId,
      objectKey);

    var allBlocks = new List<Block>();
    var startTime = DateTimeOffset.UtcNow;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (DateTimeOffset.UtcNow - startTime > MaxPollingDuration)
      {
        throw new TimeoutException($"Textract job {jobId} did not complete within {MaxPollingDuration.TotalMinutes} minutes.");
      }

      await Task.Delay(PollingInterval, cancellationToken);

      var getRequest = new GetDocumentTextDetectionRequest { JobId = jobId };
      var getResponse = await textractClient.GetDocumentTextDetectionAsync(getRequest, cancellationToken);

      if (getResponse.JobStatus == JobStatus.FAILED)
      {
        throw new InvalidOperationException($"Textract job {jobId} failed: {getResponse.StatusMessage}");
      }

      if (getResponse.JobStatus == JobStatus.SUCCEEDED)
      {
        allBlocks.AddRange(getResponse.Blocks);

        // Handle pagination
        var nextToken = getResponse.NextToken;
        while (!string.IsNullOrEmpty(nextToken))
        {
          cancellationToken.ThrowIfCancellationRequested();

          var pageRequest = new GetDocumentTextDetectionRequest
          {
            JobId = jobId,
            NextToken = nextToken
          };

          var pageResponse = await textractClient.GetDocumentTextDetectionAsync(pageRequest, cancellationToken);
          allBlocks.AddRange(pageResponse.Blocks);
          nextToken = pageResponse.NextToken;
        }

        break;
      }
    }

    var (text, pageCount) = AssembleTextFromBlocks(allBlocks);

    var metadata = new Dictionary<string, string>
    {
      ["textract_api"] = "async",
      ["job_id"] = jobId,
      ["block_count"] = allBlocks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    logger.LogInformation(
      "Textract async extracted {CharCount} characters from {PageCount} pages for object {ObjectKey}",
      text.Length,
      pageCount,
      objectKey);

    return new TextExtractionResult(text, "textract", pageCount, UsedOcr: true, metadata);
  }

  private static (string Text, int PageCount) AssembleTextFromBlocks(List<Block> blocks)
  {
    var textBuilder = new StringBuilder();
    var pageCount = 0;

    foreach (var block in blocks.OrderBy(b => b.Page).ThenBy(b => b.Geometry?.BoundingBox?.Top ?? 0))
    {
      if (block.BlockType == BlockType.PAGE)
      {
        pageCount++;
      }
      else if (block.BlockType == BlockType.LINE && !string.IsNullOrWhiteSpace(block.Text))
      {
        textBuilder.AppendLine(block.Text);
      }
    }

    return (textBuilder.ToString().Trim(), pageCount);
  }

  private static string ExtractBucketFromKey(string objectKey)
  {
    // objectKey format: "bucket/path/to/file.pdf" or just "path/to/file.pdf"
    var parts = objectKey.Split('/', 2);
    return parts.Length > 1 ? parts[0] : string.Empty;
  }

  private static string ExtractNameFromKey(string objectKey)
  {
    var parts = objectKey.Split('/', 2);
    return parts.Length > 1 ? parts[1] : objectKey;
  }
}
