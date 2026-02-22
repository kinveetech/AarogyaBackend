using Aarogya.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Enums;

public sealed class EnumUtilsTests
{
  [Theory]
  [InlineData(UserRole.Patient, "patient")]
  [InlineData(UserRole.Doctor, "doctor")]
  [InlineData(UserRole.LabTechnician, "lab_technician")]
  [InlineData(UserRole.Admin, "admin")]
  public void ToDescription_ShouldReturnCorrectDescription_ForAllUserRoles(
    UserRole value, string expected)
  {
    EnumUtils.ToDescription(value).Should().Be(expected);
  }

  [Theory]
  [InlineData(ReportType.BloodTest, "blood_test")]
  [InlineData(ReportType.UrineTest, "urine_test")]
  [InlineData(ReportType.Radiology, "radiology")]
  [InlineData(ReportType.Cardiology, "cardiology")]
  [InlineData(ReportType.Other, "other")]
  public void ToDescription_ShouldReturnCorrectDescription_ForAllReportTypes(
    ReportType value, string expected)
  {
    EnumUtils.ToDescription(value).Should().Be(expected);
  }

  [Theory]
  [InlineData(ReportStatus.Draft, "draft")]
  [InlineData(ReportStatus.Uploaded, "uploaded")]
  [InlineData(ReportStatus.Processing, "processing")]
  [InlineData(ReportStatus.Clean, "clean")]
  [InlineData(ReportStatus.Infected, "infected")]
  [InlineData(ReportStatus.Validated, "validated")]
  [InlineData(ReportStatus.Published, "published")]
  [InlineData(ReportStatus.Archived, "archived")]
  public void ToDescription_ShouldReturnCorrectDescription_ForAllReportStatuses(
    ReportStatus value, string expected)
  {
    EnumUtils.ToDescription(value).Should().Be(expected);
  }

  [Theory]
  [InlineData(AccessGrantStatus.Active, "active")]
  [InlineData(AccessGrantStatus.Revoked, "revoked")]
  [InlineData(AccessGrantStatus.Expired, "expired")]
  public void ToDescription_ShouldReturnCorrectDescription_ForAllAccessGrantStatuses(
    AccessGrantStatus value, string expected)
  {
    EnumUtils.ToDescription(value).Should().Be(expected);
  }

  [Fact]
  public void FromDescription_ShouldRoundTrip_ForAllEnumValues()
  {
    foreach (var role in Enum.GetValues<UserRole>())
    {
      var description = EnumUtils.ToDescription(role);
      EnumUtils.FromDescription<UserRole>(description).Should().Be(role);
    }

    foreach (var type in Enum.GetValues<ReportType>())
    {
      var description = EnumUtils.ToDescription(type);
      EnumUtils.FromDescription<ReportType>(description).Should().Be(type);
    }

    foreach (var status in Enum.GetValues<ReportStatus>())
    {
      var description = EnumUtils.ToDescription(status);
      EnumUtils.FromDescription<ReportStatus>(description).Should().Be(status);
    }

    foreach (var status in Enum.GetValues<AccessGrantStatus>())
    {
      var description = EnumUtils.ToDescription(status);
      EnumUtils.FromDescription<AccessGrantStatus>(description).Should().Be(status);
    }
  }

  [Fact]
  public void FromDescription_ShouldBeCaseInsensitive()
  {
    EnumUtils.FromDescription<UserRole>("PATIENT").Should().Be(UserRole.Patient);
    EnumUtils.FromDescription<UserRole>("Patient").Should().Be(UserRole.Patient);
    EnumUtils.FromDescription<UserRole>("pAtIeNt").Should().Be(UserRole.Patient);
  }

  [Fact]
  public void FromDescription_ShouldThrow_ForUnknownDescription()
  {
    var action = () => EnumUtils.FromDescription<UserRole>("nonexistent");

    action.Should().Throw<InvalidOperationException>();
  }
}
