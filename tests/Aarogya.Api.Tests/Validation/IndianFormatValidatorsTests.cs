using Aarogya.Api.Validation;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Aarogya.Api.Tests.Validation;

public sealed class IndianFormatValidatorsTests
{
  #region Phone number validation

  private sealed record PhoneModel(string? Phone);

  private sealed class PhoneValidator : AbstractValidator<PhoneModel>
  {
    public PhoneValidator()
    {
      RuleFor(x => x.Phone).MustBeIndianPhoneNumber();
    }
  }

  [Theory]
  [InlineData("+919876543210")]
  [InlineData("+916000000000")]
  public void MustBeIndianPhoneNumber_ShouldAccept_ValidIndianPhones(string phone)
  {
    var validator = new PhoneValidator();
    var result = validator.Validate(new PhoneModel(phone));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("9876543210", "missing +91 prefix")]
  [InlineData("+910876543210", "starts with 0 after +91")]
  [InlineData("+911876543210", "starts with 1 after +91")]
  [InlineData("+915876543210", "starts with 5 after +91")]
  [InlineData("+91123", "too short")]
  [InlineData(null, "null")]
  [InlineData("", "empty string")]
  [InlineData("+1234567890", "non-Indian country code")]
  [InlineData("+9198765432100", "too long")]
  public void MustBeIndianPhoneNumber_ShouldReject_InvalidPhones(string? phone, string reason)
  {
    _ = reason;
    var validator = new PhoneValidator();
    var result = validator.Validate(new PhoneModel(phone));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region Aadhaar number validation

  private sealed record AadhaarModel(string Aadhaar);

  private sealed class AadhaarValidator : AbstractValidator<AadhaarModel>
  {
    public AadhaarValidator()
    {
      RuleFor(x => x.Aadhaar).MustBeAadhaarNumber();
    }
  }

  [Theory]
  [InlineData("234567890123")]
  [InlineData("999999999999")]
  [InlineData("500000000000")]
  public void MustBeAadhaarNumber_ShouldAccept_ValidAadhaarNumbers(string aadhaar)
  {
    var validator = new AadhaarValidator();
    var result = validator.Validate(new AadhaarModel(aadhaar));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("123456789012", "starts with 1")]
  [InlineData("023456789012", "starts with 0")]
  [InlineData("12345678901", "11 digits")]
  [InlineData("1234567890123", "13 digits")]
  [InlineData("", "empty string")]
  [InlineData("abcdefghijkl", "non-numeric")]
  public void MustBeAadhaarNumber_ShouldReject_InvalidAadhaarNumbers(string aadhaar, string reason)
  {
    _ = reason;
    var validator = new AadhaarValidator();
    var result = validator.Validate(new AadhaarModel(aadhaar));
    result.IsValid.Should().BeFalse();
  }

  #endregion
}
