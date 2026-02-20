using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class EntityCacheOptions
{
  public const string SectionName = "EntityCache";

  [Range(1, 86400)]
  public int UserProfileTtlSeconds { get; init; } = 300;

  [Range(1, 86400)]
  public int AccessGrantTtlSeconds { get; init; } = 120;

  [Range(1, 86400)]
  public int ReportListingTtlSeconds { get; init; } = 120;
}
