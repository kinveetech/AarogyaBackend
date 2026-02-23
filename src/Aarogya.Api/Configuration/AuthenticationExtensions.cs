using System.Text;
using Aarogya.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Aarogya.Api.Configuration;

public static class AuthenticationExtensions
{
  private const string CognitoJwtScheme = "CognitoJwt";
  private const string LocalJwtScheme = "LocalJwt";
  private const string ApiKeyScheme = ApiKeyAuthenticationHandler.SchemeName;

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
    var jwtOptions = new JwtOptions();
    configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
    var hasLocalJwt = HasLocalJwtConfiguration(jwtOptions);

    services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddPolicyScheme(JwtBearerDefaults.AuthenticationScheme, "Bearer token selector", options =>
      {
        options.ForwardDefaultSelector = context =>
        {
          var bearerToken = ExtractBearerToken(context.Request.Headers.Authorization);
          if (!string.IsNullOrWhiteSpace(bearerToken))
          {
            if (hasLocalJwt)
            {
              var tokenIssuer = TryReadIssuer(bearerToken);
              if (string.Equals(tokenIssuer, jwtOptions.Issuer, StringComparison.Ordinal))
              {
                return LocalJwtScheme;
              }
            }

            return CognitoJwtScheme;
          }

          if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName))
          {
            return ApiKeyScheme;
          }

          return CognitoJwtScheme;
        };
      })
      .AddJwtBearer(CognitoJwtScheme, options =>
      {
        options.MapInboundClaims = false;
        options.Authority = issuer;
        options.RequireHttpsMetadata = ShouldRequireHttpsMetadata(awsOptions, issuer);

        var expectedClientId = awsOptions.Cognito.AppClientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = issuer,
          ValidateAudience = true,
          ValidAudience = expectedClientId,
          AudienceValidator = (audiences, token, _) =>
          {
            // Cognito ID tokens have "aud" = client_id.
            // Cognito access tokens omit "aud" and use "client_id" instead.
            if (audiences.Any(a => string.Equals(a, expectedClientId, StringComparison.Ordinal)))
            {
              return true;
            }

            if (token is JsonWebToken jwt
              && jwt.TryGetPayloadValue<string>("client_id", out var clientId))
            {
              return string.Equals(clientId, expectedClientId, StringComparison.Ordinal);
            }

            return false;
          },
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          NameClaimType = "sub",
          RoleClaimType = "cognito:groups"
        };
      });

    services.AddAuthentication()
      .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, _ => { });

    if (hasLocalJwt)
    {
      services.AddAuthentication()
        .AddJwtBearer(LocalJwtScheme, options =>
        {
          options.MapInboundClaims = false;
          options.RequireHttpsMetadata = false;

          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            NameClaimType = "sub",
            RoleClaimType = "role"
          };
        });
    }

    return services;
  }

  internal static string ResolveCognitoIssuer(AwsOptions awsOptions)
  {
    ArgumentNullException.ThrowIfNull(awsOptions);

    var configuredIssuer = awsOptions.Cognito.Issuer;
    if (!string.IsNullOrWhiteSpace(configuredIssuer)
      && !IsPlaceholderValue(configuredIssuer)
      && Uri.TryCreate(configuredIssuer, UriKind.Absolute, out var parsedIssuer))
    {
      return parsedIssuer.ToString().TrimEnd('/');
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

  internal static string ResolveCognitoOAuthBaseUrl(AwsOptions awsOptions)
  {
    ArgumentNullException.ThrowIfNull(awsOptions);

    if (awsOptions.UseLocalStack && !string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
    {
      if (string.IsNullOrWhiteSpace(awsOptions.Cognito.UserPoolId))
      {
        throw new InvalidOperationException("Aws:Cognito:UserPoolId is required to resolve Cognito OAuth base URL.");
      }

      return $"{awsOptions.ServiceUrl.TrimEnd('/')}/{awsOptions.Cognito.UserPoolId}";
    }

    if (string.IsNullOrWhiteSpace(awsOptions.Cognito.Domain) || IsPlaceholderValue(awsOptions.Cognito.Domain))
    {
      throw new InvalidOperationException(
        "Aws:Cognito:Domain is required to resolve Cognito OAuth base URL for non-LocalStack environments.");
    }

    return $"https://{awsOptions.Cognito.Domain.Trim()}.auth.{awsOptions.Region}.amazoncognito.com";
  }

  internal static bool ShouldRequireHttpsMetadata(AwsOptions awsOptions, string issuer)
  {
    ArgumentNullException.ThrowIfNull(awsOptions);
    ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

    return !awsOptions.UseLocalStack
      && !issuer.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
  }

  private static bool HasLocalJwtConfiguration(JwtOptions jwtOptions)
  {
    if (string.IsNullOrWhiteSpace(jwtOptions.Key)
      || string.IsNullOrWhiteSpace(jwtOptions.Issuer)
      || string.IsNullOrWhiteSpace(jwtOptions.Audience))
    {
      return false;
    }

    if (jwtOptions.Key.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    return jwtOptions.Key.Length >= 32;
  }

  private static string? TryReadIssuer(string token)
  {
    var handler = new JsonWebTokenHandler();
    if (!handler.CanReadToken(token))
    {
      return null;
    }

    var jsonToken = handler.ReadJsonWebToken(token);
    return jsonToken.Issuer;
  }

  private static string? ExtractBearerToken(StringValues authorizationHeader)
  {
    if (StringValues.IsNullOrEmpty(authorizationHeader))
    {
      return null;
    }

    var header = authorizationHeader.ToString();
    const string bearerPrefix = "Bearer ";
    if (!header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return null;
    }

    return header[bearerPrefix.Length..].Trim();
  }

  private static bool IsPlaceholderValue(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return true;
    }

    return value.Contains("SET_VIA_ENV_VAR", StringComparison.OrdinalIgnoreCase)
      || value.Contains("SET_VIA_USER_SECRETS_OR_ENV_VAR", StringComparison.OrdinalIgnoreCase);
  }
}
