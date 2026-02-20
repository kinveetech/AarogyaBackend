namespace Aarogya.Api.Features.V1.Notifications;

internal interface ISmsSender
{
  public Task<SmsSendResult> SendAsync(
    string phoneNumber,
    string message,
    string notificationType,
    CancellationToken cancellationToken = default);
}

internal sealed record SmsSendResult(
  bool Success,
  bool IsRateLimited,
  decimal EstimatedCostInInr,
  string? MessageId);
