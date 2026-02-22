using System.Text.Json;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Aarogya.Infrastructure.Tests.Converters;

public sealed class JsonbValueConverterTests
{
  private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Serialize_ShouldRoundTrip_AccessGrantScope()
  {
    var scope = new AccessGrantScope
    {
      CanReadReports = true,
      CanDownloadReports = false,
      AllowedReportIds = [Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")],
      AllowedReportTypes = ["blood_test", "radiology"]
    };

    var json = JsonSerializer.Serialize(scope, Options);
    var deserialized = JsonSerializer.Deserialize<AccessGrantScope>(json, Options);

    deserialized.Should().BeEquivalentTo(scope);
  }

  [Fact]
  public void Serialize_ShouldRoundTrip_ReportMetadata()
  {
    var metadata = new ReportMetadata
    {
      SourceSystem = "lab-system-v2",
      Tags = new Dictionary<string, string>
      {
        ["priority"] = "high",
        ["department"] = "hematology"
      }
    };

    var json = JsonSerializer.Serialize(metadata, Options);
    var deserialized = JsonSerializer.Deserialize<ReportMetadata>(json, Options);

    deserialized.Should().BeEquivalentTo(metadata);
  }

  [Fact]
  public void Serialize_ShouldRoundTrip_ReportResults()
  {
    var results = new ReportResults
    {
      ReportVersion = 2,
      Notes = "Within normal limits",
      Parameters =
      [
        new ReportResultParameter
        {
          Code = "HGB",
          Name = "Hemoglobin",
          Value = 14.5m,
          Unit = "g/dL",
          ReferenceRange = "12.0-17.5",
          AbnormalFlag = false
        }
      ]
    };

    var json = JsonSerializer.Serialize(results, Options);
    var deserialized = JsonSerializer.Deserialize<ReportResults>(json, Options);

    deserialized.Should().BeEquivalentTo(results);
  }

  [Fact]
  public void Deserialize_ShouldReturnDefaults_WhenEmptyJson()
  {
    var scope = JsonSerializer.Deserialize<AccessGrantScope>("{}", Options);

    scope.Should().NotBeNull();
    scope!.CanReadReports.Should().BeTrue();
    scope.CanDownloadReports.Should().BeTrue();
    scope.AllowedReportIds.Should().BeEmpty();
    scope.AllowedReportTypes.Should().BeEmpty();
  }

  [Fact]
  public void Deserialize_ShouldReturnDefaults_WhenEmptyJson_ReportMetadata()
  {
    var metadata = JsonSerializer.Deserialize<ReportMetadata>("{}", Options);

    metadata.Should().NotBeNull();
    metadata!.SourceSystem.Should().BeNull();
    metadata.Tags.Should().BeEmpty();
  }

  [Fact]
  public void Deserialize_ShouldReturnDefaults_WhenEmptyJson_ReportResults()
  {
    var results = JsonSerializer.Deserialize<ReportResults>("{}", Options);

    results.Should().NotBeNull();
    results!.ReportVersion.Should().Be(1);
    results.Notes.Should().BeNull();
    results.Parameters.Should().BeEmpty();
  }

  [Fact]
  public void Serialize_ShouldProduceEquivalentJson_ForIdenticalObjects()
  {
    var scope1 = new AccessGrantScope
    {
      CanReadReports = true,
      CanDownloadReports = false,
      AllowedReportIds = [],
      AllowedReportTypes = ["blood_test"]
    };

    var scope2 = new AccessGrantScope
    {
      CanReadReports = true,
      CanDownloadReports = false,
      AllowedReportIds = [],
      AllowedReportTypes = ["blood_test"]
    };

    var json1 = JsonSerializer.Serialize(scope1, Options);
    var json2 = JsonSerializer.Serialize(scope2, Options);

    json1.Should().Be(json2);
  }

  [Fact]
  public void Serialize_ShouldProduceDifferentJson_ForDifferentObjects()
  {
    var scope1 = new AccessGrantScope { CanReadReports = true };
    var scope2 = new AccessGrantScope { CanReadReports = false };

    var json1 = JsonSerializer.Serialize(scope1, Options);
    var json2 = JsonSerializer.Serialize(scope2, Options);

    json1.Should().NotBe(json2);
  }
}
