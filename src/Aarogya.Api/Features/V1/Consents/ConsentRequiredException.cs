namespace Aarogya.Api.Features.V1.Consents;

internal sealed class ConsentRequiredException : InvalidOperationException
{
  public ConsentRequiredException()
  {
    Purpose = string.Empty;
  }

  public ConsentRequiredException(string purpose)
    : base($"Consent is required for purpose '{purpose}'.")
  {
    Purpose = purpose;
  }

  public ConsentRequiredException(string message, Exception innerException)
    : base(message, innerException)
  {
    Purpose = string.Empty;
  }

  public string Purpose { get; }
}
