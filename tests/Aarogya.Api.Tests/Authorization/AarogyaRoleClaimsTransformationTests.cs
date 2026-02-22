using System.Security.Claims;
using Aarogya.Api.Authorization;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Authorization;

public sealed class AarogyaRoleClaimsTransformationTests
{
  private static readonly string[] DoctorRole = ["Doctor"];
  private static readonly string[] PatientRole = ["Patient"];

  private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();
  private readonly AarogyaRoleClaimsTransformation _transformation;

  public AarogyaRoleClaimsTransformationTests()
  {
    _roleAssignmentServiceMock
      .Setup(s => s.GetAssignedRoles(It.IsAny<string>()))
      .Returns(Array.Empty<string>());

    _transformation = new AarogyaRoleClaimsTransformation(_roleAssignmentServiceMock.Object);
  }

  [Fact]
  public async Task TransformAsync_ShouldNotModify_UnauthenticatedPrincipalAsync()
  {
    var identity = new ClaimsIdentity();
    var principal = new ClaimsPrincipal(identity);

    var result = await _transformation.TransformAsync(principal);

    result.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
  }

  [Fact]
  public async Task TransformAsync_ShouldAddRoleClaims_FromCognitoGroupsAsync()
  {
    var identity = new ClaimsIdentity(
      [
        new Claim("sub", "user-1"),
        new Claim("cognito:groups", "Patient")
      ],
      "Bearer");
    var principal = new ClaimsPrincipal(identity);

    var result = await _transformation.TransformAsync(principal);

    var roleClaims = result.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
    roleClaims.Should().Contain("Patient");
  }

  [Fact]
  public async Task TransformAsync_ShouldFilterUnsupportedCognitoGroupsAsync()
  {
    var identity = new ClaimsIdentity(
      [
        new Claim("sub", "user-1"),
        new Claim("cognito:groups", "SomeRandomGroup")
      ],
      "Bearer");
    var principal = new ClaimsPrincipal(identity);

    var result = await _transformation.TransformAsync(principal);

    var roleClaims = result.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
    roleClaims.Should().NotContain("SomeRandomGroup");
  }

  [Fact]
  public async Task TransformAsync_ShouldMergeDynamicRolesAsync()
  {
    var identity = new ClaimsIdentity(
      [
        new Claim("sub", "user-1"),
        new Claim("cognito:groups", "Patient")
      ],
      "Bearer");
    var principal = new ClaimsPrincipal(identity);

    _roleAssignmentServiceMock
      .Setup(s => s.GetAssignedRoles("user-1"))
      .Returns(DoctorRole);

    var result = await _transformation.TransformAsync(principal);

    var roleClaims = result.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
    roleClaims.Should().Contain("Patient");
    roleClaims.Should().Contain("Doctor");
  }

  [Fact]
  public async Task TransformAsync_ShouldAddImplicitRoles_ForAdminAsync()
  {
    var identity = new ClaimsIdentity(
      [
        new Claim("sub", "admin-1"),
        new Claim("cognito:groups", "Admin")
      ],
      "Bearer");
    var principal = new ClaimsPrincipal(identity);

    var result = await _transformation.TransformAsync(principal);

    var roleClaims = result.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
    roleClaims.Should().Contain("Admin");
    roleClaims.Should().Contain("Patient");
    roleClaims.Should().Contain("Doctor");
    roleClaims.Should().Contain("LabTechnician");
  }

  [Fact]
  public async Task TransformAsync_ShouldDeduplicateRolesAsync()
  {
    var identity = new ClaimsIdentity(
      [
        new Claim("sub", "user-1"),
        new Claim("cognito:groups", "Patient")
      ],
      "Bearer");
    var principal = new ClaimsPrincipal(identity);

    _roleAssignmentServiceMock
      .Setup(s => s.GetAssignedRoles("user-1"))
      .Returns(PatientRole);

    var result = await _transformation.TransformAsync(principal);

    var patientRoleClaims = result.Claims
      .Where(c => c.Type == ClaimTypes.Role && c.Value == "Patient")
      .ToList();
    patientRoleClaims.Should().HaveCount(1);
  }
}
