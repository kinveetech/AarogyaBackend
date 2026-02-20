using System.Security.Claims;
using System.Text.Encodings.Web;
using Aarogya.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class ApiKeyAuthenticationHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  IApiKeyService apiKeyService)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "ApiKey";
  public const string HeaderName = "X-API-Key";

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(HeaderName, out var values))
    {
      return AuthenticateResult.NoResult();
    }

    var apiKey = values.ToString();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      return AuthenticateResult.Fail("API key is required.");
    }

    var validation = await apiKeyService.ValidateKeyAsync(apiKey, Context.RequestAborted);
    if (!validation.Success)
    {
      if (validation.IsRateLimited)
      {
        Context.Items["ApiKeyRateLimited"] = true;
      }

      return AuthenticateResult.Fail(validation.Message);
    }

    var claims = new List<Claim>
    {
      new("sub", $"lab:{validation.PartnerId}"),
      new("api_key_id", validation.KeyId ?? string.Empty),
      new("lab_partner_id", validation.PartnerId ?? string.Empty),
      new("lab_partner_name", validation.PartnerName ?? string.Empty),
      new("auth_method", "api_key"),
      new(ClaimTypes.Role, AarogyaRoles.LabTechnician)
    };

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);
    return AuthenticateResult.Success(ticket);
  }

  protected override Task HandleChallengeAsync(AuthenticationProperties properties)
  {
    if (Context.Items.TryGetValue("ApiKeyRateLimited", out var value)
      && value is true)
    {
      Response.StatusCode = StatusCodes.Status429TooManyRequests;
      return Response.WriteAsync("Rate limit exceeded for API key.");
    }

    return base.HandleChallengeAsync(properties);
  }
}
