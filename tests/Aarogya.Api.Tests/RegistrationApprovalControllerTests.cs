using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class RegistrationApprovalControllerTests
{
  #region ListPendingRegistrationsAsync

  [Fact]
  public async Task ListPendingRegistrationsAsync_ShouldReturnOkAsync()
  {
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.ListPendingAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(new List<PendingRegistrationResponse>
      {
        new("pending-doc", "Doctor", "Test", "Doctor", "doc@aarogya.dev",
          DateTimeOffset.UtcNow,
          new DoctorRegistrationData("MED-001", "Cardiology", null, null),
          null)
      });

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.ListPendingRegistrationsAsync(CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<PendingRegistrationResponse>>().Subject;
    items.Should().HaveCount(1);
  }

  #endregion

  #region ApproveRegistrationAsync

  [Fact]
  public async Task ApproveRegistrationAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      new Mock<IRegistrationApprovalService>().Object,
      new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.ApproveRegistrationAsync(
      "target-sub", new ApproveRegistrationRequest(null), CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task ApproveRegistrationAsync_ShouldReturnOk_WhenApprovedAsync()
  {
    var response = new RegistrationStatusResponse("target-sub", "Doctor", "approved", null);
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.ApproveAsync("admin-sub", "target-sub", It.IsAny<ApproveRegistrationRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.ApproveRegistrationAsync(
      "target-sub", new ApproveRegistrationRequest(null), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task ApproveRegistrationAsync_ShouldReturnNotFound_WhenUserMissingAsync()
  {
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.ApproveAsync("admin-sub", "missing-user", It.IsAny<ApproveRegistrationRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("User not found."));

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.ApproveRegistrationAsync(
      "missing-user", new ApproveRegistrationRequest(null), CancellationToken.None);

    result.Should().BeOfType<NotFoundObjectResult>();
  }

  [Fact]
  public async Task ApproveRegistrationAsync_ShouldReturnBadRequest_WhenNotPendingAsync()
  {
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.ApproveAsync("admin-sub", "target-sub", It.IsAny<ApproveRegistrationRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Cannot approve."));

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.ApproveRegistrationAsync(
      "target-sub", new ApproveRegistrationRequest(null), CancellationToken.None);

    result.Should().BeOfType<BadRequestObjectResult>();
  }

  #endregion

  #region RejectRegistrationAsync

  [Fact]
  public async Task RejectRegistrationAsync_ShouldReturnOk_WhenRejectedAsync()
  {
    var response = new RegistrationStatusResponse("target-sub", "Doctor", "rejected", "Not qualified");
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.RejectAsync("admin-sub", "target-sub", It.IsAny<RejectRegistrationRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.RejectRegistrationAsync(
      "target-sub", new RejectRegistrationRequest("Not qualified"), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task RejectRegistrationAsync_ShouldReturnNotFound_WhenUserMissingAsync()
  {
    var approvalService = new Mock<IRegistrationApprovalService>();
    approvalService
      .Setup(x => x.RejectAsync("admin-sub", "missing-user", It.IsAny<RejectRegistrationRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("User not found."));

    var controller = CreateController(approvalService.Object, CreateUser("admin-sub"));
    var result = await controller.RejectRegistrationAsync(
      "missing-user", new RejectRegistrationRequest("Not valid"), CancellationToken.None);

    result.Should().BeOfType<NotFoundObjectResult>();
  }

  #endregion

  #region Helpers

  private static RegistrationApprovalController CreateController(
    IRegistrationApprovalService approvalService,
    ClaimsPrincipal user)
  {
    return new RegistrationApprovalController(approvalService)
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
