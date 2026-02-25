using System.Security.Claims;
using Aarogya.Api.Authentication;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class RegistrationRequiredMiddlewareTests
{
  #region Exempt Paths

  [Theory]
  [InlineData("/api/auth/login")]
  [InlineData("/health")]
  [InlineData("/health/ready")]
  [InlineData("/swagger")]
  [InlineData("/api/v1/users/register")]
  [InlineData("/api/v1/users/me/registration-status")]
  public async Task InvokeAsync_ShouldPassThrough_ForExemptPathsAsync(string path)
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateAuthenticatedContext("user-sub", path);
    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
  }

  [Fact]
  public async Task InvokeAsync_ShouldPassThrough_WhenUnauthenticatedAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = new DefaultHttpContext();
    context.User = new ClaimsPrincipal(new ClaimsIdentity());
    context.Request.Path = "/api/v1/reports";

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
  }

  [Fact]
  public async Task InvokeAsync_ShouldPassThrough_ForLabPrefixedSubAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var context = CreateAuthenticatedContext("lab:api-key-sub", "/api/v1/reports");
    var serviceProvider = new Mock<IServiceProvider>();
    context.RequestServices = serviceProvider.Object;

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
  }

  #endregion

  #region Registration Required

  [Fact]
  public async Task InvokeAsync_ShouldReturn403_WhenUserNotFoundAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("unknown-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var context = CreateAuthenticatedContext("unknown-sub", "/api/v1/reports");
    SetupServiceProvider(context, userRepo.Object);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeFalse();
    context.Response.StatusCode.Should().Be(403);
    var body = await ReadResponseBodyAsync(context);
    body.Should().Contain("registration_required");
  }

  [Fact]
  public async Task InvokeAsync_ShouldReturn403_WhenRegistrationPendingAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "pending-sub",
      Role = UserRole.Doctor,
      RegistrationStatus = RegistrationStatus.PendingApproval,
      FirstName = "Test",
      LastName = "Doctor",
      Email = "doc@aarogya.dev"
    };

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("pending-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var context = CreateAuthenticatedContext("pending-sub", "/api/v1/reports");
    SetupServiceProvider(context, userRepo.Object);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeFalse();
    context.Response.StatusCode.Should().Be(403);
    var body = await ReadResponseBodyAsync(context);
    body.Should().Contain("registration_pending_approval");
  }

  [Fact]
  public async Task InvokeAsync_ShouldReturn403_WhenRegistrationRejectedAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "rejected-sub",
      Role = UserRole.Doctor,
      RegistrationStatus = RegistrationStatus.Rejected,
      FirstName = "Test",
      LastName = "Doctor",
      Email = "doc@aarogya.dev"
    };

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("rejected-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var context = CreateAuthenticatedContext("rejected-sub", "/api/v1/reports");
    SetupServiceProvider(context, userRepo.Object);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeFalse();
    context.Response.StatusCode.Should().Be(403);
    var body = await ReadResponseBodyAsync(context);
    body.Should().Contain("registration_rejected");
  }

  [Fact]
  public async Task InvokeAsync_ShouldPassThrough_WhenApprovedUserAsync()
  {
    var nextCalled = false;
    var middleware = new RegistrationRequiredMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    });

    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "approved-sub",
      Role = UserRole.Patient,
      RegistrationStatus = RegistrationStatus.Approved,
      FirstName = "Test",
      LastName = "Patient",
      Email = "patient@aarogya.dev"
    };

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("approved-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var context = CreateAuthenticatedContext("approved-sub", "/api/v1/reports");
    SetupServiceProvider(context, userRepo.Object);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
  }

  #endregion

  #region Helpers

  private static DefaultHttpContext CreateAuthenticatedContext(string sub, string path)
  {
    var context = new DefaultHttpContext();
    context.User = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", sub)
    ], "TestAuth"));
    context.Request.Path = path;
    context.Response.Body = new MemoryStream();
    return context;
  }

  private static void SetupServiceProvider(DefaultHttpContext context, IUserRepository userRepo)
  {
    var serviceProvider = new Mock<IServiceProvider>();
    serviceProvider
      .Setup(sp => sp.GetService(typeof(IUserRepository)))
      .Returns(userRepo);
    context.RequestServices = serviceProvider.Object;
  }

  private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext context)
  {
    context.Response.Body.Seek(0, SeekOrigin.Begin);
    using var reader = new StreamReader(context.Response.Body);
    return await reader.ReadToEndAsync();
  }

  #endregion
}
