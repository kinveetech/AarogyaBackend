using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AccessGrantsControllerTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

  // --- ListAccessGrantsAsync ---

  [Fact]
  public async Task ListAccessGrantsAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      accessGrantService: Mock.Of<IAccessGrantService>());

    var result = await controller.ListAccessGrantsAsync(CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task ListAccessGrantsAsync_ShouldReturnOk_WhenServiceReturnsGrantsAsync()
  {
    var grants = new List<AccessGrantResponse>
    {
      new(Guid.NewGuid(), "seed-PATIENT-1", "seed-DOCTOR-1", true, [], "care", FixedNow, FixedNow.AddDays(30), false)
    };

    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.GetForPatientAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(grants);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.ListAccessGrantsAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(grants);
  }

  [Fact]
  public async Task ListAccessGrantsAsync_ShouldReturnForbidden_WhenConsentNotGrantedAsync()
  {
    var consentService = CreateConsentRequiredService();
    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: Mock.Of<IAccessGrantService>(),
      consentService: consentService);

    var result = await controller.ListAccessGrantsAsync(CancellationToken.None);

    var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
    objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    objectResult.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  // --- ListReceivedAccessGrantsAsync ---

  [Fact]
  public async Task ListReceivedAccessGrantsAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      accessGrantService: Mock.Of<IAccessGrantService>());

    var result = await controller.ListReceivedAccessGrantsAsync(CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task ListReceivedAccessGrantsAsync_ShouldReturnOk_WhenDoctorHasConsentAsync()
  {
    var grants = new List<AccessGrantResponse>
    {
      new(Guid.NewGuid(), "seed-PATIENT-1", "seed-DOCTOR-1", true, [], "care", FixedNow, FixedNow.AddDays(30), false)
    };

    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.GetForDoctorAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(grants);

    var controller = CreateController(
      user: CreateUser("seed-DOCTOR-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.ListReceivedAccessGrantsAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(grants);
  }

  [Fact]
  public async Task ListReceivedAccessGrantsAsync_ShouldReturnForbidden_WhenConsentNotGrantedAsync()
  {
    var consentService = CreateConsentRequiredService();
    var controller = CreateController(
      user: CreateUser("seed-DOCTOR-1"),
      accessGrantService: Mock.Of<IAccessGrantService>(),
      consentService: consentService);

    var result = await controller.ListReceivedAccessGrantsAsync(CancellationToken.None);

    var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
    objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
  }

  // --- CreateAccessGrantAsync ---

  [Fact]
  public async Task CreateAccessGrantAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      accessGrantService: Mock.Of<IAccessGrantService>());

    var result = await controller.CreateAccessGrantAsync(
      new CreateAccessGrantRequest("seed-DOCTOR-1", true, null, "care", null),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task CreateAccessGrantAsync_ShouldReturnCreated_WhenSuccessfulAsync()
  {
    var grantId = Guid.NewGuid();
    var response = new AccessGrantResponse(
      grantId, "seed-PATIENT-1", "seed-DOCTOR-1", true, [], "care", FixedNow, FixedNow.AddDays(30), false);

    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.CreateAsync("seed-PATIENT-1", It.IsAny<CreateAccessGrantRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.CreateAccessGrantAsync(
      new CreateAccessGrantRequest("seed-DOCTOR-1", true, null, "care", null),
      CancellationToken.None);

    var created = result.Should().BeOfType<CreatedResult>().Subject;
    created.Value.Should().BeEquivalentTo(response);
    created.Location.Should().Contain(grantId.ToString());
  }

  [Fact]
  public async Task CreateAccessGrantAsync_ShouldReturnBadRequest_WhenServiceThrowsInvalidOperationAsync()
  {
    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<CreateAccessGrantRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Cannot grant access to self."));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.CreateAccessGrantAsync(
      new CreateAccessGrantRequest("seed-PATIENT-1", true, null, "self", null),
      CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task CreateAccessGrantAsync_ShouldReturnForbidden_WhenConsentNotGrantedAsync()
  {
    var consentService = CreateConsentRequiredService();
    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: Mock.Of<IAccessGrantService>(),
      consentService: consentService);

    var result = await controller.CreateAccessGrantAsync(
      new CreateAccessGrantRequest("seed-DOCTOR-1", true, null, "care", null),
      CancellationToken.None);

    var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
    objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
  }

  // --- RevokeAccessGrantAsync ---

  [Fact]
  public async Task RevokeAccessGrantAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      accessGrantService: Mock.Of<IAccessGrantService>());

    var result = await controller.RevokeAccessGrantAsync(Guid.NewGuid(), CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task RevokeAccessGrantAsync_ShouldReturnNoContent_WhenRevokedAsync()
  {
    var grantId = Guid.NewGuid();

    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.RevokeAsync("seed-PATIENT-1", grantId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.RevokeAccessGrantAsync(grantId, CancellationToken.None);

    result.Should().BeOfType<NoContentResult>();
  }

  [Fact]
  public async Task RevokeAccessGrantAsync_ShouldReturnNotFound_WhenGrantMissingAsync()
  {
    var accessGrantService = new Mock<IAccessGrantService>();
    accessGrantService
      .Setup(x => x.RevokeAsync("seed-PATIENT-1", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: accessGrantService.Object);

    var result = await controller.RevokeAccessGrantAsync(Guid.NewGuid(), CancellationToken.None);

    result.Should().BeOfType<NotFoundResult>();
  }

  [Fact]
  public async Task RevokeAccessGrantAsync_ShouldReturnForbidden_WhenConsentNotGrantedAsync()
  {
    var consentService = CreateConsentRequiredService();
    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      accessGrantService: Mock.Of<IAccessGrantService>(),
      consentService: consentService);

    var result = await controller.RevokeAccessGrantAsync(Guid.NewGuid(), CancellationToken.None);

    var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
    objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
  }

  // --- Helpers ---

  private static AccessGrantsController CreateController(
    ClaimsPrincipal user,
    IAccessGrantService accessGrantService,
    IConsentService? consentService = null)
  {
    consentService ??= CreatePassthroughConsentService();
    return new AccessGrantsController(accessGrantService, consentService)
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext { User = user }
      }
    };
  }

  private static IConsentService CreatePassthroughConsentService()
  {
    var mock = new Mock<IConsentService>();
    mock
      .Setup(x => x.EnsureGrantedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    return mock.Object;
  }

  private static IConsentService CreateConsentRequiredService()
  {
    var mock = new Mock<IConsentService>();
    mock
      .Setup(x => x.EnsureGrantedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ConsentRequiredException("medical_data_sharing"));
    return mock.Object;
  }

  private static ClaimsPrincipal CreateUser(string sub) =>
    new(new ClaimsIdentity([new Claim("sub", sub)], "TestAuth"));
}
