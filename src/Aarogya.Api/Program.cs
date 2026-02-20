using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Authorization;
using Aarogya.Api.Configuration;
using Aarogya.Api.Endpoints;
using Aarogya.Api.Features.V1;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Health;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Security;
using Aarogya.Api.Validation;
using Aarogya.Infrastructure;
using Aarogya.Infrastructure.Security;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
const int MaxReportUploadSizeBytes = 50 * 1024 * 1024;

// Add AAROGYA_ prefixed environment variables as a configuration source.
// This allows deployment environments to set values like
// AAROGYA_Aws__Cognito__UserPoolId and AAROGYA_Aws__Cognito__AppClientId,
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
  .AddOptionsWithValidateOnStart<AwsOptions>()
  .BindConfiguration(AwsOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptions<JwtOptions>()
  .BindConfiguration(JwtOptions.SectionName);

builder.Services
  .AddOptionsWithValidateOnStart<RedisOptions>()
  .BindConfiguration(RedisOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<CorsOptions>()
  .BindConfiguration(CorsOptions.SectionName);

builder.Services
  .AddOptionsWithValidateOnStart<SecurityHeadersOptions>()
  .BindConfiguration(SecurityHeadersOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<DatabaseOptions>()
  .BindConfiguration(DatabaseOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<OtpOptions>()
  .BindConfiguration(OtpOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<PkceOptions>()
  .BindConfiguration(PkceOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<ApiKeyOptions>()
  .BindConfiguration(ApiKeyOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<AccessGrantOptions>()
  .BindConfiguration(AccessGrantOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<RateLimitingOptions>()
  .BindConfiguration(RateLimitingOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<VirusScanningOptions>()
  .BindConfiguration(VirusScanningOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<FileDeletionOptions>()
  .BindConfiguration(FileDeletionOptions.SectionName)
  .ValidateDataAnnotations();

builder.Services
  .AddOptionsWithValidateOnStart<DataEncryptionRotationOptions>()
  .BindConfiguration(DataEncryptionRotationOptions.SectionName)
  .ValidateDataAnnotations();

// Add Infrastructure services (DbContext, health checks, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

// Add services to the container
builder.Services
  .AddControllers()
  .AddJsonOptions(options =>
  {
    // Keep JSON payload handling locked to declared DTO contracts.
    options.JsonSerializerOptions.TypeInfoResolverChain.Clear();
    options.JsonSerializerOptions.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
    options.JsonSerializerOptions.UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement;
    options.JsonSerializerOptions.MaxDepth = 32;
  })
  .ConfigureApiBehaviorOptions(options =>
  {
    options.InvalidModelStateResponseFactory = context =>
      new BadRequestObjectResult(ValidationErrorResponse.FromModelState(context.ModelState));
  });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
  options.MultipartBodyLengthLimit = MaxReportUploadSizeBytes;
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<OtpRequestCommandValidator>(includeInternalTypes: true);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient(CognitoOAuthTokenClient.HttpClientName, client =>
{
  client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
builder.Services.AddSingleton<IPhoneOtpSender, MockPhoneOtpSender>();
builder.Services.AddSingleton<IPhoneOtpService, InMemoryPhoneOtpService>();
builder.Services.AddSingleton<IApiKeyService, InMemoryApiKeyService>();
builder.Services.AddSingleton<IPkceAuthorizationService, InMemoryPkceAuthorizationService>();
builder.Services.AddSingleton<ICognitoSocialTokenClient, CognitoOAuthTokenClient>();
builder.Services.AddSingleton<ISocialAuthService, InMemorySocialAuthService>();
builder.Services.AddSingleton<IRoleAssignmentService, InMemoryRoleAssignmentService>();
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();
builder.Services.AddHostedService<DataEncryptionKeyRotationHostedService>();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "Aarogya API",
    Version = "v1",
    Description = "Backend API for Aarogya mobile application (versioned under /api/v1)."
  });

  c.SupportNonNullableReferenceTypes();

  // Add JWT Authentication to Swagger
  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
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

builder.Services.AddCognitoJwtAuthentication(builder.Configuration);
builder.Services.AddAarogyaAuthorization();

builder.Services.AddAarogyaCorsPolicy(builder.Configuration);
builder.Services.AddAarogyaSecurityHeaders(builder.Configuration);
builder.Services.AddV1FeatureServices();

var rateLimitingOptions = builder.Configuration
  .GetSection(RateLimitingOptions.SectionName)
  .Get<RateLimitingOptions>() ?? new RateLimitingOptions();
builder.Services.AddAarogyaRateLimiting(rateLimitingOptions);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
  options.KnownNetworks.Clear();
  options.KnownProxies.Clear();
});

var app = builder.Build();

// Validate required configuration at startup
StartupExtensions.ValidateRequiredConfiguration(app.Configuration, app.Environment);
await StartupExtensions.InitializeDatabaseAsync(app);

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
  c.RoutePrefix = "swagger";
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aarogya API v1");
});

app.UseAarogyaRequestLogging();
app.UseAarogyaApiVersioning();
app.UseForwardedHeaders();
app.UseAarogyaSecurityHeaders();

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
  app.UseHsts();
}
app.UseCors("AarogyaPolicy");
app.UseAuthentication();
if (rateLimitingOptions.EnableRateLimiting)
{
  app.UseRateLimiter();
  app.UseMiddleware<RateLimitHeadersMiddleware>();
}
app.UseAuthorization();
app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
  Predicate = check => check.Tags.Contains("live"),
  ResponseWriter = HealthCheckResponseWriter.WriteResponse
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
  Predicate = check => check.Tags.Contains("ready"),
  ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

try
{
  Log.Information("Starting Aarogya API ({Environment})", app.Environment.EnvironmentName);
  await app.RunAsync();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application start-up failed");
}
finally
{
  await Log.CloseAndFlushAsync();
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Public for WebApplicationFactory-based integration tests.")]
[SuppressMessage(
  "Major Code Smell",
  "S1118",
  Justification = "Top-level statements generate the entry point; this partial declaration enables test hosting.")]
public partial class Program
{
}
