using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CloudFrontInvalidationServiceTests
{
  [Fact]
  public async Task InvalidateObjectAsync_ShouldSubmitInvalidation_WhenEnabledAsync()
  {
    CreateInvalidationRequest? submittedRequest = null;
    var cloudFrontClient = new Mock<IAmazonCloudFront>();
    cloudFrontClient
      .Setup(x => x.CreateInvalidationAsync(It.IsAny<CreateInvalidationRequest>(), It.IsAny<CancellationToken>()))
      .Callback<CreateInvalidationRequest, CancellationToken>((request, _) => submittedRequest = request)
      .ReturnsAsync(new CreateInvalidationResponse());

    var service = new CloudFrontInvalidationService(
      cloudFrontClient.Object,
      Options.Create(new AwsOptions
      {
        UseLocalStack = false,
        S3 = new S3Options
        {
          CloudFront = new CloudFrontOptions
          {
            Enabled = true,
            DistributionId = "E123ABC456XYZ",
            EnableInvalidationOnDelete = true
          }
        }
      }),
      NullLogger<CloudFrontInvalidationService>.Instance);

    await service.InvalidateObjectAsync("reports/seed-PATIENT-1/report.pdf", CancellationToken.None);

    submittedRequest.Should().NotBeNull();
    submittedRequest!.DistributionId.Should().Be("E123ABC456XYZ");
    submittedRequest.InvalidationBatch.Paths.Items.Should().ContainSingle("/reports/seed-PATIENT-1/report.pdf");
    cloudFrontClient.Verify(
      x => x.CreateInvalidationAsync(It.IsAny<CreateInvalidationRequest>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task InvalidateObjectAsync_ShouldNoOp_WhenCloudFrontDisabledAsync()
  {
    var cloudFrontClient = new Mock<IAmazonCloudFront>();

    var service = new CloudFrontInvalidationService(
      cloudFrontClient.Object,
      Options.Create(new AwsOptions
      {
        S3 = new S3Options
        {
          CloudFront = new CloudFrontOptions
          {
            Enabled = false
          }
        }
      }),
      NullLogger<CloudFrontInvalidationService>.Instance);

    await service.InvalidateObjectAsync("reports/seed-PATIENT-1/report.pdf", CancellationToken.None);

    cloudFrontClient.Verify(
      x => x.CreateInvalidationAsync(It.IsAny<CreateInvalidationRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }
}
