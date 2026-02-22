using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;

namespace Aarogya.Domain.Enums;

public static class EnumUtils
{
  public static string ToDescription<TEnum>(TEnum value) where TEnum : struct, Enum
    => Cache<TEnum>.ToDescriptionMap.GetValueOrDefault(value)
      ?? throw new InvalidOperationException(
        $"Enum value '{value}' of type {typeof(TEnum).Name} does not have a [Description] attribute.");

  public static TEnum FromDescription<TEnum>(string description) where TEnum : struct, Enum
    => Cache<TEnum>.FromDescriptionMap.TryGetValue(description, out var result)
      ? result
      : throw new InvalidOperationException(
        $"No enum value of type {typeof(TEnum).Name} has [Description(\"{description}\")].");

  private static class Cache<TEnum> where TEnum : struct, Enum
  {
    public static readonly FrozenDictionary<TEnum, string> ToDescriptionMap = BuildToDescriptionMap();
    public static readonly FrozenDictionary<string, TEnum> FromDescriptionMap = BuildFromDescriptionMap();

    private static FrozenDictionary<TEnum, string> BuildToDescriptionMap()
    {
      var values = Enum.GetValues<TEnum>();
      var dict = new Dictionary<TEnum, string>(values.Length);
      foreach (var value in values)
      {
        var field = typeof(TEnum).GetField(value.ToString())!;
        var attr = field.GetCustomAttribute<DescriptionAttribute>()
          ?? throw new InvalidOperationException(
            $"Enum value '{value}' of type {typeof(TEnum).Name} is missing a [Description] attribute.");
        dict[value] = attr.Description;
      }

      return dict.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, TEnum> BuildFromDescriptionMap()
    {
      var dict = new Dictionary<string, TEnum>(ToDescriptionMap.Count, StringComparer.OrdinalIgnoreCase);
      foreach (var (enumValue, description) in ToDescriptionMap)
      {
        dict[description] = enumValue;
      }

      return dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
  }
}
