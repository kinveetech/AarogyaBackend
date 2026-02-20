using System.Net;
using System.Text.RegularExpressions;

namespace Aarogya.Api.Security;

internal static class InputSanitizer
{
  private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
  private static readonly Regex ControlCharacterRegex = new("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F\\u007F]+", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
  private static readonly Regex MultiWhitespaceRegex = new("\\s{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

  public static string SanitizePlainText(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    var normalized = WebUtility.HtmlDecode(value).Trim();
    normalized = HtmlTagRegex.Replace(normalized, string.Empty);
    normalized = ControlCharacterRegex.Replace(normalized, " ");
    normalized = MultiWhitespaceRegex.Replace(normalized, " ").Trim();

    return normalized;
  }

  public static string? SanitizeNullablePlainText(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var sanitized = SanitizePlainText(value);
    return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
  }

  public static Dictionary<string, string> SanitizeStringDictionary(IReadOnlyDictionary<string, string>? value)
  {
    if (value is null || value.Count == 0)
    {
      return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var pair in value)
    {
      var sanitizedKey = SanitizePlainText(pair.Key);
      if (string.IsNullOrWhiteSpace(sanitizedKey))
      {
        continue;
      }

      sanitized[sanitizedKey] = SanitizePlainText(pair.Value);
    }

    return sanitized;
  }
}
