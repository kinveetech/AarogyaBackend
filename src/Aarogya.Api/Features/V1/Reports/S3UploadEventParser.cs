using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Aarogya.Api.Features.V1.Reports;

internal static class S3UploadEventParser
{
  public static IReadOnlyList<S3UploadEventRecord> ParseRecords(string messageBody)
  {
    if (string.IsNullOrWhiteSpace(messageBody))
    {
      return [];
    }

    using var document = JsonDocument.Parse(messageBody);
    if (!document.RootElement.TryGetProperty("Records", out var recordsElement)
      || recordsElement.ValueKind != JsonValueKind.Array)
    {
      return [];
    }

    var records = new List<S3UploadEventRecord>();
    foreach (var recordElement in recordsElement.EnumerateArray())
    {
      if (!TryReadString(recordElement, out var eventName, "eventName")
        || !TryReadString(recordElement, out var eventTimeRaw, "eventTime")
        || !TryReadString(recordElement, out var bucketName, "s3", "bucket", "name")
        || !TryReadString(recordElement, out var objectKeyRaw, "s3", "object", "key"))
      {
        continue;
      }

      if (!DateTimeOffset.TryParse(
        eventTimeRaw,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out var eventTime))
      {
        continue;
      }

      long? size = null;
      if (TryReadInt64(recordElement, out var parsedSize, "s3", "object", "size"))
      {
        size = parsedSize;
      }

      var decodedObjectKey = WebUtility.UrlDecode(objectKeyRaw.Replace('+', ' '));
      records.Add(new S3UploadEventRecord(bucketName, decodedObjectKey, size, eventName, eventTime));
    }

    return records;
  }

  private static bool TryReadString(JsonElement root, out string value, params string[] path)
  {
    value = string.Empty;

    if (!TryNavigate(root, out var element, path) || element.ValueKind != JsonValueKind.String)
    {
      return false;
    }

    value = element.GetString() ?? string.Empty;
    return !string.IsNullOrWhiteSpace(value);
  }

  private static bool TryReadInt64(JsonElement root, out long value, params string[] path)
  {
    value = default;
    if (!TryNavigate(root, out var element, path))
    {
      return false;
    }

    if (element.ValueKind == JsonValueKind.Number)
    {
      return element.TryGetInt64(out value);
    }

    if (element.ValueKind == JsonValueKind.String)
    {
      return long.TryParse(element.GetString(), out value);
    }

    return false;
  }

  private static bool TryNavigate(JsonElement root, out JsonElement element, params string[] path)
  {
    element = root;
    foreach (var propertyName in path)
    {
      if (element.ValueKind != JsonValueKind.Object
        || !element.TryGetProperty(propertyName, out element))
      {
        return false;
      }
    }

    return true;
  }
}

internal sealed record S3UploadEventRecord(
  string BucketName,
  string ObjectKey,
  long? SizeBytes,
  string EventName,
  DateTimeOffset EventTime);
