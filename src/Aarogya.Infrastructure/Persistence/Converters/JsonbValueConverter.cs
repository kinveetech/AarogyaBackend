using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aarogya.Infrastructure.Persistence.Converters;

internal static class JsonbValueConverter
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  public static ValueConverter<TValue, string> CreateConverter<TValue>()
    where TValue : class, new()
    => new(
      v => JsonSerializer.Serialize(v, SerializerOptions),
      v => string.IsNullOrWhiteSpace(v)
        ? new TValue()
        : JsonSerializer.Deserialize<TValue>(v, SerializerOptions) ?? new TValue());

  public static ValueComparer<TValue> CreateComparer<TValue>()
    where TValue : class, new()
    => new(
      (l, r) => JsonSerializer.Serialize(l, SerializerOptions) == JsonSerializer.Serialize(r, SerializerOptions),
      v => JsonSerializer.Serialize(v, SerializerOptions).GetHashCode(StringComparison.Ordinal),
      v => JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(v, SerializerOptions), SerializerOptions) ?? new TValue());
}
