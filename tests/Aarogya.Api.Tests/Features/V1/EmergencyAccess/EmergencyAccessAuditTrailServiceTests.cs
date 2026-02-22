using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.EmergencyAccess;

public sealed class EmergencyAccessAuditTrailServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 21, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task QueryAsync_ShouldReturnAllLogs_WhenNoFiltersAppliedAsync()
  {
    var logs = CreateAuditLogs(5);
    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest();
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(5);
    result.Items.Should().HaveCount(5);
    result.Page.Should().Be(1);
  }

  [Fact]
  public async Task QueryAsync_ShouldFilterByGrantId_WhenGrantIdProvidedAsync()
  {
    var grantId = Guid.NewGuid();
    var logs = new[]
    {
      CreateAuditLog(entityId: grantId, data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["grantId"] = grantId.ToString()
      }),
      CreateAuditLog(entityId: Guid.NewGuid(), data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["grantId"] = Guid.NewGuid().ToString()
      }),
      CreateAuditLog(entityId: grantId, data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
    };

    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest(GrantId: grantId);
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(2);
    result.Items.Should().AllSatisfy(item =>
      item.GrantId.Should().Be(grantId));
  }

  [Fact]
  public async Task QueryAsync_ShouldFilterByPatientSub_WhenPatientSubProvidedAsync()
  {
    var patientId = Guid.NewGuid();
    var otherActorId = Guid.NewGuid();
    var logs = new[]
    {
      CreateAuditLog(actorUserId: patientId),
      CreateAuditLog(actorUserId: otherActorId),
      CreateAuditLog(
        actorUserId: Guid.NewGuid(),
        data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["patientUserId"] = patientId.ToString()
        })
    };

    var (sut, _, userRepository) = CreateService(logs);
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("patient-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User { Id = patientId, ExternalAuthId = "patient-sub", Role = UserRole.Patient });

    var request = new EmergencyAccessAuditQueryRequest(PatientSub: "patient-sub");
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(2);
  }

  [Fact]
  public async Task QueryAsync_ShouldFilterByDoctorSub_WhenDoctorSubProvidedAsync()
  {
    var doctorId = Guid.NewGuid();
    var logs = new[]
    {
      CreateAuditLog(actorUserId: doctorId),
      CreateAuditLog(actorUserId: Guid.NewGuid()),
      CreateAuditLog(
        actorUserId: Guid.NewGuid(),
        data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["doctorUserId"] = doctorId.ToString()
        })
    };

    var (sut, _, userRepository) = CreateService(logs);
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("doctor-sub", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User { Id = doctorId, ExternalAuthId = "doctor-sub", Role = UserRole.Doctor });

    var request = new EmergencyAccessAuditQueryRequest(DoctorSub: "doctor-sub");
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(2);
  }

  [Fact]
  public async Task QueryAsync_ShouldPaginateResults_WhenPageAndPageSizeProvidedAsync()
  {
    var logs = CreateAuditLogs(25);
    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest(Page: 2, PageSize: 10);
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.Page.Should().Be(2);
    result.PageSize.Should().Be(10);
    result.TotalCount.Should().Be(25);
    result.Items.Should().HaveCount(10);
  }

  [Fact]
  public async Task QueryAsync_ShouldReturnLastPage_WhenPageExceedsTotalAsync()
  {
    var logs = CreateAuditLogs(5);
    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest(Page: 3, PageSize: 10);
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(5);
    result.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task QueryAsync_ShouldClampPageSizeTo200Async()
  {
    var logs = CreateAuditLogs(5);
    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest(PageSize: 500);
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.PageSize.Should().Be(200);
  }

  [Fact]
  public async Task QueryAsync_ShouldReturnEmptyItems_WhenNoLogsMatchAsync()
  {
    var (sut, _, _) = CreateService([]);

    var request = new EmergencyAccessAuditQueryRequest(GrantId: Guid.NewGuid());
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.TotalCount.Should().Be(0);
    result.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task QueryAsync_ShouldThrow_WhenRequestIsNullAsync()
  {
    var (sut, _, _) = CreateService([]);

    var act = async () => await sut.QueryAsync(null!, CancellationToken.None);
    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task QueryAsync_ShouldHandleMinimumPageValue_WhenPageIsZeroAsync()
  {
    var logs = CreateAuditLogs(5);
    var (sut, _, _) = CreateService(logs);

    var request = new EmergencyAccessAuditQueryRequest(Page: 0, PageSize: 10);
    var result = await sut.QueryAsync(request, CancellationToken.None);

    result.Page.Should().Be(1);
    result.Items.Should().HaveCount(5);
  }

  private static (EmergencyAccessAuditTrailService Sut, Mock<IAuditLogRepository> AuditLogRepo, Mock<IUserRepository> UserRepo)
    CreateService(IReadOnlyList<AuditLog> logs)
  {
    var auditLogRepository = new Mock<IAuditLogRepository>();
    auditLogRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AuditLog>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(logs);

    var userRepository = new Mock<IUserRepository>();

    var sut = new EmergencyAccessAuditTrailService(auditLogRepository.Object, userRepository.Object);
    return (sut, auditLogRepository, userRepository);
  }

  private static AuditLog[] CreateAuditLogs(int count)
  {
    return Enumerable.Range(0, count)
      .Select(_ => CreateAuditLog())
      .ToArray();
  }

  private static AuditLog CreateAuditLog(
    Guid? actorUserId = null,
    Guid? entityId = null,
    Dictionary<string, string>? data = null)
  {
    var id = entityId ?? Guid.NewGuid();
    return new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = actorUserId ?? Guid.NewGuid(),
      ActorRole = UserRole.Doctor,
      Action = "emergency_access.granted",
      EntityType = "emergency_access_grant",
      EntityId = id,
      OccurredAt = FixedNow.AddMinutes(-((id.GetHashCode() & 0x7FFFFFFF) % 59 + 1)),
      ResultStatus = 200,
      Details = new AuditLogDetails
      {
        Summary = "Emergency access granted",
        Data = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["grantId"] = id.ToString()
        }
      }
    };
  }
}
