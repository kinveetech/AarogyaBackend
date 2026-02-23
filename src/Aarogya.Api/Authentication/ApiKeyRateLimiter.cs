using System.Collections.Concurrent;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class ApiKeyRateLimiter(IOptions<ApiKeyOptions> options, IUtcClock clock)
{
  private readonly ApiKeyOptions _options = options.Value;
  private readonly ConcurrentDictionary<string, RateState> _rateByKeyId = new(StringComparer.Ordinal);

  public bool IsRateLimited(string keyId)
  {
    var now = clock.UtcNow;
    var state = _rateByKeyId.GetOrAdd(keyId, _ => new RateState());
    lock (state.Sync)
    {
      var windowStart = now.AddSeconds(-_options.RateLimitWindowSeconds);
      state.Requests = state.Requests
        .Where(timestamp => timestamp >= windowStart)
        .ToList();

      if (state.Requests.Count >= _options.MaxRequestsPerWindow)
      {
        return true;
      }

      state.Requests.Add(now);
      return false;
    }
  }

  private sealed class RateState
  {
    public object Sync { get; } = new();

    public List<DateTimeOffset> Requests { get; set; } = [];
  }
}
