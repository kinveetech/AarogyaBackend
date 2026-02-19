using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Aarogya.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aarogya.Api.Controllers;

[ApiController]
[Route("api/auth")]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class AuthController : ControllerBase
{
  private readonly IPhoneOtpService _phoneOtpService;
  private readonly IPkceAuthorizationService _pkceAuthorizationService;

  public AuthController(
    IPhoneOtpService phoneOtpService,
    IPkceAuthorizationService pkceAuthorizationService)
  {
    _phoneOtpService = phoneOtpService;
    _pkceAuthorizationService = pkceAuthorizationService;
  }

  [AllowAnonymous]
  [HttpPost("otp/request")]
  [ProducesResponseType(typeof(OtpRequestResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(OtpResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(OtpResponse), StatusCodes.Status429TooManyRequests)]
  public async Task<IActionResult> RequestPhoneOtpAsync([FromBody] OtpRequestCommand request, CancellationToken cancellationToken)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(new OtpResponse("Invalid request payload."));
    }

    var result = await _phoneOtpService.RequestOtpAsync(request.PhoneNumber, cancellationToken);
    if (!result.Success)
    {
      if (result.IsRateLimited)
      {
        return StatusCode(StatusCodes.Status429TooManyRequests, new OtpResponse(result.Message));
      }

      return BadRequest(new OtpResponse(result.Message));
    }

    return Ok(new OtpRequestResponse(result.Message, result.ExpiresAt));
  }

  [AllowAnonymous]
  [HttpPost("otp/verify")]
  [ProducesResponseType(typeof(OtpResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(OtpResponse), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> VerifyPhoneOtpAsync([FromBody] OtpVerifyCommand request, CancellationToken cancellationToken)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(new OtpResponse("Invalid request payload."));
    }

    var result = await _phoneOtpService.VerifyOtpAsync(request.PhoneNumber, request.Otp, cancellationToken);
    if (!result.Success)
    {
      return BadRequest(new OtpResponse(result.Message));
    }

    return Ok(new OtpResponse(result.Message));
  }

  [AllowAnonymous]
  [HttpPost("pkce/authorize")]
  [ProducesResponseType(typeof(PkceAuthorizeResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(PkceErrorResponse), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CreatePkceAuthorizationCodeAsync(
    [FromBody] PkceAuthorizeCommand request,
    CancellationToken cancellationToken)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(new PkceErrorResponse("Invalid request payload."));
    }

    var result = await _pkceAuthorizationService.CreateAuthorizationCodeAsync(
      new PkceAuthorizeRequest(
        request.ClientId,
        request.RedirectUri,
        request.CodeChallenge,
        request.CodeChallengeMethod,
        request.Platform,
        request.Scope,
        request.State),
      cancellationToken);

    if (!result.Success)
    {
      return BadRequest(new PkceErrorResponse(result.Message));
    }

    return Ok(new PkceAuthorizeResponse(result.AuthorizationCode!, result.ExpiresAt!.Value, result.State));
  }

  [AllowAnonymous]
  [HttpPost("pkce/token")]
  [ProducesResponseType(typeof(PkceTokenResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(PkceErrorResponse), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> ExchangePkceAuthorizationCodeAsync(
    [FromBody] PkceTokenCommand request,
    CancellationToken cancellationToken)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(new PkceErrorResponse("Invalid request payload."));
    }

    var result = await _pkceAuthorizationService.ExchangeAuthorizationCodeAsync(
      new PkceTokenRequest(
        request.ClientId,
        request.RedirectUri,
        request.AuthorizationCode,
        request.CodeVerifier),
      cancellationToken);

    if (!result.Success)
    {
      return BadRequest(new PkceErrorResponse(result.Message));
    }

    return Ok(new PkceTokenResponse(
      result.AccessToken!,
      result.RefreshToken!,
      result.IdToken!,
      result.TokenType,
      result.ExpiresInSeconds));
  }

  [Authorize]
  [HttpGet("me")]
  [ProducesResponseType(typeof(AuthClaimsResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public ActionResult<AuthClaimsResponse> GetCurrentUserClaims()
  {
    var claimsPrincipal = User;
    var sub = claimsPrincipal.FindFirstValue("sub");
    var email = claimsPrincipal.FindFirstValue("email");

    var roles = claimsPrincipal.Claims
      .Where(claim => claim.Type is "cognito:groups" or ClaimTypes.Role or "role")
      .Select(claim => claim.Value)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();

    return Ok(new AuthClaimsResponse(sub, email, roles));
  }
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Response DTO is referenced by a public controller action signature.")]
public sealed record AuthClaimsResponse(string? Sub, string? Email, IReadOnlyList<string> Roles);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record OtpRequestCommand(
  [property: System.ComponentModel.DataAnnotations.Required]
  string PhoneNumber);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record OtpVerifyCommand(
  [property: System.ComponentModel.DataAnnotations.Required]
  string PhoneNumber,
  [property: System.ComponentModel.DataAnnotations.Required]
  string Otp);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public response metadata attributes.")]
public sealed record OtpResponse(string Message);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public response metadata attributes.")]
public sealed record OtpRequestResponse(string Message, DateTimeOffset? ExpiresAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record PkceAuthorizeCommand(
  [property: System.ComponentModel.DataAnnotations.Required]
  string ClientId,
  [property: System.ComponentModel.DataAnnotations.Required]
  Uri RedirectUri,
  [property: System.ComponentModel.DataAnnotations.Required]
  string CodeChallenge,
  [property: System.ComponentModel.DataAnnotations.Required]
  string CodeChallengeMethod,
  [property: System.ComponentModel.DataAnnotations.Required]
  string Platform,
  string? Scope,
  string? State);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record PkceTokenCommand(
  [property: System.ComponentModel.DataAnnotations.Required]
  string ClientId,
  [property: System.ComponentModel.DataAnnotations.Required]
  Uri RedirectUri,
  [property: System.ComponentModel.DataAnnotations.Required]
  string AuthorizationCode,
  [property: System.ComponentModel.DataAnnotations.Required]
  string CodeVerifier);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public response metadata attributes.")]
public sealed record PkceAuthorizeResponse(string AuthorizationCode, DateTimeOffset ExpiresAt, string? State);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public response metadata attributes.")]
public sealed record PkceTokenResponse(
  string AccessToken,
  string RefreshToken,
  string IdToken,
  string TokenType,
  int ExpiresInSeconds);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public response metadata attributes.")]
public sealed record PkceErrorResponse(string Error);
