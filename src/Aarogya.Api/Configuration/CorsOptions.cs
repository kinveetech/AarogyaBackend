namespace Aarogya.Api.Configuration;

public sealed class CorsOptions
{
  public const string SectionName = "Cors";

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Configuration binding requires array type")]
  public string[] AllowedOrigins { get; set; } = [];

  public bool AllowCredentials { get; set; }
}
