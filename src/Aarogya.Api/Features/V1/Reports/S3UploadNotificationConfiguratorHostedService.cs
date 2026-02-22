using System.Text.Json;
using Aarogya.Api.Configuration;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class S3UploadNotificationConfiguratorHostedService(
  IAmazonS3 s3Client,
  IAmazonSQS sqsClient,
  IOptions<AwsOptions> awsOptions,
  IOptions<VirusScanningOptions> virusScanningOptions,
  ILogger<S3UploadNotificationConfiguratorHostedService> logger)
  : IHostedService
{
  private const string UploadNotificationId = "aarogya-report-upload-events";

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var options = awsOptions.Value;
    if (!options.Sqs.ConfigureS3NotificationsOnStartup)
    {
      logger.LogInformation("S3 upload notification configuration is disabled.");
      return;
    }

    if (string.IsNullOrWhiteSpace(options.S3.BucketName)
      || string.IsNullOrWhiteSpace(options.Sqs.QueueName)
      || options.S3.BucketName.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase)
      || options.Sqs.QueueName.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase))
    {
      logger.LogWarning("Skipping S3->SQS notification setup because bucket or queue is not configured.");
      return;
    }

    await EnsureBucketAsync(options.S3.BucketName, cancellationToken);

    var queueUrl = await GetOrCreateQueueUrlAsync(options.Sqs.QueueName, cancellationToken);
    var queueArn = await GetQueueArnAsync(queueUrl, cancellationToken);
    await EnsureQuarantineBucketAsync(virusScanningOptions.Value, cancellationToken);

    await EnsureQueuePolicyAsync(queueUrl, queueArn, options.S3.BucketName, cancellationToken);
    await EnsureBucketNotificationAsync(queueArn, options.S3.BucketName, cancellationToken);

    logger.LogInformation(
      "Configured S3 upload notifications for bucket '{BucketName}' to queue '{QueueName}'.",
      options.S3.BucketName,
      options.Sqs.QueueName);
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private async Task<string> GetOrCreateQueueUrlAsync(string queueName, CancellationToken cancellationToken)
  {
    try
    {
      var existing = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
      return existing.QueueUrl;
    }
    catch (QueueDoesNotExistException)
    {
      var created = await sqsClient.CreateQueueAsync(new CreateQueueRequest
      {
        QueueName = queueName
      }, cancellationToken);

      return created.QueueUrl;
    }
  }

  private async Task<string> GetQueueArnAsync(string queueUrl, CancellationToken cancellationToken)
  {
    var attributes = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
    {
      QueueUrl = queueUrl,
      AttributeNames = ["QueueArn"]
    }, cancellationToken);

    if (!attributes.Attributes.TryGetValue("QueueArn", out var queueArn)
      || string.IsNullOrWhiteSpace(queueArn))
    {
      throw new InvalidOperationException("SQS queue ARN could not be resolved.");
    }

    return queueArn;
  }

  private async Task EnsureQueuePolicyAsync(
    string queueUrl,
    string queueArn,
    string bucketName,
    CancellationToken cancellationToken)
  {
    var bucketArn = $"arn:aws:s3:::{bucketName}";
    var policy = JsonSerializer.Serialize(new
    {
      Version = "2012-10-17",
      Statement = new[]
      {
        new
        {
          Sid = "AllowS3UploadEvents",
          Effect = "Allow",
          Principal = new { Service = "s3.amazonaws.com" },
          Action = "sqs:SendMessage",
          Resource = queueArn,
          Condition = new
          {
            ArnEquals = new
            {
              SourceArn = bucketArn
            }
          }
        }
      }
    });

    await sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
    {
      QueueUrl = queueUrl,
      Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
      {
        ["Policy"] = policy
      }
    }, cancellationToken);
  }

  private async Task EnsureBucketNotificationAsync(
    string queueArn,
    string bucketName,
    CancellationToken cancellationToken)
  {
    var current = await s3Client.GetBucketNotificationAsync(bucketName, cancellationToken);

    var queueConfigurations = (current.QueueConfigurations ?? [])
      .Where(config => !string.Equals(config.Id, UploadNotificationId, StringComparison.Ordinal))
      .ToList();

    queueConfigurations.Add(new QueueConfiguration
    {
      Id = UploadNotificationId,
      Queue = queueArn,
      Events = [EventType.ObjectCreatedPut, EventType.ObjectCreatedPost, EventType.ObjectCreatedCompleteMultipartUpload]
    });

    var updated = new PutBucketNotificationRequest
    {
      BucketName = bucketName,
      QueueConfigurations = queueConfigurations,
      TopicConfigurations = current.TopicConfigurations,
      LambdaFunctionConfigurations = current.LambdaFunctionConfigurations,
      EventBridgeConfiguration = current.EventBridgeConfiguration
    };

    await s3Client.PutBucketNotificationAsync(updated, cancellationToken);
  }

  private async Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken)
  {
    try
    {
      await s3Client.GetBucketAclAsync(new GetBucketAclRequest
      {
        BucketName = bucketName
      }, cancellationToken);
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      await s3Client.PutBucketAsync(bucketName, cancellationToken);
      logger.LogInformation("Created S3 bucket '{BucketName}'.", bucketName);
    }
  }

  private async Task EnsureQuarantineBucketAsync(
    VirusScanningOptions options,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(options.QuarantineBucketName))
    {
      return;
    }

    try
    {
      await s3Client.GetBucketAclAsync(new GetBucketAclRequest
      {
        BucketName = options.QuarantineBucketName
      }, cancellationToken);
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      await s3Client.PutBucketAsync(options.QuarantineBucketName, cancellationToken);
      logger.LogInformation(ex, "Created quarantine bucket '{BucketName}'.", options.QuarantineBucketName);
    }
  }
}
