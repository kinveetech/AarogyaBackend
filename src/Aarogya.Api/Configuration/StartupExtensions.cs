using System.Diagnostics.CodeAnalysis;
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
    var violations = new List<string>();

    violations.AddRange(GetMissingRequiredConfiguration(configuration));
    violations.AddRange(GetSecurityConfigurationViolations(configuration, environment));

    var joinedViolations = string.Join("; ", violations);
    if (string.IsNullOrWhiteSpace(joinedViolations))
    {
      return;
    }

    var message = $"Invalid configuration: {joinedViolations}. "
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

  private static List<string> GetMissingRequiredConfiguration(IConfiguration configuration)
  {
    var violations = new List<string>();

    if (string.IsNullOrWhiteSpace(configuration["ConnectionStrings:DefaultConnection"]))
    {
      violations.Add("Missing ConnectionStrings:DefaultConnection");
    }

    var jwtKeyValue = configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKeyValue) || jwtKeyValue == "SET_VIA_USER_SECRETS_OR_ENV_VAR")
    {
      violations.Add("Missing Jwt:Key");
    }

    return violations;
  }

  [SuppressMessage(
    "Security",
    "S2068:Credentials should not be hard-coded",
    Justification = "These are sentinel placeholders used to detect insecure default configuration values.")]
  private static List<string> GetSecurityConfigurationViolations(
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    var violations = new List<string>();
    var jwtKeyValue = configuration["Jwt:Key"];

    if (!string.IsNullOrWhiteSpace(jwtKeyValue)
      && jwtKeyValue != "SET_VIA_USER_SECRETS_OR_ENV_VAR"
      && jwtKeyValue.Length < 32)
    {
      violations.Add("Jwt:Key must be at least 32 characters long");
    }

    var defaultConnection = configuration["ConnectionStrings:DefaultConnection"] ?? string.Empty;
    if (!environment.IsDevelopment()
      && ContainsAny(defaultConnection, "aarogya_dev_password", "your_password", "password=changeme"))
    {
      violations.Add("ConnectionStrings:DefaultConnection contains insecure default credentials");
    }

    var redisConnection = configuration["ConnectionStrings:Redis"] ?? string.Empty;
    if (!environment.IsDevelopment()
      && ContainsAny(redisConnection, "redis_password", "changeme", "SET_VIA_USER_SECRETS_OR_ENV_VAR"))
    {
      violations.Add("ConnectionStrings:Redis contains insecure default credentials");
    }

    var awsServiceUrl = configuration["Aws:ServiceUrl"];
    if (!string.IsNullOrWhiteSpace(awsServiceUrl)
      && (!Uri.TryCreate(awsServiceUrl, UriKind.Absolute, out var parsed)
      || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
    {
      violations.Add("Aws:ServiceUrl must be a valid absolute HTTP/HTTPS URL");
    }

    var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    foreach (var origin in corsOrigins)
    {
      if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
        || (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps))
      {
        violations.Add($"Cors:AllowedOrigins contains invalid URL: {origin}");
      }
    }

    return violations;
  }

  private static bool ContainsAny(string value, params string[] patterns)
  {
    return Array.Exists(patterns, pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
  }
}
