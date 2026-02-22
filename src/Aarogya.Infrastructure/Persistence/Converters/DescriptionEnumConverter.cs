using Aarogya.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aarogya.Infrastructure.Persistence.Converters;

internal static class DescriptionEnumConverter
{
  public static ValueConverter<TEnum, string> Create<TEnum>() where TEnum : struct, Enum
    => new(
      v => EnumUtils.ToDescription(v),
      v => EnumUtils.FromDescription<TEnum>(v));
}
