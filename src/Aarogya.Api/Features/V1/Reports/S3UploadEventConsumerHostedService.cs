using Aarogya.Api.Configuration;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class S3UploadEventConsumerHostedService(
  IAmazonSQS sqsClient,
  IAmazonS3 s3Client,
  IOptions<AwsOptions> awsOptions,
  ILogger<S3UploadEventConsumerHostedService> logger)
  : BackgroundService
{
  private string? _queueUrl;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var sqsOptions = awsOptions.Value.Sqs;
    if (!sqsOptions.EnableUploadEventConsumer)
    {
      logger.LogInformation("S3 upload event consumer is disabled.");
      return;
    }
    if (string.IsNullOrWhiteSpace(sqsOptions.QueueName)
      || sqsOptions.QueueName.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase))
    {
      logger.LogWarning("S3 upload event consumer is enabled but queue name is not configured.");
      return;
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _queueUrl ??= await ResolveQueueUrlAsync(sqsOptions.QueueName, stoppingToken);
        await PollQueueAsync(_queueUrl, sqsOptions, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Unexpected failure while consuming S3 upload events.");
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
      }
    }
  }

  private async Task<string> ResolveQueueUrlAsync(string queueName, CancellationToken cancellationToken)
  {
    var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
    return response.QueueUrl;
  }

  private async Task PollQueueAsync(
    string queueUrl,
    SqsOptions sqsOptions,
    CancellationToken cancellationToken)
  {
    var receive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
    {
      QueueUrl = queueUrl,
      MaxNumberOfMessages = sqsOptions.MaxNumberOfMessages,
      WaitTimeSeconds = sqsOptions.ReceiveWaitTimeSeconds,
      VisibilityTimeout = sqsOptions.VisibilityTimeoutSeconds
    }, cancellationToken);

    if (receive.Messages.Count == 0)
    {
      await Task.Delay(sqsOptions.EmptyPollDelayMilliseconds, cancellationToken);
      return;
    }

    foreach (var message in receive.Messages)
    {
      try
      {
        await ProcessMessageAsync(message, cancellationToken);

        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
          QueueUrl = queueUrl,
          ReceiptHandle = message.ReceiptHandle
        }, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Failed to process S3 upload event message {MessageId}.", message.MessageId);
      }
    }
  }

  private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
  {
    var records = S3UploadEventParser.ParseRecords(message.Body);
    if (records.Count == 0)
    {
      logger.LogDebug("SQS message {MessageId} did not contain S3 records.", message.MessageId);
      return;
    }

    foreach (var record in records)
    {
      var contentType = await TryResolveContentTypeAsync(record.BucketName, record.ObjectKey, cancellationToken);

      logger.LogInformation(
        "Processed S3 upload event: bucket={BucketName}, key={ObjectKey}, size={SizeBytes}, contentType={ContentType}, event={EventName}, time={EventTime}",
        record.BucketName,
        record.ObjectKey,
        record.SizeBytes,
        contentType ?? "unknown",
        record.EventName,
        record.EventTime);
    }
  }

  private async Task<string?> TryResolveContentTypeAsync(
    string bucketName,
    string objectKey,
    CancellationToken cancellationToken)
  {
    try
    {
      var headResponse = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
      {
        BucketName = bucketName,
        Key = objectKey
      }, cancellationToken);

      return string.IsNullOrWhiteSpace(headResponse.Headers.ContentType)
        ? null
        : headResponse.Headers.ContentType;
    }
    catch (AmazonS3Exception ex)
    {
      logger.LogWarning(
        ex,
        "Unable to resolve content type for uploaded object {BucketName}/{ObjectKey}.",
        bucketName,
        objectKey);
      return null;
    }
  }
}
