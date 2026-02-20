using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class FirebaseMessagingOptions
{
  public const string SectionName = "FirebaseMessaging";

  public bool EnableSending { get; set; }

  [Required]
  [MaxLength(2048)]
  public string Endpoint { get; set; } = "https://fcm.googleapis.com/fcm/send";

  [MaxLength(4096)]
  public string? ServerKey { get; set; }
}
