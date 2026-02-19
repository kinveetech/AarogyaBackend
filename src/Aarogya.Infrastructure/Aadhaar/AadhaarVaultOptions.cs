namespace Aarogya.Infrastructure.Aadhaar;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type")]
public sealed class AadhaarVaultOptions
{
  public const string SectionName = "AadhaarVault";

  public bool UseMockApi { get; set; } = true;

  public string MockApiBaseUrl { get; set; } = "http://localhost:5099";

  public string ValidateEndpoint { get; set; } = "/api/mock/uidai/validate";

  public string TokenizeEndpoint { get; set; } = "/api/mock/uidai/tokenize";
}
