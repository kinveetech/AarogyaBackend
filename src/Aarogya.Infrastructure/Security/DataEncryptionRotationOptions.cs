using System.ComponentModel.DataAnnotations;

namespace Aarogya.Infrastructure.Security;

public sealed class DataEncryptionRotationOptions
{
  public const string SectionName = "EncryptionRotation";

  public bool EnableBackgroundReEncryption { get; set; } = true;

  [Range(10, 10_080)]
  public int CheckIntervalMinutes { get; set; } = 1_440;

  [Range(50, 5_000)]
  public int BatchSize { get; set; } = 250;
}
