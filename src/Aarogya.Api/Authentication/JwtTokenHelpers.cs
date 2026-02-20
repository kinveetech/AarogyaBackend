using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aarogya.Api.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Aarogya.Api.Authentication;

internal static class JwtTokenHelpers
{
  public static string GenerateToken(int bytesLength)
  {
    var bytes = RandomNumberGenerator.GetBytes(bytesLength);
    return Convert.ToBase64String(bytes)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
  }

  public static bool CanIssueJwtTokens(JwtOptions jwtOptions)
  {
    if (string.IsNullOrWhiteSpace(jwtOptions.Key))
    {
      return false;
    }

    if (jwtOptions.Key.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    return jwtOptions.Key.Length >= 32
      && !string.IsNullOrWhiteSpace(jwtOptions.Issuer)
      && !string.IsNullOrWhiteSpace(jwtOptions.Audience);
  }

  public static string GenerateJwtToken(
    IUtcClock clock,
    JwtOptions jwtOptions,
    int accessTokenExpirySeconds,
    IReadOnlyCollection<Claim> claims)
  {
    var now = clock.UtcNow;
    var expiresAt = now.AddSeconds(accessTokenExpirySeconds);
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
    var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var descriptor = new SecurityTokenDescriptor
    {
      Issuer = jwtOptions.Issuer,
      Audience = jwtOptions.Audience,
      Subject = new ClaimsIdentity(claims),
      NotBefore = now.UtcDateTime,
      Expires = expiresAt.UtcDateTime,
      SigningCredentials = signingCredentials
    };

    var handler = new JwtSecurityTokenHandler();
    var token = handler.CreateToken(descriptor);
    return handler.WriteToken(token);
  }
}
