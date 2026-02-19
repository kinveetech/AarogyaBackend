using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aarogya.Api.Health;

internal static class HealthCheckResponseWriter
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    WriteIndented = true
  };

  public static Task WriteResponse(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
      status = report.Status.ToString(),
      totalDurationMs = report.TotalDuration.TotalMilliseconds,
      checks = report.Entries.ToDictionary(
        entry => entry.Key,
        entry => new
        {
          status = entry.Value.Status.ToString(),
          durationMs = entry.Value.Duration.TotalMilliseconds,
          description = entry.Value.Description,
          error = entry.Value.Exception?.Message,
          data = entry.Value.Data.ToDictionary(
            item => item.Key,
            item => item.Value?.ToString())
        })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
  }
}
