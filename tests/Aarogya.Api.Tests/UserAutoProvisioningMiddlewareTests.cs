using System.Security.Claims;
using Aarogya.Api.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserAutoProvisioningMiddlewareTests
{
  [Fact]
  public async Task InvokeAsync_ShouldCallService_WhenAuthenticatedAsync()
  {
    var provisioningService = new Mock<IUserAutoProvisioningService>();
    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider.Setup(sp => sp.GetService(typeof(IUserAutoProvisioningService)))
      .Returns(provisioningService.Object);

    var httpContext = new DefaultHttpContext
    {
      User = new ClaimsPrincipal(new ClaimsIdentity(
      [
        new Claim("sub", "user-123")
      ], "TestAuth")),
      RequestServices = serviceProvider.Object
    };

    var nextCalled = false;
    var middleware = new UserAutoProvisioningMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    await middleware.InvokeAsync(httpContext);

    provisioningService.Verify(s => s.EnsureUserExistsAsync(
      It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    nextCalled.Should().BeTrue();
  }

  [Fact]
  public async Task InvokeAsync_ShouldSkipService_WhenUnauthenticatedAsync()
  {
    var provisioningService = new Mock<IUserAutoProvisioningService>();
    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider.Setup(sp => sp.GetService(typeof(IUserAutoProvisioningService)))
      .Returns(provisioningService.Object);

    var httpContext = new DefaultHttpContext
    {
      User = new ClaimsPrincipal(new ClaimsIdentity()),
      RequestServices = serviceProvider.Object
    };

    var nextCalled = false;
    var middleware = new UserAutoProvisioningMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    await middleware.InvokeAsync(httpContext);

    provisioningService.Verify(s => s.EnsureUserExistsAsync(
      It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    nextCalled.Should().BeTrue();
  }

  [Fact]
  public async Task InvokeAsync_ShouldContinue_WhenServiceNotRegisteredAsync()
  {
    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider.Setup(sp => sp.GetService(typeof(IUserAutoProvisioningService)))
      .Returns(null!);

    var httpContext = new DefaultHttpContext
    {
      User = new ClaimsPrincipal(new ClaimsIdentity(
      [
        new Claim("sub", "user-123")
      ], "TestAuth")),
      RequestServices = serviceProvider.Object
    };

    var nextCalled = false;
    var middleware = new UserAutoProvisioningMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    await middleware.InvokeAsync(httpContext);

    nextCalled.Should().BeTrue();
  }
}
