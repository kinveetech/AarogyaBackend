using System.Text.RegularExpressions;
using FluentValidation;

namespace Aarogya.Api.Validation;

internal static partial class IndianFormatValidators
{
  [GeneratedRegex(@"^\+91[6-9]\d{9}$", RegexOptions.CultureInvariant, 200)]
  private static partial Regex IndianPhoneRegex();

  [GeneratedRegex(@"^[2-9]\d{11}$", RegexOptions.CultureInvariant, 200)]
  private static partial Regex AadhaarRegex();

  public static IRuleBuilderOptions<T, string> MustBeIndianPhoneNumber<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder.Must(value => !string.IsNullOrWhiteSpace(value) && IndianPhoneRegex().IsMatch(value.Trim()))
      .WithMessage("Phone number must be in Indian E.164 format (+91XXXXXXXXXX).");
  }

  public static IRuleBuilderOptions<T, string> MustBeAadhaarNumber<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder.Must(value => !string.IsNullOrWhiteSpace(value) && AadhaarRegex().IsMatch(value.Trim()))
      .WithMessage("Aadhaar must be a 12-digit number that does not start with 0 or 1.");
  }
}
