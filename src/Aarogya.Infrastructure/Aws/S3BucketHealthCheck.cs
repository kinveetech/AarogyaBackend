using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aarogya.Infrastructure.Aws;

internal sealed class S3BucketHealthCheck(
  IAmazonS3 s3Client,
  IConfiguration configuration) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    var bucketName = configuration["Aws:S3:BucketName"]?.Trim();
    if (string.IsNullOrWhiteSpace(bucketName) || IsPlaceholder(bucketName))
    {
      return HealthCheckResult.Unhealthy("S3 bucket name is not configured.");
    }

    try
    {
      var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
      {
        BucketName = bucketName,
        MaxKeys = 1
      }, cancellationToken);

      if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
      {
        return HealthCheckResult.Healthy($"S3 bucket '{bucketName}' is reachable.");
      }

      return HealthCheckResult.Unhealthy($"S3 bucket '{bucketName}' check returned HTTP {(int)response.HttpStatusCode}.");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return HealthCheckResult.Unhealthy("S3 health check timed out.");
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy("S3 health check failed.", ex);
    }
  }

  private static bool IsPlaceholder(string value)
  {
    return value.Contains("SET_VIA_ENV_VAR", StringComparison.OrdinalIgnoreCase)
      || value.Contains("SET_VIA_USER_SECRETS_OR_ENV_VAR", StringComparison.OrdinalIgnoreCase);
  }
}
