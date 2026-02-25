using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UsersControllerTests
{
  [Fact]
  public async Task GetCurrentUserProfileAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(new Mock<IUserProfileService>().Object, new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.GetCurrentUserProfileAsync(CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task GetCurrentUserProfileAsync_ShouldReturnOk_WhenUserExistsAsync()
  {
    var response = new UserProfileResponse(
      "seed-PATIENT-1",
      "patient@aarogya.dev",
      "Test",
      "Patient",
      "+919876543210",
      "Pune",
      "O+",
      new DateOnly(1995, 6, 5),
      null,
      "approved",
      ["Patient"]);

    var userProfileService = new Mock<IUserProfileService>();
    userProfileService.Setup(x => x.GetCurrentUserAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(response);

    var controller = CreateController(userProfileService.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.GetCurrentUserProfileAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task UpdateCurrentUserProfileAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(new Mock<IUserProfileService>().Object, new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.UpdateCurrentUserProfileAsync(
      new UpdateUserProfileRequest("A", null, null, null, null, null, null),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task UpdateCurrentUserProfileAsync_ShouldReturnNotFound_WhenUserMissingAsync()
  {
    var userProfileService = new Mock<IUserProfileService>();
    userProfileService
      .Setup(x => x.UpdateCurrentUserAsync("seed-PATIENT-1", It.IsAny<UpdateUserProfileRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("Authenticated user is not provisioned in the database."));

    var controller = CreateController(userProfileService.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.UpdateCurrentUserProfileAsync(
      new UpdateUserProfileRequest("A", null, null, null, null, null, null),
      CancellationToken.None);

    var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
    notFound.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldReturnOkAsync()
  {
    var verification = new AadhaarVerificationResponse(
      Guid.NewGuid(),
      false,
      "LOCAL",
      new AadhaarDemographicsResponse("Verified Holder", null, null, "India"));

    var userProfileService = new Mock<IUserProfileService>();
    userProfileService
      .Setup(x => x.VerifyCurrentUserAadhaarAsync("seed-PATIENT-1", It.IsAny<VerifyAadhaarRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(verification);

    var controller = CreateController(userProfileService.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.VerifyCurrentUserAadhaarAsync(new VerifyAadhaarRequest("123456789012"), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(verification);
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldReturnBadRequest_WhenVerificationFailsAsync()
  {
    var userProfileService = new Mock<IUserProfileService>();
    userProfileService
      .Setup(x => x.VerifyCurrentUserAadhaarAsync("seed-PATIENT-1", It.IsAny<VerifyAadhaarRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Aadhaar validation failed."));

    var controller = CreateController(userProfileService.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.VerifyCurrentUserAadhaarAsync(new VerifyAadhaarRequest("123456789012"), CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task ExportCurrentUserDataAsync_ShouldReturnOkAsync()
  {
    var dataRightsService = new Mock<IUserDataRightsService>();
    dataRightsService
      .Setup(x => x.ExportCurrentUserDataAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DataExportResponse(
        DateTimeOffset.UtcNow,
        new UserProfileExportData(Guid.NewGuid(), "seed-PATIENT-1", "patient@aarogya.dev", "Test", "Patient", null, null, null, null, true, "Patient"),
        [],
        [],
        [],
        [],
        []));

    var controller = CreateController(
      new Mock<IUserProfileService>().Object,
      CreateUser("seed-PATIENT-1"),
      dataRightsService.Object);

    var result = await controller.ExportCurrentUserDataAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeOfType<DataExportResponse>();
  }

  [Fact]
  public async Task DeleteCurrentUserDataAsync_ShouldReturnBadRequest_WhenNotConfirmedAsync()
  {
    var dataRightsService = new Mock<IUserDataRightsService>();
    dataRightsService
      .Setup(x => x.DeleteCurrentUserDataAsync("seed-PATIENT-1", It.IsAny<DataDeletionRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("ConfirmPermanentDeletion must be true to proceed."));

    var controller = CreateController(
      new Mock<IUserProfileService>().Object,
      CreateUser("seed-PATIENT-1"),
      dataRightsService.Object);

    var result = await controller.DeleteCurrentUserDataAsync(
      new DataDeletionRequest(false, "test"),
      CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  private static UsersController CreateController(
    IUserProfileService userProfileService,
    ClaimsPrincipal user,
    IUserDataRightsService? userDataRightsService = null)
  {
    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.EnsureGrantedAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return new UsersController(
      userProfileService,
      userDataRightsService ?? Mock.Of<IUserDataRightsService>(),
      consentService.Object,
      Mock.Of<IUserRegistrationService>(),
      Mock.Of<IRegistrationApprovalService>())
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = user
        }
      }
    };
  }

  private static ClaimsPrincipal CreateUser(string sub)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", sub)
    ], "TestAuth"));
  }
}
