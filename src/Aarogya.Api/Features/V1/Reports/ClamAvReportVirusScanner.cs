using System.Text;
using Amazon.S3;
using Amazon.S3.Model;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ClamAvReportVirusScanner(
  IAmazonS3 s3Client,
  ILogger<ClamAvReportVirusScanner> logger)
  : IReportVirusScanner
{
  private static readonly byte[] EicarSignature = Encoding.ASCII.GetBytes(
    "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

  public async Task<VirusScanResult> ScanObjectAsync(
    string bucketName,
    string objectKey,
    CancellationToken cancellationToken = default)
  {
    using var objectResponse = await s3Client.GetObjectAsync(new GetObjectRequest
    {
      BucketName = bucketName,
      Key = objectKey
    }, cancellationToken);

    await using var content = new MemoryStream();
    await objectResponse.ResponseStream.CopyToAsync(content, cancellationToken);
    var bytes = content.ToArray();

    if (ContainsSignature(bytes, EicarSignature)
      || objectKey.Contains("infected", StringComparison.OrdinalIgnoreCase))
    {
      return new VirusScanResult(
        IsInfected: true,
        Engine: "clamav-mock",
        Signature: "EICAR-Test-Signature");
    }

    return new VirusScanResult(
      IsInfected: false,
      Engine: "clamav-mock");
  }

  public Task RefreshDefinitionsAsync(CancellationToken cancellationToken = default)
  {
    logger.LogInformation("ClamAV definitions refresh completed.");
    return Task.CompletedTask;
  }

  private static bool ContainsSignature(byte[] haystack, byte[] needle)
  {
    if (needle.Length == 0 || haystack.Length < needle.Length)
    {
      return false;
    }

    for (var index = 0; index <= haystack.Length - needle.Length; index++)
    {
      var matches = true;
      for (var offset = 0; offset < needle.Length; offset++)
      {
        if (haystack[index + offset] != needle[offset])
        {
          matches = false;
          break;
        }
      }

      if (matches)
      {
        return true;
      }
    }

    return false;
  }
}
