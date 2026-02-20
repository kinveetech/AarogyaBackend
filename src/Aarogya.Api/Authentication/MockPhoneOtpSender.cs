using Aarogya.Api.Features.V1.Notifications;

namespace Aarogya.Api.Authentication;

internal sealed class MockPhoneOtpSender(
  ISmsSender smsSender,
  ILogger<MockPhoneOtpSender> logger)
  : IPhoneOtpSender
{
  public async Task<OtpDispatchResult> SendOtpAsync(
    string phoneNumber,
    string otp,
    DateTimeOffset expiresAt,
    CancellationToken cancellationToken = default)
  {
    var message = $"Aarogya OTP {otp}. Expires at {expiresAt:O}.";
    var result = await smsSender.SendAsync(phoneNumber, message, "otp", cancellationToken);
    if (!result.Success)
    {
      var failureMessage = result.IsRateLimited
        ? "Too many OTP SMS requests. Please try again later."
        : "Unable to deliver OTP right now.";
      logger.LogWarning(
        "Mock OTP delivery failed. phone={PhoneNumber}, rateLimited={IsRateLimited}",
        phoneNumber,
        result.IsRateLimited);
      return new OtpDispatchResult(false, result.IsRateLimited, failureMessage);
    }

    logger.LogInformation(
      "Mock OTP delivery: phone={PhoneNumber}, otp={Otp}, expiresAtUtc={ExpiresAtUtc}, smsId={SmsMessageId}, estimatedCostInInr={EstimatedCostInInr}",
      phoneNumber,
      otp,
      expiresAt.UtcDateTime,
      result.MessageId,
      result.EstimatedCostInInr);

    return new OtpDispatchResult(true, false, "OTP sent successfully.");
  }
}
