using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class EmailNotificationsOptions
{
  public const string SectionName = "EmailNotifications";

  public bool EnableTransactionalEmails { get; set; }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Configuration binding requires string type.")]
  [Required]
  [Url]
  [MaxLength(1024)]
  public string UnsubscribeBaseUrl { get; set; } = "https://aarogya.app/unsubscribe";
}
