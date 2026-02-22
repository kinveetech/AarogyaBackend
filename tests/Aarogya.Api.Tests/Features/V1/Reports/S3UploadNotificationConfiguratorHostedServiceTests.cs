using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class S3UploadNotificationConfiguratorHostedServiceTests
{
  [Fact]
  public async Task StartAsync_ShouldSkip_WhenConfigurationDisabledAsync()
  {
    var s3Client = new Mock<IAmazonS3>();
    var sqsClient = new Mock<IAmazonSQS>();

    var awsOptions = Options.Create(new AwsOptions
    {
      Sqs = new SqsOptions { ConfigureS3NotificationsOnStartup = false }
    });
    var virusScanningOptions = Options.Create(new VirusScanningOptions());

    var sut = new S3UploadNotificationConfiguratorHostedService(
      s3Client.Object,
      sqsClient.Object,
      awsOptions,
      virusScanningOptions,
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    await sut.StartAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    s3Client.Verify(
      x => x.PutBucketNotificationAsync(It.IsAny<PutBucketNotificationRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task StartAsync_ShouldSkip_WhenBucketNameNotConfiguredAsync()
  {
    var s3Client = new Mock<IAmazonS3>();
    var sqsClient = new Mock<IAmazonSQS>();

    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options { BucketName = "SET_VIA_ENV" },
      Sqs = new SqsOptions
      {
        ConfigureS3NotificationsOnStartup = true,
        QueueName = "test-queue"
      }
    });
    var virusScanningOptions = Options.Create(new VirusScanningOptions());

    var sut = new S3UploadNotificationConfiguratorHostedService(
      s3Client.Object,
      sqsClient.Object,
      awsOptions,
      virusScanningOptions,
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    await sut.StartAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task StartAsync_ShouldSkip_WhenQueueNameNotConfiguredAsync()
  {
    var s3Client = new Mock<IAmazonS3>();
    var sqsClient = new Mock<IAmazonSQS>();

    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options { BucketName = "aarogya-reports" },
      Sqs = new SqsOptions
      {
        ConfigureS3NotificationsOnStartup = true,
        QueueName = "SET_VIA_ENV"
      }
    });
    var virusScanningOptions = Options.Create(new VirusScanningOptions());

    var sut = new S3UploadNotificationConfiguratorHostedService(
      s3Client.Object,
      sqsClient.Object,
      awsOptions,
      virusScanningOptions,
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    await sut.StartAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task StartAsync_ShouldCreateQueueAndConfigureNotifications_WhenQueueDoesNotExistAsync()
  {
    var queueUrl = "http://localhost:4566/000000000000/aarogya-upload-events";
    var queueArn = "arn:aws:sqs:ap-south-1:000000000000:aarogya-upload-events";

    var sqsClient = new Mock<IAmazonSQS>();
    sqsClient
      .Setup(x => x.GetQueueUrlAsync("aarogya-upload-events", It.IsAny<CancellationToken>()))
      .ThrowsAsync(new QueueDoesNotExistException("not found"));
    sqsClient
      .Setup(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new CreateQueueResponse { QueueUrl = queueUrl });
    sqsClient
      .Setup(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetQueueAttributesResponse
      {
        Attributes = new Dictionary<string, string> { ["QueueArn"] = queueArn }
      });
    sqsClient
      .Setup(x => x.SetQueueAttributesAsync(It.IsAny<SetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SetQueueAttributesResponse());

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetBucketNotificationAsync("aarogya-reports", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetBucketNotificationResponse
      {
        QueueConfigurations = [],
        TopicConfigurations = [],
        LambdaFunctionConfigurations = []
      });
    s3Client
      .Setup(x => x.PutBucketNotificationAsync(
        It.IsAny<PutBucketNotificationRequest>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutBucketNotificationResponse());
    s3Client
      .Setup(x => x.GetBucketAclAsync(It.IsAny<GetBucketAclRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetBucketAclResponse());

    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options { BucketName = "aarogya-reports" },
      Sqs = new SqsOptions
      {
        ConfigureS3NotificationsOnStartup = true,
        QueueName = "aarogya-upload-events"
      }
    });
    var virusScanningOptions = Options.Create(new VirusScanningOptions
    {
      QuarantineBucketName = "aarogya-quarantine"
    });

    var sut = new S3UploadNotificationConfiguratorHostedService(
      s3Client.Object,
      sqsClient.Object,
      awsOptions,
      virusScanningOptions,
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    await sut.StartAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.CreateQueueAsync(
        It.Is<CreateQueueRequest>(r => r.QueueName == "aarogya-upload-events"),
        It.IsAny<CancellationToken>()),
      Times.Once);

    sqsClient.Verify(
      x => x.SetQueueAttributesAsync(It.IsAny<SetQueueAttributesRequest>(), It.IsAny<CancellationToken>()),
      Times.Once);

    s3Client.Verify(
      x => x.PutBucketNotificationAsync(
        It.Is<PutBucketNotificationRequest>(r => r.BucketName == "aarogya-reports"),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task StartAsync_ShouldUseExistingQueue_WhenQueueAlreadyExistsAsync()
  {
    var queueUrl = "http://localhost:4566/000000000000/aarogya-upload-events";
    var queueArn = "arn:aws:sqs:ap-south-1:000000000000:aarogya-upload-events";

    var sqsClient = new Mock<IAmazonSQS>();
    sqsClient
      .Setup(x => x.GetQueueUrlAsync("aarogya-upload-events", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl });
    sqsClient
      .Setup(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetQueueAttributesResponse
      {
        Attributes = new Dictionary<string, string> { ["QueueArn"] = queueArn }
      });
    sqsClient
      .Setup(x => x.SetQueueAttributesAsync(It.IsAny<SetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SetQueueAttributesResponse());

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetBucketNotificationAsync("aarogya-reports", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetBucketNotificationResponse
      {
        QueueConfigurations = [],
        TopicConfigurations = [],
        LambdaFunctionConfigurations = []
      });
    s3Client
      .Setup(x => x.PutBucketNotificationAsync(
        It.IsAny<PutBucketNotificationRequest>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutBucketNotificationResponse());
    s3Client
      .Setup(x => x.GetBucketAclAsync(It.IsAny<GetBucketAclRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetBucketAclResponse());

    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options { BucketName = "aarogya-reports" },
      Sqs = new SqsOptions
      {
        ConfigureS3NotificationsOnStartup = true,
        QueueName = "aarogya-upload-events"
      }
    });
    var virusScanningOptions = Options.Create(new VirusScanningOptions
    {
      QuarantineBucketName = "aarogya-quarantine"
    });

    var sut = new S3UploadNotificationConfiguratorHostedService(
      s3Client.Object,
      sqsClient.Object,
      awsOptions,
      virusScanningOptions,
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    await sut.StartAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task StopAsync_ShouldCompleteImmediatelyAsync()
  {
    var sut = new S3UploadNotificationConfiguratorHostedService(
      Mock.Of<IAmazonS3>(),
      Mock.Of<IAmazonSQS>(),
      Options.Create(new AwsOptions()),
      Options.Create(new VirusScanningOptions()),
      NullLogger<S3UploadNotificationConfiguratorHostedService>.Instance);

    var act = async () => await sut.StopAsync(CancellationToken.None);
    await act.Should().NotThrowAsync();
  }
}
