using System.Text;
using Aarogya.Api.Features.V1.Reports;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ClamAvReportVirusScannerTests
{
  [Fact]
  public async Task ScanObjectAsync_ShouldMarkClean_WhenNoSignatureDetectedAsync()
  {
    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetObjectResponse
      {
        ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("normal file content"))
      });

    var scanner = new ClamAvReportVirusScanner(s3Client.Object, NullLogger<ClamAvReportVirusScanner>.Instance);

    var result = await scanner.ScanObjectAsync("bucket", "reports/file.pdf", CancellationToken.None);

    result.IsInfected.Should().BeFalse();
    result.Engine.Should().Be("clamav-mock");
  }

  [Fact]
  public async Task ScanObjectAsync_ShouldMarkInfected_WhenEicarSignatureDetectedAsync()
  {
    const string eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetObjectResponse
      {
        ResponseStream = new MemoryStream(Encoding.ASCII.GetBytes(eicar))
      });

    var scanner = new ClamAvReportVirusScanner(s3Client.Object, NullLogger<ClamAvReportVirusScanner>.Instance);

    var result = await scanner.ScanObjectAsync("bucket", "reports/file.pdf", CancellationToken.None);

    result.IsInfected.Should().BeTrue();
    result.Signature.Should().Be("EICAR-Test-Signature");
  }
}
