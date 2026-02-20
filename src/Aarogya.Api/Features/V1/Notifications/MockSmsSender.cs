using System.Collections.Concurrent;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class MockSmsSender(
  IOptions<SmsNotificationsOptions> options,
  IUtcClock clock,
  ILogger<MockSmsSender> logger)
  : ISmsSender
{
  private readonly SmsNotificationsOptions _options = options.Value;
  private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new(StringComparer.Ordinal);

  public Task<SmsSendResult> SendAsync(
    string phoneNumber,
    string message,
    string notificationType,
    CancellationToken cancellationToken = default)
  {
    if (!_options.EnableCriticalSms)
    {
      logger.LogInformation(
        "Mock SMS disabled by configuration. type={NotificationType}, phone={PhoneNumber}",
        notificationType,
        phoneNumber);
      return Task.FromResult(new SmsSendResult(true, false, 0m, $"mock-disabled-{Guid.NewGuid():N}"));
    }

    var now = clock.UtcNow;
    var key = $"{notificationType}:{phoneNumber}";
    var entry = _entries.GetOrAdd(key, _ => new RateLimitEntry());
    var windowStart = now.AddSeconds(-_options.RateLimitWindowSeconds);

    lock (entry.Lock)
    {
      entry.Timestamps.RemoveAll(timestamp => timestamp < windowStart);
      if (entry.Timestamps.Count >= _options.MaxSendsPerWindow)
      {
        logger.LogWarning(
          "Mock SMS rate limit hit. type={NotificationType}, phone={PhoneNumber}, max={MaxPerWindow}, windowSeconds={WindowSeconds}",
          notificationType,
          phoneNumber,
          _options.MaxSendsPerWindow,
          _options.RateLimitWindowSeconds);
        return Task.FromResult(new SmsSendResult(false, true, 0m, null));
      }

      entry.Timestamps.Add(now);
    }

    var messageId = $"mock-sms-{Guid.NewGuid():N}";
    var cost = _options.EstimatedCostPerMessageInInr;

    logger.LogInformation(
      "Mock SMS delivered. id={MessageId}, type={NotificationType}, phone={PhoneNumber}, estimatedCostInInr={EstimatedCostInInr}, body={Message}",
      messageId,
      notificationType,
      phoneNumber,
      cost,
      message);

    return Task.FromResult(new SmsSendResult(true, false, cost, messageId));
  }

  private sealed class RateLimitEntry
  {
    public object Lock { get; } = new();

    public List<DateTimeOffset> Timestamps { get; } = [];
  }
}
