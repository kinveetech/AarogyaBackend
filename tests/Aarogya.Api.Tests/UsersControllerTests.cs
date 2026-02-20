using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
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
    var service = new Mock<IUserProfileService>();
    var controller = CreateController(service.Object, new ClaimsPrincipal(new ClaimsIdentity()));

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
      ["Patient"]);

    var service = new Mock<IUserProfileService>();
    service.Setup(x => x.GetCurrentUserAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(response);

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.GetCurrentUserProfileAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task UpdateCurrentUserProfileAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var service = new Mock<IUserProfileService>();
    var controller = CreateController(service.Object, new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.UpdateCurrentUserProfileAsync(
      new UpdateUserProfileRequest("A", null, null, null, null, null, null),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task UpdateCurrentUserProfileAsync_ShouldReturnNotFound_WhenUserMissingAsync()
  {
    var service = new Mock<IUserProfileService>();
    service
      .Setup(x => x.UpdateCurrentUserAsync("seed-PATIENT-1", It.IsAny<UpdateUserProfileRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("Authenticated user is not provisioned in the database."));

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-1"));

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

    var service = new Mock<IUserProfileService>();
    service
      .Setup(x => x.VerifyCurrentUserAadhaarAsync("seed-PATIENT-1", It.IsAny<VerifyAadhaarRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(verification);

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.VerifyCurrentUserAadhaarAsync(new VerifyAadhaarRequest("123456789012"), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(verification);
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldReturnBadRequest_WhenVerificationFailsAsync()
  {
    var service = new Mock<IUserProfileService>();
    service
      .Setup(x => x.VerifyCurrentUserAadhaarAsync("seed-PATIENT-1", It.IsAny<VerifyAadhaarRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Aadhaar validation failed."));

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-1"));

    var result = await controller.VerifyCurrentUserAadhaarAsync(new VerifyAadhaarRequest("123456789012"), CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  private static UsersController CreateController(IUserProfileService service, ClaimsPrincipal user)
  {
    return new UsersController(service)
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
