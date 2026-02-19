using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aarogya.Infrastructure.Persistence.Converters;

internal static class EnumSnakeCaseConverter
{
  public static ValueConverter<TEnum, string> Create<TEnum>()
    where TEnum : struct, Enum
    => new(
      value => ToSnakeCase(value.ToString()),
      value => ParseSnakeCase<TEnum>(value));

  private static TEnum ParseSnakeCase<TEnum>(string? dbValue)
    where TEnum : struct, Enum
  {
    if (string.IsNullOrWhiteSpace(dbValue))
    {
      throw new InvalidOperationException($"Cannot map empty value to enum {typeof(TEnum).Name}.");
    }

    var enumName = Enum.GetNames<TEnum>()
      .FirstOrDefault(name => string.Equals(ToSnakeCase(name), dbValue, StringComparison.OrdinalIgnoreCase));

    if (enumName is null)
    {
      throw new InvalidOperationException($"Unsupported enum value '{dbValue}' for {typeof(TEnum).Name}.");
    }

    return Enum.Parse<TEnum>(enumName, ignoreCase: true);
  }

  private static string ToSnakeCase(string value)
  {
    var builder = new StringBuilder();
    for (var i = 0; i < value.Length; i++)
    {
      var ch = value[i];
      if (char.IsUpper(ch) && i > 0)
      {
        builder.Append('_');
      }

      builder.Append(char.ToLowerInvariant(ch));
    }

    return builder.ToString();
  }
}
