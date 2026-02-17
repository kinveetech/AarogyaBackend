using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class DatabaseOptions
{
  public const string SectionName = "Database";

  /// <summary>
  /// Maximum number of seconds to wait for a command to execute.
  /// </summary>
  [Range(1, 300)]
  public int CommandTimeoutSeconds { get; set; } = 30;

  /// <summary>
  /// Whether to enable EF Core detailed error messages (logged SQL, parameters, etc.).
  /// Should only be true in Development.
  /// </summary>
  public bool EnableDetailedErrors { get; set; }

  /// <summary>
  /// Whether to enable EF Core sensitive data logging (parameter values in logs).
  /// Should only be true in Development.
  /// </summary>
  public bool EnableSensitiveDataLogging { get; set; }

  /// <summary>
  /// Maximum number of retry attempts for transient database failures.
  /// </summary>
  [Range(0, 10)]
  public int MaxRetryCount { get; set; } = 3;

  /// <summary>
  /// Maximum delay in seconds between retry attempts.
  /// </summary>
  [Range(1, 60)]
  public int MaxRetryDelaySeconds { get; set; } = 5;

  /// <summary>
  /// Whether to automatically apply pending migrations on startup.
  /// Should only be true in Development.
  /// </summary>
  public bool AutoMigrateOnStartup { get; set; }
}
