using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Aarogya.ServiceDefaults;

public static class Extensions
{
  public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
  {
    builder.ConfigureOpenTelemetry();
    return builder;
  }

  private static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
  {
    builder.Logging.AddOpenTelemetry(logging =>
    {
      logging.IncludeFormattedMessage = true;
      logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
      .WithMetrics(metrics =>
      {
        metrics
          .AddAspNetCoreInstrumentation()
          .AddHttpClientInstrumentation()
          .AddRuntimeInstrumentation();
      })
      .WithTracing(tracing =>
      {
        tracing
          .AddSource("Npgsql")
          .AddAspNetCoreInstrumentation(options =>
          {
            options.Filter = httpContext =>
              !httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
          })
          .AddHttpClientInstrumentation();
      });

    AddOpenTelemetryExporters(builder);

    return builder;
  }

  private static void AddOpenTelemetryExporters(IHostApplicationBuilder builder)
  {
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    if (string.IsNullOrWhiteSpace(otlpEndpoint))
    {
      return;
    }

    builder.Services.AddOpenTelemetry()
      .UseOtlpExporter();
  }
}
