using Serilog;
using Serilog.Events;

namespace Aarogya.Api.Configuration;

public static class StartupExtensions
{
  public static IServiceCollection AddAarogyaCorsPolicy(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var corsConfig = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();

    services.AddCors(options =>
    {
      options.AddPolicy("AarogyaPolicy", policy =>
      {
        var origins = corsConfig?.AllowedOrigins ?? [];
        if (origins.Length > 0)
        {
          policy.WithOrigins(origins);

          if (corsConfig!.AllowCredentials)
          {
            policy.AllowCredentials();
          }
        }
        else
        {
          Log.Warning("No CORS origins configured — all cross-origin requests will be blocked. "
            + "Set Cors:AllowedOrigins in appsettings or AAROGYA_Cors__AllowedOrigins__0 env var.");
        }

        policy.AllowAnyMethod().AllowAnyHeader();
      });
    });

    return services;
  }

  public static IApplicationBuilder UseAarogyaRequestLogging(this IApplicationBuilder app)
  {
    app.UseSerilogRequestLogging(options =>
    {
      options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
      options.GetLevel = static (_, elapsed, ex) =>
      {
        if (ex is not null)
        {
          return LogEventLevel.Error;
        }

        return elapsed > 1000 ? LogEventLevel.Warning : LogEventLevel.Information;
      };
      options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
      {
        diagnosticContext.Set("TraceIdentifier", httpContext.TraceIdentifier);
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set(
          "SensitiveHeadersPresent",
          httpContext.Request.Headers.ContainsKey("Authorization")
          || httpContext.Request.Headers.ContainsKey("Cookie"));
      };
    });

    return app;
  }

  public static void ValidateRequiredConfiguration(IConfiguration configuration, IHostEnvironment environment)
  {
    var missingKeys = new List<string>();

    if (string.IsNullOrWhiteSpace(configuration["ConnectionStrings:DefaultConnection"]))
    {
      missingKeys.Add("ConnectionStrings:DefaultConnection");
    }

    var jwtKeyValue = configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKeyValue) || jwtKeyValue == "SET_VIA_USER_SECRETS_OR_ENV_VAR")
    {
      missingKeys.Add("Jwt:Key");
    }

    if (missingKeys.Count <= 0)
    {
      return;
    }

    var message = $"Missing required configuration: {string.Join(", ", missingKeys)}. "
      + "Set via user-secrets, environment variables (prefix AAROGYA_), or appsettings.";

    if (environment.IsDevelopment())
    {
      Log.Warning(message);
    }
    else
    {
      throw new InvalidOperationException(message);
    }
  }
}
