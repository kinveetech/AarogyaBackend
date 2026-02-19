using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Aarogya.Infrastructure.Security;

public sealed class BlindIndexService : IBlindIndexService
{
  private static readonly byte[] DevelopmentFallbackKey = SHA256.HashData(Encoding.UTF8.GetBytes("aarogya-dev-blind-index"));

  private readonly byte[] _hmacKey;

  public BlindIndexService(IOptions<EncryptionOptions> options)
  {
    ArgumentNullException.ThrowIfNull(options);

    _hmacKey = DeriveKey(options.Value.BlindIndexKey);
  }

  public byte[]? Compute(string? value, string scope, bool normalize = true)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(scope))
    {
      throw new ArgumentException("Blind-index scope is required.", nameof(scope));
    }

    var normalizedValue = normalize
      ? value.Trim().ToUpperInvariant()
      : value.Trim();

    var scopedPayload = Encoding.UTF8.GetBytes($"{scope}:{normalizedValue}");

    using var hmac = new HMACSHA256(_hmacKey);
    return hmac.ComputeHash(scopedPayload);
  }

  private static byte[] DeriveKey(string? source)
  {
    if (string.IsNullOrWhiteSpace(source)
      || source == "SET_VIA_USER_SECRETS_OR_ENV_VAR")
    {
      return DevelopmentFallbackKey;
    }

    return SHA256.HashData(Encoding.UTF8.GetBytes(source));
  }
}
