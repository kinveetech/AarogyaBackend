using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class PkceOptions
{
  public const string SectionName = "Pkce";

  [Range(30, 900)]
  public int AuthorizationCodeExpirySeconds { get; set; } = 300;

  [Range(60, 3600)]
  public int AccessTokenExpirySeconds { get; set; } = 900;

  [Range(300, 2592000)]
  public int RefreshTokenExpirySeconds { get; set; } = 2592000;
}
