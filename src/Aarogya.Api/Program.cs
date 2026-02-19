using Aarogya.Api.Configuration;
using Aarogya.Api.Health;
using Aarogya.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;

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

builder.Services.AddAarogyaCorsPolicy(builder.Configuration);

var app = builder.Build();

// Validate required configuration at startup
StartupExtensions.ValidateRequiredConfiguration(app.Configuration, app.Environment);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI(c =>
  {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aarogya API v1");
  });
}

app.UseAarogyaRequestLogging();

app.UseHttpsRedirection();
app.UseCors("AarogyaPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
  Predicate = _ => true,
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
