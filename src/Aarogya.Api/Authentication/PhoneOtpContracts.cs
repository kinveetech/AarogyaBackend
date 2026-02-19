using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Authentication;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IPhoneOtpService
{
  public Task<OtpRequestResult> RequestOtpAsync(string phoneNumber, CancellationToken cancellationToken = default);

  public Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}

internal interface IPhoneOtpSender
{
  public Task SendOtpAsync(string phoneNumber, string otp, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}

internal interface IUtcClock
{
  public DateTimeOffset UtcNow { get; }
}

internal sealed class SystemUtcClock : IUtcClock
{
  public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by a public service contract used by API controller.")]
public sealed record OtpRequestResult(
  bool Success,
  bool IsRateLimited,
  string Message,
  DateTimeOffset? ExpiresAt = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by a public service contract used by API controller.")]
public sealed record OtpVerificationResult(
  bool Success,
  string Message);
