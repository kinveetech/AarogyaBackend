using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ConsentsControllerTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

  // --- GetCurrentUserConsentsAsync ---

  [Fact]
  public async Task GetCurrentUserConsentsAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      consentService: Mock.Of<IConsentService>());

    var result = await controller.GetCurrentUserConsentsAsync(CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task GetCurrentUserConsentsAsync_ShouldReturnOk_WhenServiceReturnsConsentsAsync()
  {
    var consents = new List<ConsentRecordResponse>
    {
      new("medical_data_sharing", true, FixedNow, "api"),
      new("profile_management", true, FixedNow.AddDays(-1), "api")
    };

    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.GetCurrentForUserAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(consents);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      consentService: consentService.Object);

    var result = await controller.GetCurrentUserConsentsAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(consents);
  }

  [Fact]
  public async Task GetCurrentUserConsentsAsync_ShouldReturnOk_WhenNoConsentsExistAsync()
  {
    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.GetCurrentForUserAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new List<ConsentRecordResponse>());

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      consentService: consentService.Object);

    var result = await controller.GetCurrentUserConsentsAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<ConsentRecordResponse>>().Subject;
    items.Should().BeEmpty();
  }

  // --- UpsertConsentAsync ---

  [Fact]
  public async Task UpsertConsentAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      consentService: Mock.Of<IConsentService>());

    var result = await controller.UpsertConsentAsync(
      "medical_data_sharing",
      new UpsertConsentRequest(true),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task UpsertConsentAsync_ShouldReturnOk_WhenSuccessfulAsync()
  {
    var response = new ConsentRecordResponse("medical_data_sharing", true, FixedNow, "api");

    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.UpsertForUserAsync(
        "seed-PATIENT-1",
        "medical_data_sharing",
        It.IsAny<UpsertConsentRequest>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      consentService: consentService.Object);

    var result = await controller.UpsertConsentAsync(
      "medical_data_sharing",
      new UpsertConsentRequest(true),
      CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task UpsertConsentAsync_ShouldReturnBadRequest_WhenServiceThrowsInvalidOperationAsync()
  {
    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.UpsertForUserAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<UpsertConsentRequest>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Unsupported consent purpose."));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      consentService: consentService.Object);

    var result = await controller.UpsertConsentAsync(
      "unknown_purpose",
      new UpsertConsentRequest(true),
      CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task UpsertConsentAsync_ShouldPassPurposeFromRoute_ToServiceAsync()
  {
    string? capturedPurpose = null;

    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.UpsertForUserAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<UpsertConsentRequest>(),
        It.IsAny<CancellationToken>()))
      .Callback<string, string, UpsertConsentRequest, CancellationToken>((_, purpose, _, _) => capturedPurpose = purpose)
      .ReturnsAsync(new ConsentRecordResponse("profile_management", true, FixedNow, "api"));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      consentService: consentService.Object);

    await controller.UpsertConsentAsync(
      "profile_management",
      new UpsertConsentRequest(true),
      CancellationToken.None);

    capturedPurpose.Should().Be("profile_management");
  }

  // --- Helpers ---

  private static ConsentsController CreateController(
    ClaimsPrincipal user,
    IConsentService consentService)
  {
    return new ConsentsController(consentService)
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext { User = user }
      }
    };
  }

  private static ClaimsPrincipal CreateUser(string sub) =>
    new(new ClaimsIdentity([new Claim("sub", sub)], "TestAuth"));
}
