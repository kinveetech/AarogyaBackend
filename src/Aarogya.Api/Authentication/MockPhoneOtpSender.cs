namespace Aarogya.Api.Authentication;

internal sealed class MockPhoneOtpSender(ILogger<MockPhoneOtpSender> logger) : IPhoneOtpSender
{
  public Task SendOtpAsync(string phoneNumber, string otp, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
  {
    logger.LogInformation(
      "Mock OTP delivery: phone={PhoneNumber}, otp={Otp}, expiresAtUtc={ExpiresAtUtc}",
      phoneNumber,
      otp,
      expiresAt.UtcDateTime);

    return Task.CompletedTask;
  }
}
