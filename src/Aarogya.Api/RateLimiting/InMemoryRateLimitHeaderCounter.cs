using System.Collections.Concurrent;

namespace Aarogya.Api.RateLimiting;

internal interface IRateLimitHeaderCounter
{
  public RateLimitHeaderSnapshot TrackAccepted(RateLimitDescriptor descriptor, DateTimeOffset now);
}

internal sealed class InMemoryRateLimitHeaderCounter : IRateLimitHeaderCounter
{
  private readonly ConcurrentDictionary<string, FixedWindowState> _fixedWindowStates = new(StringComparer.Ordinal);
  private readonly ConcurrentDictionary<string, SlidingWindowState> _slidingWindowStates = new(StringComparer.Ordinal);

  public RateLimitHeaderSnapshot TrackAccepted(RateLimitDescriptor descriptor, DateTimeOffset now)
  {
    return descriptor.IsSlidingWindow
      ? TrackSlidingWindow(descriptor, now)
      : TrackFixedWindow(descriptor, now);
  }

  private RateLimitHeaderSnapshot TrackFixedWindow(RateLimitDescriptor descriptor, DateTimeOffset now)
  {
    var state = _fixedWindowStates.GetOrAdd(
      BuildStorageKey(descriptor),
      _ => new FixedWindowState(now));

    lock (state.Gate)
    {
      if (now - state.WindowStart >= descriptor.Window)
      {
        state.WindowStart = now;
        state.Count = 0;
      }

      state.Count++;
      var remaining = Math.Max(0, descriptor.PermitLimit - state.Count);
      return new RateLimitHeaderSnapshot(
        descriptor.PermitLimit,
        remaining,
        state.WindowStart.Add(descriptor.Window));
    }
  }

  private RateLimitHeaderSnapshot TrackSlidingWindow(RateLimitDescriptor descriptor, DateTimeOffset now)
  {
    var state = _slidingWindowStates.GetOrAdd(
      BuildStorageKey(descriptor),
      _ => new SlidingWindowState());

    lock (state.Gate)
    {
      var cutoff = now - descriptor.Window;
      while (state.Hits.Count > 0 && state.Hits.Peek() <= cutoff)
      {
        state.Hits.Dequeue();
      }

      state.Hits.Enqueue(now);
      var remaining = Math.Max(0, descriptor.PermitLimit - state.Hits.Count);
      var resetAt = state.Hits.Count == 0
        ? now.Add(descriptor.Window)
        : state.Hits.Peek().Add(descriptor.Window);

      return new RateLimitHeaderSnapshot(
        descriptor.PermitLimit,
        remaining,
        resetAt);
    }
  }

  private static string BuildStorageKey(RateLimitDescriptor descriptor)
    => $"{descriptor.PolicyName}:{descriptor.PartitionKey}";

  private sealed class FixedWindowState
  {
    public FixedWindowState(DateTimeOffset windowStart)
    {
      WindowStart = windowStart;
    }

    public object Gate { get; } = new();
    public DateTimeOffset WindowStart { get; set; }
    public int Count { get; set; }
  }

  private sealed class SlidingWindowState
  {
    public object Gate { get; } = new();
    public Queue<DateTimeOffset> Hits { get; } = new();
  }
}
