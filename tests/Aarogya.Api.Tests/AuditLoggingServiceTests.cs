using System.Net;
using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AuditLoggingServiceTests
{
  [Fact]
  public async Task LogDataAccessAsync_ShouldPersistStructuredAuditRecordAsync()
  {
    var actor = new User
    {
      Id = Guid.NewGuid(),
      Role = UserRole.Patient
    };
    var occurredAt = new DateTimeOffset(2026, 2, 20, 17, 0, 0, TimeSpan.Zero);

    var httpContext = new DefaultHttpContext();
    httpContext.TraceIdentifier = Guid.NewGuid().ToString("D");
    httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.24");
    httpContext.Request.Method = HttpMethods.Get;
    httpContext.Request.Path = "/api/v1/users/me";
    httpContext.Request.Headers.UserAgent = "UnitTestAgent/1.0";
    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

    var auditLogRepository = new Mock<IAuditLogRepository>();
    AuditLog? created = null;
    auditLogRepository
      .Setup(x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
      .Callback<AuditLog, CancellationToken>((auditLog, _) => created = auditLog)
      .Returns(Task.CompletedTask);

    var unitOfWork = new Mock<IUnitOfWork>();
    var service = new AuditLoggingService(
      auditLogRepository.Object,
      unitOfWork.Object,
      httpContextAccessor,
      new FixedUtcClock(occurredAt),
      NullLogger<AuditLoggingService>.Instance);

    await service.LogDataAccessAsync(
      actor,
      "user_profile.read",
      "user",
      actor.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["resourceId"] = actor.Id.ToString("D")
      },
      CancellationToken.None);

    created.Should().NotBeNull();
    created!.ActorUserId.Should().Be(actor.Id);
    created.Action.Should().Be("user_profile.read");
    created.EntityType.Should().Be("user");
    created.ClientIp.Should().Be(IPAddress.Parse("10.0.0.24"));
    created.RequestPath.Should().Be("/api/v1/users/me");
    created.RequestMethod.Should().Be(HttpMethods.Get);
    created.Details.Data.Should().ContainKey("occurredAtUtc");
    created.Details.Data.Should().ContainKey("occurredAtIst");
    created.Details.Data.Should().ContainKey("resourceId");

    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
