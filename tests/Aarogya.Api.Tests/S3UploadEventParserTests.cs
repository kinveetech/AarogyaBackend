using Aarogya.Api.Features.V1.Reports;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class S3UploadEventParserTests
{
  [Fact]
  public void ParseRecords_ShouldExtractUploadEventFields()
  {
    const string messageBody = """
      {
        "Records": [
          {
            "eventName": "ObjectCreated:Put",
            "eventTime": "2026-02-20T12:00:00.000Z",
            "s3": {
              "bucket": {
                "name": "aarogya-dev"
              },
              "object": {
                "key": "reports/user-123/file%20name.pdf",
                "size": 3145728
              }
            }
          }
        ]
      }
      """;

    var records = S3UploadEventParser.ParseRecords(messageBody);

    records.Should().ContainSingle();
    records[0].BucketName.Should().Be("aarogya-dev");
    records[0].ObjectKey.Should().Be("reports/user-123/file name.pdf");
    records[0].SizeBytes.Should().Be(3145728);
    records[0].EventName.Should().Be("ObjectCreated:Put");
    records[0].EventTime.Should().Be(DateTimeOffset.Parse("2026-02-20T12:00:00.000Z"));
  }

  [Fact]
  public void ParseRecords_ShouldReturnEmpty_WhenRecordsMissing()
  {
    const string messageBody = """
      {
        "Type": "Notification",
        "Message": "No S3 records"
      }
      """;

    var records = S3UploadEventParser.ParseRecords(messageBody);

    records.Should().BeEmpty();
  }
}
