using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Aarogya.Api.Configuration;

public static class AuthenticationExtensions
{
  public static IServiceCollection AddCognitoJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    var awsOptions = new AwsOptions();
    configuration.GetSection(AwsOptions.SectionName).Bind(awsOptions);

    if (string.IsNullOrWhiteSpace(awsOptions.Cognito.UserPoolId))
    {
      throw new InvalidOperationException("Aws:Cognito:UserPoolId is required for Cognito JWT validation.");
    }

    if (string.IsNullOrWhiteSpace(awsOptions.Cognito.AppClientId))
    {
      throw new InvalidOperationException("Aws:Cognito:AppClientId is required for Cognito JWT validation.");
    }

    var issuer = ResolveCognitoIssuer(awsOptions);

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.MapInboundClaims = false;
        options.Authority = issuer;
        options.RequireHttpsMetadata = ShouldRequireHttpsMetadata(awsOptions, issuer);

        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = issuer,
          ValidateAudience = true,
          ValidAudience = awsOptions.Cognito.AppClientId,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          NameClaimType = "sub",
          RoleClaimType = "cognito:groups"
        };
      });

    return services;
  }

  internal static string ResolveCognitoIssuer(AwsOptions awsOptions)
  {
    ArgumentNullException.ThrowIfNull(awsOptions);

    if (!string.IsNullOrWhiteSpace(awsOptions.Cognito.Issuer))
    {
      return awsOptions.Cognito.Issuer.TrimEnd('/');
    }

    if (string.IsNullOrWhiteSpace(awsOptions.Cognito.UserPoolId))
    {
      throw new InvalidOperationException("Aws:Cognito:UserPoolId is required to resolve Cognito issuer.");
    }

    if (awsOptions.UseLocalStack && !string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
    {
      return $"{awsOptions.ServiceUrl.TrimEnd('/')}/{awsOptions.Cognito.UserPoolId}";
    }

    return $"https://cognito-idp.{awsOptions.Region}.amazonaws.com/{awsOptions.Cognito.UserPoolId}";
  }

  internal static bool ShouldRequireHttpsMetadata(AwsOptions awsOptions, string issuer)
  {
    ArgumentNullException.ThrowIfNull(awsOptions);
    ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

    return !awsOptions.UseLocalStack
      && !issuer.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
  }
}
