using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.EmergencyAccess;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class EmergencyAccessControllerTests
{
  [Fact]
  public async Task CreateEmergencyAccessRequestAsync_ShouldReturnCreated_WhenValidAsync()
  {
    var service = new Mock<IEmergencyAccessService>();
    var auditTrailService = new Mock<IEmergencyAccessAuditTrailService>();
    var response = new EmergencyAccessResponse(
      Guid.NewGuid(),
      "seed-PATIENT-1",
      "seed-DOCTOR-1",
      Guid.NewGuid(),
      DateTimeOffset.UtcNow,
      DateTimeOffset.UtcNow.AddHours(24),
      "emergency:accident");
    service
      .Setup(x => x.RequestAsync(It.IsAny<CreateEmergencyAccessRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = new EmergencyAccessController(service.Object, auditTrailService.Object);

    var result = await controller.CreateEmergencyAccessRequestAsync(
      new CreateEmergencyAccessRequest("seed-PATIENT-1", "+919876543210", "seed-DOCTOR-1", "accident", 24),
      CancellationToken.None);

    var created = result.Should().BeOfType<CreatedResult>().Subject;
    created.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task CreateEmergencyAccessRequestAsync_ShouldReturnBadRequest_WhenInvalidAsync()
  {
    var service = new Mock<IEmergencyAccessService>();
    var auditTrailService = new Mock<IEmergencyAccessAuditTrailService>();
    service
      .Setup(x => x.RequestAsync(It.IsAny<CreateEmergencyAccessRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Only registered emergency contacts can request emergency access."));

    var controller = new EmergencyAccessController(service.Object, auditTrailService.Object);

    var result = await controller.CreateEmergencyAccessRequestAsync(
      new CreateEmergencyAccessRequest("seed-PATIENT-1", "+919876543210", "seed-DOCTOR-1", "accident", 24),
      CancellationToken.None);

    result.Should().BeOfType<BadRequestObjectResult>();
  }

  [Fact]
  public async Task GetEmergencyAccessAuditAsync_ShouldReturnOk_WhenServiceSucceedsAsync()
  {
    var service = new Mock<IEmergencyAccessService>();
    var auditTrailService = new Mock<IEmergencyAccessAuditTrailService>();
    var response = new EmergencyAccessAuditTrailResponse(
      1,
      50,
      1,
      [
        new EmergencyAccessAuditEventResponse(
          Guid.NewGuid(),
          DateTimeOffset.UtcNow,
          "emergency_access.requested",
          Guid.NewGuid(),
          Guid.NewGuid(),
          "Patient",
          "access_grant",
          Guid.NewGuid(),
          new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
      ]);
    auditTrailService
      .Setup(x => x.QueryAsync(It.IsAny<EmergencyAccessAuditQueryRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = new EmergencyAccessController(service.Object, auditTrailService.Object);
    var result = await controller.GetEmergencyAccessAuditAsync(new EmergencyAccessAuditQueryRequest(), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }
}
