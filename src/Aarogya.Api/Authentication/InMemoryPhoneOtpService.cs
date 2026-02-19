using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class InMemoryPhoneOtpService(
  IOptions<OtpOptions> options,
  IPhoneOtpSender otpSender,
  IUtcClock clock)
  : IPhoneOtpService
{
  private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(200);
  private static readonly Regex IndianPhoneRegex = new(
    @"^\+91[6-9]\d{9}$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant,
    RegexMatchTimeout);
  private readonly OtpOptions _options = options.Value;
  private readonly ConcurrentDictionary<string, OtpEntry> _entries = new(StringComparer.Ordinal);

  public async Task<OtpRequestResult> RequestOtpAsync(string phoneNumber, CancellationToken cancellationToken = default)
  {
    if (!TryNormalizeIndianPhone(phoneNumber, out var normalizedPhone))
    {
      return new OtpRequestResult(false, false, "Phone number must be a valid Indian mobile in +91 format.");
    }

    var now = clock.UtcNow;
    var entry = _entries.GetOrAdd(normalizedPhone, _ => new OtpEntry());

    lock (entry.Lock)
    {
      var windowStart = now.AddSeconds(-_options.RateLimitWindowSeconds);
      entry.RequestTimestamps.RemoveAll(timestamp => timestamp < windowStart);

      if (entry.RequestTimestamps.Count >= _options.MaxRequestsPerWindow)
      {
        return new OtpRequestResult(false, true, "Too many OTP requests. Please try again later.");
      }

      entry.RequestTimestamps.Add(now);
      entry.OtpCode = GenerateOtp(_options.CodeLength);
      entry.ExpiresAt = now.AddSeconds(_options.CodeExpirySeconds);
    }

    await otpSender.SendOtpAsync(normalizedPhone, entry.OtpCode!, entry.ExpiresAt!.Value, cancellationToken);

    return new OtpRequestResult(
      true,
      false,
      "OTP sent successfully (mocked delivery).",
      entry.ExpiresAt);
  }

  public Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
  {
    if (!TryNormalizeIndianPhone(phoneNumber, out var normalizedPhone))
    {
      return Task.FromResult(new OtpVerificationResult(false, "Phone number must be a valid Indian mobile in +91 format."));
    }

    if (string.IsNullOrWhiteSpace(otp))
    {
      return Task.FromResult(new OtpVerificationResult(false, "OTP is required."));
    }

    if (!_entries.TryGetValue(normalizedPhone, out var entry))
    {
      return Task.FromResult(new OtpVerificationResult(false, "OTP not found or expired."));
    }

    var now = clock.UtcNow;

    lock (entry.Lock)
    {
      if (string.IsNullOrWhiteSpace(entry.OtpCode) || entry.ExpiresAt is null)
      {
        return Task.FromResult(new OtpVerificationResult(false, "OTP not found or expired."));
      }

      if (entry.ExpiresAt.Value < now)
      {
        entry.OtpCode = null;
        entry.ExpiresAt = null;
        return Task.FromResult(new OtpVerificationResult(false, "OTP expired."));
      }

      if (!string.Equals(entry.OtpCode, otp.Trim(), StringComparison.Ordinal))
      {
        return Task.FromResult(new OtpVerificationResult(false, "Invalid OTP."));
      }

      entry.OtpCode = null;
      entry.ExpiresAt = null;
      return Task.FromResult(new OtpVerificationResult(true, "Phone number verified."));
    }
  }

  internal static bool TryNormalizeIndianPhone(string? phoneNumber, out string normalizedPhone)
  {
    normalizedPhone = string.Empty;
    if (string.IsNullOrWhiteSpace(phoneNumber))
    {
      return false;
    }

    var compact = phoneNumber.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
    if (!IndianPhoneRegex.IsMatch(compact))
    {
      return false;
    }

    normalizedPhone = compact;
    return true;
  }

  private static string GenerateOtp(int length)
  {
    var bytes = RandomNumberGenerator.GetBytes(length);
    var chars = new char[length];

    for (var i = 0; i < length; i++)
    {
      chars[i] = (char)('0' + (bytes[i] % 10));
    }

    return new string(chars);
  }

  private sealed class OtpEntry
  {
    public object Lock { get; } = new();

    public List<DateTimeOffset> RequestTimestamps { get; } = [];

    public string? OtpCode { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
  }
}
