namespace Aarogya.Api.RateLimiting;

internal sealed record RateLimitDescriptor(
  string PolicyName,
  string PartitionKey,
  int PermitLimit,
  TimeSpan Window,
  bool IsSlidingWindow,
  int SegmentsPerWindow)
{
  public const string HttpContextItemKey = "__aarogya_rate_limit_descriptor";
}

internal sealed record RateLimitHeaderSnapshot(
  int Limit,
  int Remaining,
  DateTimeOffset ResetAt);
