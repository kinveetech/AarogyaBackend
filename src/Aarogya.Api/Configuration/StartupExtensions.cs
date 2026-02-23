using System.Diagnostics.CodeAnalysis;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
          policy.SetPreflightMaxAge(TimeSpan.FromMinutes(10));

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

  public static IServiceCollection AddAarogyaSecurityHeaders(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var options = configuration.GetSection(SecurityHeadersOptions.SectionName).Get<SecurityHeadersOptions>()
      ?? new SecurityHeadersOptions();

    services.AddHsts(hsts =>
    {
      hsts.Preload = options.HstsPreload;
      hsts.IncludeSubDomains = options.HstsIncludeSubDomains;
      hsts.MaxAge = TimeSpan.FromDays(options.HstsMaxAgeDays);
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

  public static IApplicationBuilder UseAarogyaSecurityHeaders(this IApplicationBuilder app)
  {
    var options = app.ApplicationServices.GetRequiredService<IOptions<SecurityHeadersOptions>>().Value;

    app.Use(async (context, next) =>
    {
      context.Response.OnStarting(static state =>
      {
        var (httpContext, securityHeadersOptions) = ((HttpContext, SecurityHeadersOptions))state;
        var headers = httpContext.Response.Headers;

        headers["Content-Security-Policy"] = securityHeadersOptions.ContentSecurityPolicy;
        headers["X-Frame-Options"] = securityHeadersOptions.XFrameOptions;
        headers["X-Content-Type-Options"] = securityHeadersOptions.XContentTypeOptions;
        headers["Referrer-Policy"] = securityHeadersOptions.ReferrerPolicy;

        return Task.CompletedTask;
      }, (context, options));

      await next();
    });

    return app;
  }

  public static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken cancellationToken = default)
  {
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AarogyaDbContext>();
    var configuration = services.GetRequiredService<IConfiguration>();

    var autoMigrate = configuration.GetSection("Database:AutoMigrateOnStartup").Get<bool?>();
    if (autoMigrate ?? false)
    {
      await dbContext.Database.MigrateAsync(cancellationToken);
    }

    var seeder = services.GetRequiredService<IDataSeeder>();
    await seeder.SeedAsync(cancellationToken);
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

    var cognitoUserPoolId = configuration["Aws:Cognito:UserPoolId"];
    if (IsMissingConfigurationValue(cognitoUserPoolId))
    {
      violations.Add("Missing Aws:Cognito:UserPoolId");
    }

    var cognitoAppClientId = configuration["Aws:Cognito:AppClientId"];
    if (IsMissingConfigurationValue(cognitoAppClientId))
    {
      violations.Add("Missing Aws:Cognito:AppClientId");
    }

    var useLocalStack = configuration.GetSection("Aws:UseLocalStack").Get<bool?>() ?? false;
    if (!useLocalStack)
    {
      var cognitoDomain = configuration["Aws:Cognito:Domain"];
      if (IsMissingConfigurationValue(cognitoDomain))
      {
        violations.Add("Missing Aws:Cognito:Domain (required for non-LocalStack environments)");
      }
    }

    AddSocialProviderConfigurationViolations(configuration, violations);

    return violations;
  }

  private static void AddSocialProviderConfigurationViolations(IConfiguration configuration, List<string> violations)
  {
    var redirectUris = configuration.GetSection("Aws:Cognito:SocialIdentityProviders:MobileRedirectUris").Get<string[]>() ?? [];
    if (redirectUris.Length == 0 || Array.TrueForAll(redirectUris, IsMissingConfigurationValue))
    {
      violations.Add("Missing Aws:Cognito:SocialIdentityProviders:MobileRedirectUris");
    }

    foreach (var provider in new[] { "Google", "Apple", "Facebook" })
    {
      ValidateProvider(provider);
    }

    return;

    void ValidateProvider(string provider)
    {
      var enabledKey = $"Aws:Cognito:SocialIdentityProviders:{provider}:Enabled";
      var enabledRaw = configuration[enabledKey];
      if (IsMissingConfigurationValue(enabledRaw))
      {
        violations.Add($"Missing {enabledKey}");
        return;
      }

      var isEnabled = bool.TryParse(enabledRaw, out var parsedEnabled) && parsedEnabled;
      if (!isEnabled)
      {
        return;
      }

      var clientId = configuration[$"Aws:Cognito:SocialIdentityProviders:{provider}:ClientId"];
      if (IsMissingConfigurationValue(clientId))
      {
        violations.Add($"Missing Aws:Cognito:SocialIdentityProviders:{provider}:ClientId");
      }

      var clientSecret = configuration[$"Aws:Cognito:SocialIdentityProviders:{provider}:ClientSecret"];
      if (IsMissingConfigurationValue(clientSecret))
      {
        violations.Add($"Missing Aws:Cognito:SocialIdentityProviders:{provider}:ClientSecret");
      }
    }
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

    var enforceTls13 = configuration.GetSection("TransportSecurity").GetSection("EnforceTls13").Get<bool?>() ?? false;
    if (enforceTls13)
    {
      AddTransportSecurityViolations(configuration, violations);
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

  private static void AddTransportSecurityViolations(IConfiguration configuration, List<string> violations)
  {
    var defaultConnection = configuration["ConnectionStrings:DefaultConnection"] ?? string.Empty;
    if (!HasSecurePostgreSqlSslMode(defaultConnection))
    {
      violations.Add("ConnectionStrings:DefaultConnection must set SSL Mode=Require|VerifyCA|VerifyFull when TransportSecurity:EnforceTls13=true");
    }

    if (defaultConnection.Contains("Trust Server Certificate=true", StringComparison.OrdinalIgnoreCase))
    {
      violations.Add("ConnectionStrings:DefaultConnection cannot trust server certificates when TransportSecurity:EnforceTls13=true");
    }

    var redisConnection = configuration["ConnectionStrings:Redis"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(redisConnection)
      && !redisConnection.Contains("ssl=true", StringComparison.OrdinalIgnoreCase))
    {
      violations.Add("ConnectionStrings:Redis must include ssl=true when TransportSecurity:EnforceTls13=true");
    }

    var useLocalStack = configuration.GetSection("Aws:UseLocalStack").Get<bool?>() ?? false;
    var awsServiceUrl = configuration["Aws:ServiceUrl"];
    if (!useLocalStack
      && !string.IsNullOrWhiteSpace(awsServiceUrl)
      && !awsServiceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      violations.Add("Aws:ServiceUrl must use HTTPS when TransportSecurity:EnforceTls13=true");
    }
  }

  private static bool ContainsAny(string value, params string[] patterns)
  {
    return Array.Exists(patterns, pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
  }

  private static bool HasSecurePostgreSqlSslMode(string connectionString)
  {
    return connectionString.Contains("ssl mode=require", StringComparison.OrdinalIgnoreCase)
      || connectionString.Contains("ssl mode=verifyca", StringComparison.OrdinalIgnoreCase)
      || connectionString.Contains("ssl mode=verifyfull", StringComparison.OrdinalIgnoreCase);
  }

  private static bool IsMissingConfigurationValue(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return true;
    }

    return value.Equals("SET_VIA_USER_SECRETS_OR_ENV_VAR", StringComparison.OrdinalIgnoreCase)
      || value.Equals("SET_VIA_ENV_VAR", StringComparison.OrdinalIgnoreCase);
  }
}
