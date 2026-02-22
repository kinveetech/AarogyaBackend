using System.Text.Json;
using Aarogya.Api.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Aarogya.Api.Tests.Health;

public sealed class HealthCheckResponseWriterTests
{
  [Theory]
  [InlineData(HealthStatus.Healthy)]
  [InlineData(HealthStatus.Degraded)]
  [InlineData(HealthStatus.Unhealthy)]
  public async Task WriteResponse_Should_SerializeStatusCorrectly_Async(HealthStatus status)
  {
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();

    var entries = new Dictionary<string, HealthReportEntry>
    {
      ["db"] = new(status, "Database check", TimeSpan.FromMilliseconds(50), null, null)
    };
    var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));

    await HealthCheckResponseWriter.WriteResponse(context, report);

    context.Response.Body.Seek(0, SeekOrigin.Begin);
    using var doc = await JsonDocument.ParseAsync(context.Response.Body);
    var root = doc.RootElement;

    root.GetProperty("status").GetString().Should().Be(status.ToString());
    root.GetProperty("totalDurationMs").GetDouble().Should().BeApproximately(100, 1);

    var checks = root.GetProperty("checks");
    var dbCheck = checks.GetProperty("db");
    dbCheck.GetProperty("status").GetString().Should().Be(status.ToString());
    dbCheck.GetProperty("description").GetString().Should().Be("Database check");
  }

  [Fact]
  public async Task WriteResponse_Should_SetJsonContentTypeAsync()
  {
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();
    var report = new HealthReport(
      new Dictionary<string, HealthReportEntry>(),
      TimeSpan.FromMilliseconds(1));

    await HealthCheckResponseWriter.WriteResponse(context, report);

    context.Response.ContentType.Should().Be("application/json; charset=utf-8");
  }

  [Fact]
  public async Task WriteResponse_Should_IncludeErrorMessage_WhenExceptionPresentAsync()
  {
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();
    var entries = new Dictionary<string, HealthReportEntry>
    {
      ["redis"] = new(
        HealthStatus.Unhealthy,
        "Redis unreachable",
        TimeSpan.FromMilliseconds(200),
        new InvalidOperationException("Connection refused"),
        null)
    };
    var report = new HealthReport(entries, TimeSpan.FromMilliseconds(200));

    await HealthCheckResponseWriter.WriteResponse(context, report);

    context.Response.Body.Seek(0, SeekOrigin.Begin);
    using var doc = await JsonDocument.ParseAsync(context.Response.Body);
    var redisCheck = doc.RootElement.GetProperty("checks").GetProperty("redis");
    redisCheck.GetProperty("error").GetString().Should().Be("Connection refused");
  }
}
