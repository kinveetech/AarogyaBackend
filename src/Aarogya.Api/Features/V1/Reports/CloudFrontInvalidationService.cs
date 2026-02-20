using Aarogya.Api.Configuration;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class CloudFrontInvalidationService(
  IAmazonCloudFront cloudFrontClient,
  IOptions<AwsOptions> awsOptions,
  ILogger<CloudFrontInvalidationService> logger)
  : ICloudFrontInvalidationService
{
  public async Task InvalidateObjectAsync(string objectKey, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(objectKey))
    {
      return;
    }

    var cloudFront = awsOptions.Value.S3.CloudFront;
    if (awsOptions.Value.UseLocalStack
      || !cloudFront.Enabled
      || !cloudFront.EnableInvalidationOnDelete
      || string.IsNullOrWhiteSpace(cloudFront.DistributionId))
    {
      return;
    }

    await cloudFrontClient.CreateInvalidationAsync(new CreateInvalidationRequest
    {
      DistributionId = cloudFront.DistributionId,
      InvalidationBatch = new InvalidationBatch
      {
        CallerReference = $"report-delete-{Guid.NewGuid():N}",
        Paths = new Paths
        {
          Quantity = 1,
          Items = [$"/{objectKey.TrimStart('/')}"]
        }
      }
    }, cancellationToken);

    logger.LogInformation(
      "Submitted CloudFront invalidation for distribution={DistributionId} path=/{ObjectKey}",
      cloudFront.DistributionId,
      objectKey.TrimStart('/'));
  }
}
