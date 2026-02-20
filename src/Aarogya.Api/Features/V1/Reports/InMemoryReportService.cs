using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class InMemoryReportService(
  IAmazonS3 s3Client,
  IOptions<AwsOptions> awsOptions,
  IUtcClock clock)
  : IReportService
{
  private readonly ConcurrentDictionary<string, List<ReportSummaryResponse>> _reportsByUser = new(StringComparer.Ordinal);
  private readonly AwsOptions _awsOptions = awsOptions.Value;

  public Task<IReadOnlyList<ReportSummaryResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);

    lock (reports)
    {
      return Task.FromResult<IReadOnlyList<ReportSummaryResponse>>(reports.OrderByDescending(report => report.CreatedAt).ToArray());
    }
  }

  public Task<ReportSummaryResponse> AddForUserAsync(string userSub, CreateReportRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);
    var created = new ReportSummaryResponse(Guid.NewGuid(), request.Title.Trim(), "uploaded", clock.UtcNow);

    lock (reports)
    {
      reports.Add(created);
    }

    return Task.FromResult(created);
  }

  public async Task<ReportSignedUploadUrlResponse> GetSignedUploadUrlAsync(
    string userSub,
    CreateReportUploadUrlRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var now = clock.UtcNow;
    var expiresAt = now.AddMinutes(GetExpiryMinutes(request.ExpiryMinutes));
    var objectKey = BuildObjectKey(userSub, request.FileName, now);

    var presignRequest = new GetPreSignedUrlRequest
    {
      BucketName = _awsOptions.S3.BucketName,
      Key = objectKey,
      Verb = HttpVerb.PUT,
      Expires = expiresAt.UtcDateTime,
      ContentType = request.ContentType,
      Protocol = ResolveProtocol()
    };

    var url = await s3Client.GetPreSignedURLAsync(presignRequest);
    return new ReportSignedUploadUrlResponse(objectKey, new Uri(url, UriKind.Absolute), expiresAt);
  }

  public async Task<ReportSignedDownloadUrlResponse> GetSignedDownloadUrlAsync(
    string userSub,
    CreateReportDownloadUrlRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var expectedPrefix = $"reports/{userSub}/";
    if (!request.ObjectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("Object key does not belong to the authenticated user.");
    }

    var now = clock.UtcNow;
    var expiresAt = now.AddMinutes(GetExpiryMinutes(request.ExpiryMinutes));

    if (TryGetCloudFrontSignedUrl(request.ObjectKey, expiresAt, out var cloudFrontUrl))
    {
      return new ReportSignedDownloadUrlResponse(request.ObjectKey, new Uri(cloudFrontUrl, UriKind.Absolute), expiresAt, "cloudfront");
    }

    var presignRequest = new GetPreSignedUrlRequest
    {
      BucketName = _awsOptions.S3.BucketName,
      Key = request.ObjectKey,
      Verb = HttpVerb.GET,
      Expires = expiresAt.UtcDateTime,
      Protocol = ResolveProtocol()
    };

    var s3Url = await s3Client.GetPreSignedURLAsync(presignRequest);
    return new ReportSignedDownloadUrlResponse(request.ObjectKey, new Uri(s3Url, UriKind.Absolute), expiresAt, "s3");
  }

  private int GetExpiryMinutes(int? requestedMinutes)
  {
    var configuredDefault = _awsOptions.S3.PresignedUrlExpiryMinutes;
    var candidate = requestedMinutes ?? configuredDefault;
    return Math.Clamp(candidate, 1, 10080);
  }

  private static string BuildObjectKey(string userSub, string fileName, DateTimeOffset now)
  {
    var extension = Path.GetExtension(fileName)?.Trim() ?? string.Empty;
    var extensionPart = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
    var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    var timestamp = now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    return $"reports/{userSub}/{timestamp}-{random}{extensionPart}";
  }

  private Protocol ResolveProtocol()
  {
    if (_awsOptions.UseLocalStack)
    {
      return Protocol.HTTP;
    }

    return Protocol.HTTPS;
  }

  private bool TryGetCloudFrontSignedUrl(string objectKey, DateTimeOffset expiresAt, out string url)
  {
    var cloudFront = _awsOptions.S3.CloudFront;
    if (_awsOptions.UseLocalStack
      || !cloudFront.Enabled
      || string.IsNullOrWhiteSpace(cloudFront.DistributionDomain)
      || string.IsNullOrWhiteSpace(cloudFront.KeyPairId)
      || string.IsNullOrWhiteSpace(cloudFront.PrivateKeyPem))
    {
      url = string.Empty;
      return false;
    }

    var resourceUrl = $"https://{cloudFront.DistributionDomain.TrimEnd('/')}/{objectKey}";
    url = BuildCloudFrontSignedUrl(resourceUrl, cloudFront.KeyPairId, cloudFront.PrivateKeyPem, expiresAt);
    return true;
  }

  private static string BuildCloudFrontSignedUrl(
    string resourceUrl,
    string keyPairId,
    string privateKeyPemBase64,
    DateTimeOffset expiresAt)
  {
    var expiresAtEpoch = expiresAt.ToUnixTimeSeconds();
    var policy = $"{{\"Statement\":[{{\"Resource\":\"{resourceUrl}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{expiresAtEpoch}}}}}}}]}}";

    var privateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyPemBase64));
    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem);

    var policyBytes = Encoding.UTF8.GetBytes(policy);
    var signatureBytes = rsa.SignData(policyBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    var signature = ToCloudFrontSafeBase64(signatureBytes);

    var separator = resourceUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return $"{resourceUrl}{separator}Expires={expiresAtEpoch}&Signature={signature}&Key-Pair-Id={Uri.EscapeDataString(keyPairId)}";
  }

  private static string ToCloudFrontSafeBase64(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
      .Replace('+', '-')
      .Replace('=', '_')
      .Replace('/', '~');
  }
}
