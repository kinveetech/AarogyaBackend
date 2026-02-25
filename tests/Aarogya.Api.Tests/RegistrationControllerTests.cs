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

public sealed class RegistrationControllerTests
{
  #region RegisterAsync

  [Fact]
  public async Task RegisterAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateRegistrationController(
      new Mock<IUserRegistrationService>().Object,
      new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.RegisterAsync(
      new RegisterUserRequest("patient", "T", "P", "t@a.dev", null, null, null, null, null, null, null, null),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task RegisterAsync_ShouldReturnOk_WhenRegistrationSucceedsAsync()
  {
    var response = new RegisterUserResponse(
      "new-user", "Patient", "approved", "t@a.dev", "Test", "Patient",
      null, null, null, null, null, []);

    var registrationService = new Mock<IUserRegistrationService>();
    registrationService
      .Setup(x => x.RegisterAsync("new-user", It.IsAny<RegisterUserRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateRegistrationController(
      registrationService.Object, CreateUser("new-user"));

    var result = await controller.RegisterAsync(
      new RegisterUserRequest("patient", "Test", "Patient", "t@a.dev", null, null, null, null, null, null, null, null),
      CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task RegisterAsync_ShouldReturnBadRequest_WhenAlreadyRegisteredAsync()
  {
    var registrationService = new Mock<IUserRegistrationService>();
    registrationService
      .Setup(x => x.RegisterAsync("existing-user", It.IsAny<RegisterUserRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("User is already registered."));

    var controller = CreateRegistrationController(
      registrationService.Object, CreateUser("existing-user"));

    var result = await controller.RegisterAsync(
      new RegisterUserRequest("patient", "Test", "Patient", "t@a.dev", null, null, null, null, null, null, null, null),
      CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  #endregion

  #region GetRegistrationStatusAsync

  [Fact]
  public async Task GetRegistrationStatusAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateRegistrationController(
      new Mock<IUserRegistrationService>().Object,
      new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.GetRegistrationStatusAsync(CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task GetRegistrationStatusAsync_ShouldReturnOk_WithStatusAsync()
  {
    var statusResponse = new RegistrationStatusResponse("user-sub", "Patient", "approved", null);

    var registrationService = new Mock<IUserRegistrationService>();
    registrationService
      .Setup(x => x.GetRegistrationStatusAsync("user-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(statusResponse);

    var controller = CreateRegistrationController(
      registrationService.Object, CreateUser("user-sub"));

    var result = await controller.GetRegistrationStatusAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(statusResponse);
  }

  #endregion

  #region Helpers

  private static RegistrationController CreateRegistrationController(
    IUserRegistrationService registrationService,
    ClaimsPrincipal user)
  {
    return new RegistrationController(registrationService)
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext { User = user }
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

  #endregion
}
