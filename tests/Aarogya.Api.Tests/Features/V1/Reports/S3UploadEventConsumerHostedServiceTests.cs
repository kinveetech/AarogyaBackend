using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class S3UploadEventConsumerHostedServiceTests
{
  [Fact]
  public async Task ExecuteAsync_ShouldReturnImmediately_WhenConsumerDisabledAsync()
  {
    var sqsClient = new Mock<IAmazonSQS>();
    var s3Client = new Mock<IAmazonS3>();
    var scopeFactory = new Mock<IServiceScopeFactory>();

    var options = Options.Create(new AwsOptions
    {
      Sqs = new SqsOptions { EnableUploadEventConsumer = false }
    });

    var sut = new S3UploadEventConsumerHostedService(
      scopeFactory.Object,
      sqsClient.Object,
      s3Client.Object,
      options,
      NullLogger<S3UploadEventConsumerHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldReturnImmediately_WhenQueueNameNotConfiguredAsync()
  {
    var sqsClient = new Mock<IAmazonSQS>();
    var s3Client = new Mock<IAmazonS3>();
    var scopeFactory = new Mock<IServiceScopeFactory>();

    var options = Options.Create(new AwsOptions
    {
      Sqs = new SqsOptions
      {
        EnableUploadEventConsumer = true,
        QueueName = "SET_VIA_ENV"
      }
    });

    var sut = new S3UploadEventConsumerHostedService(
      scopeFactory.Object,
      sqsClient.Object,
      s3Client.Object,
      options,
      NullLogger<S3UploadEventConsumerHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    sqsClient.Verify(
      x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldPollAndProcessMessages_WhenEnabledAsync()
  {
    var queueUrl = "http://localhost:4566/000000000000/test-queue";
    var messageBody = """
      {
        "Records": [
          {
            "eventName": "s3:ObjectCreated:Put",
            "eventTime": "2026-02-21T10:00:00Z",
            "s3": {
              "bucket": { "name": "test-bucket" },
              "object": { "key": "reports/user1/file.pdf", "size": 1024 }
            }
          }
        ]
      }
      """;

    var sqsClient = new Mock<IAmazonSQS>();
    sqsClient
      .Setup(x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl });

    var receiveCallCount = 0;
    sqsClient
      .Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(() =>
      {
        receiveCallCount++;
        if (receiveCallCount == 1)
        {
          return new ReceiveMessageResponse
          {
            Messages = [new Message { MessageId = "msg-1", Body = messageBody, ReceiptHandle = "rh-1" }]
          };
        }

        return new ReceiveMessageResponse { Messages = [] };
      });

    sqsClient
      .Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DeleteMessageResponse());

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetObjectMetadataResponse());

    var virusScanProcessor = new Mock<IReportVirusScanProcessor>();
    virusScanProcessor
      .Setup(x => x.ProcessUploadAsync(It.IsAny<S3UploadEventRecord>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var services = new ServiceCollection();
    services.AddScoped(_ => virusScanProcessor.Object);
    var provider = services.BuildServiceProvider();
    var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

    var options = Options.Create(new AwsOptions
    {
      Sqs = new SqsOptions
      {
        EnableUploadEventConsumer = true,
        QueueName = "test-queue",
        MaxNumberOfMessages = 5,
        ReceiveWaitTimeSeconds = 1,
        VisibilityTimeoutSeconds = 30,
        EmptyPollDelayMilliseconds = 10
      }
    });

    var sut = new S3UploadEventConsumerHostedService(
      scopeFactory,
      sqsClient.Object,
      s3Client.Object,
      options,
      NullLogger<S3UploadEventConsumerHostedService>.Instance);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    await sut.StartAsync(cts.Token);
    await Task.Delay(500);
    await cts.CancelAsync();

    var act = async () => await sut.StopAsync(CancellationToken.None);
    await act.Should().NotThrowAsync();

    virusScanProcessor.Verify(
      x => x.ProcessUploadAsync(It.IsAny<S3UploadEventRecord>(), It.IsAny<CancellationToken>()),
      Times.AtLeastOnce);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldStopGracefully_WhenImmediatelyCancelledAsync()
  {
    var sqsClient = new Mock<IAmazonSQS>();
    var s3Client = new Mock<IAmazonS3>();
    var scopeFactory = new Mock<IServiceScopeFactory>();

    var options = Options.Create(new AwsOptions
    {
      Sqs = new SqsOptions
      {
        EnableUploadEventConsumer = true,
        QueueName = "test-queue"
      }
    });

    var sut = new S3UploadEventConsumerHostedService(
      scopeFactory.Object,
      sqsClient.Object,
      s3Client.Object,
      options,
      NullLogger<S3UploadEventConsumerHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    await sut.StartAsync(cts.Token);

    var act = async () => await sut.StopAsync(CancellationToken.None);
    await act.Should().NotThrowAsync();
  }
}
