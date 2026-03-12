using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class ReportStatusMapperTests
{
  [Theory]
  [InlineData(ReportStatus.Draft, "draft")]
  [InlineData(ReportStatus.Uploaded, "uploaded")]
  [InlineData(ReportStatus.Processing, "processing")]
  [InlineData(ReportStatus.Clean, "clean")]
  [InlineData(ReportStatus.Infected, "infected")]
  [InlineData(ReportStatus.Validated, "validated")]
  [InlineData(ReportStatus.Published, "published")]
  [InlineData(ReportStatus.Archived, "archived")]
  [InlineData(ReportStatus.Extracting, "extracting")]
  [InlineData(ReportStatus.Extracted, "extracted")]
  [InlineData(ReportStatus.ExtractionFailed, "extraction_failed")]
  public void ToStatusString_ShouldReturnSnakeCaseString(ReportStatus status, string expected)
  {
    ReportStatusMapper.ToStatusString(status).Should().Be(expected);
  }
}
