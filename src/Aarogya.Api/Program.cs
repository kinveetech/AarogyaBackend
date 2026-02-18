using Aarogya.Api.Configuration;
using Aarogya.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add AAROGYA_ prefixed environment variables as a configuration source.
// This allows deployment environments to set AAROGYA_Jwt__Key instead of Jwt__Key,
// avoiding collisions with other applications on the same host.
builder.Configuration.AddEnvironmentVariables(prefix: "AAROGYA_");

// Configure Serilog
Log.Logger = new LoggerConfiguration()
  .ReadFrom.Configuration(builder.Configuration)
  .Enrich.FromLogContext()
  .CreateLogger();

builder.Host.UseSerilog();

// Bind and validate strongly-typed configuration options
builder.Services
  .AddOptionsWithValidateOnStart<JwtOptions>()
  .BindConfiguration(JwtOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<AwsOptions>()
  .BindConfiguration(AwsOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<RedisOptions>()
  .BindConfiguration(RedisOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<CorsOptions>()
  .BindConfiguration(CorsOptions.SectionName);

builder.Services
  .AddOptionsWithValidateOnStart<DatabaseOptions>()
  .BindConfiguration(DatabaseOptions.SectionName)
  .ValidateDataAnnotations();

// Add Infrastructure services (DbContext, health checks, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "Aarogya API",
    Version = "v1",
    Description = "Backend API for Aarogya mobile application"
  });

  // Add JWT Authentication to Swagger
  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer"
  });

  c.AddSecurityRequirement(new OpenApiSecurityRequirement
  {
    {
      new OpenApiSecurityScheme
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = "Bearer"
        }
      },
      Array.Empty<string>()
    }
  });
});

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
  ?? throw new InvalidOperationException(
    "JWT Key is not configured. Set via user-secrets, env var (Jwt__Key or AAROGYA_Jwt__Key), or appsettings.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidateAudience = true,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      ValidIssuer = builder.Configuration["Jwt:Issuer"],
      ValidAudience = builder.Configuration["Jwt:Audience"],
      IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
        System.Text.Encoding.UTF8.GetBytes(jwtKey))
    };
  });

// Add CORS
var corsConfig = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
builder.Services.AddCors(options =>
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

var app = builder.Build();

// Validate required configuration at startup
ValidateRequiredConfiguration(app.Configuration, app.Environment);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(c =>
  {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aarogya API v1");
  });
}

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

app.UseHttpsRedirection();
app.UseCors("AarogyaPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
  Predicate = _ => true
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
  Predicate = check => check.Tags.Contains("ready")
});

try
{
  Log.Information("Starting Aarogya API ({Environment})", app.Environment.EnvironmentName);
  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application start-up failed");
}
finally
{
  Log.CloseAndFlush();
}

static void ValidateRequiredConfiguration(IConfiguration configuration, IHostEnvironment environment)
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
