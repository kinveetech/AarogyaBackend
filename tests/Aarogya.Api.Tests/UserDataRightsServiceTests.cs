using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserDataRightsServiceTests
{
  [Fact]
  public async Task ExportCurrentUserDataAsync_ShouldReturnPortablePayloadAndLogRequestAsync()
  {
    var user = CreateUser("seed-PATIENT-1");
    var report = new Report
    {
      Id = Guid.NewGuid(),
      PatientId = user.Id,
      UploadedByUserId = user.Id,
      ReportNumber = "RPT-001",
      ReportType = ReportType.BloodTest,
      Status = ReportStatus.Published,
      UploadedAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)
    };

    var accessGrant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = user.Id,
      GrantedByUserId = user.Id,
      GrantedToUserId = Guid.NewGuid(),
      Status = AccessGrantStatus.Active,
      StartsAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)
    };

    var emergencyContact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      Name = "Primary Contact",
      Relationship = "Father",
      Phone = "+919900001111",
      CreatedAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)
    };

    var consent = new ConsentRecord
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      Purpose = "reports.share",
      IsGranted = true,
      Source = "api",
      OccurredAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)
    };

    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = user.Id,
      Action = "reports.created",
      EntityType = "report",
      ResultStatus = 200,
      OccurredAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)
    };

    var userRepository = new Mock<IUserRepository>();
    var reportRepository = new Mock<IReportRepository>();
    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    var consentRecordRepository = new Mock<IConsentRecordRepository>();
    var auditLogRepository = new Mock<IAuditLogRepository>();
    var unitOfWork = new Mock<IUnitOfWork>();
    var auditLoggingService = new Mock<IAuditLoggingService>();
    var clock = new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));

    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);
    reportRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>())).ReturnsAsync([report]);
    accessGrantRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>())).ReturnsAsync([accessGrant]);
    emergencyContactRepository.Setup(x => x.ListByUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync([emergencyContact]);
    consentRecordRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<ConsentRecord>>(), It.IsAny<CancellationToken>())).ReturnsAsync([consent]);
    auditLogRepository.Setup(x => x.ListByActorAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync([auditLog]);

    var service = new UserDataRightsService(
      userRepository.Object,
      reportRepository.Object,
      accessGrantRepository.Object,
      emergencyContactRepository.Object,
      consentRecordRepository.Object,
      auditLogRepository.Object,
      unitOfWork.Object,
      auditLoggingService.Object,
      clock);

    var response = await service.ExportCurrentUserDataAsync("seed-PATIENT-1", CancellationToken.None);

    response.Profile.UserId.Should().Be(user.Id);
    response.Reports.Should().HaveCount(1);
    response.AccessGrants.Should().HaveCount(1);
    response.EmergencyContacts.Should().HaveCount(1);
    response.Consents.Should().HaveCount(1);
    response.AuditLogs.Should().HaveCount(1);

    auditLoggingService.Verify(x => x.LogDataAccessAsync(
      user,
      "user_data.export_requested",
      "user",
      user.Id,
      200,
      It.IsAny<IReadOnlyDictionary<string, string>>(),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task DeleteCurrentUserDataAsync_ShouldThrow_WhenConfirmationFlagIsFalseAsync()
  {
    var service = CreateService();

    var action = async () => await service.DeleteCurrentUserDataAsync(
      "seed-PATIENT-1",
      new DataDeletionRequest(false, "test"),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ConfirmPermanentDeletion*");
  }

  [Fact]
  public async Task DeleteCurrentUserDataAsync_ShouldAnonymizeAndDeleteRelatedDataAsync()
  {
    var now = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);
    var user = CreateUser("seed-PATIENT-1");
    var report = new Report
    {
      Id = Guid.NewGuid(),
      PatientId = user.Id,
      UploadedByUserId = user.Id,
      DoctorId = user.Id,
      ReportNumber = "RPT-DEL-001",
      ReportType = ReportType.BloodTest,
      Status = ReportStatus.Published,
      UploadedAt = now.AddDays(-1),
      FileStorageKey = "reports/sample.pdf",
      ChecksumSha256 = "abc",
      Results = new ReportResults
      {
        Notes = "contains pii",
        Parameters = [new ReportResultParameter { Code = "HB", Name = "Hemoglobin", Value = 12.3m }]
      },
      Metadata = new ReportMetadata
      {
        Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["email"] = "patient@aarogya.dev"
        }
      },
      Parameters = [new ReportParameter { ParameterCode = "HB", ParameterName = "Hemoglobin", MeasuredValueNumeric = 12.3m }]
    };

    var grant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = user.Id,
      GrantedToUserId = Guid.NewGuid(),
      GrantedByUserId = user.Id,
      StartsAt = now.AddDays(-2),
      Status = AccessGrantStatus.Active
    };

    var contact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      Name = "Parent",
      Relationship = "Mother",
      Phone = "+919900001111"
    };

    var consent = new ConsentRecord
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      Purpose = "reports.share",
      IsGranted = true,
      Source = "api",
      OccurredAt = now.AddDays(-1)
    };

    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = user.Id,
      UserAgent = "agent",
      Action = "reports.created",
      EntityType = "report",
      ClientIp = System.Net.IPAddress.Parse("10.0.0.1"),
      Details = new AuditLogDetails
      {
        Summary = "before",
        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["email"] = "patient@aarogya.dev",
          ["event"] = "create"
        }
      }
    };

    var userRepository = new Mock<IUserRepository>();
    var reportRepository = new Mock<IReportRepository>();
    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    var consentRecordRepository = new Mock<IConsentRecordRepository>();
    var auditLogRepository = new Mock<IAuditLogRepository>();
    var unitOfWork = new Mock<IUnitOfWork>();
    var auditLoggingService = new Mock<IAuditLoggingService>();

    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);
    reportRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>())).ReturnsAsync([report]);
    accessGrantRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>())).ReturnsAsync([grant]);
    emergencyContactRepository.Setup(x => x.ListByUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync([contact]);
    consentRecordRepository.Setup(x => x.ListAsync(It.IsAny<ISpecification<ConsentRecord>>(), It.IsAny<CancellationToken>())).ReturnsAsync([consent]);
    auditLogRepository.Setup(x => x.ListByActorAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync([auditLog]);

    var service = new UserDataRightsService(
      userRepository.Object,
      reportRepository.Object,
      accessGrantRepository.Object,
      emergencyContactRepository.Object,
      consentRecordRepository.Object,
      auditLogRepository.Object,
      unitOfWork.Object,
      auditLoggingService.Object,
      new FixedUtcClock(now));

    var response = await service.DeleteCurrentUserDataAsync(
      "seed-PATIENT-1",
      new DataDeletionRequest(true, "requested by user"),
      CancellationToken.None);

    user.ExternalAuthId.Should().BeNull();
    user.Email.Should().StartWith("deleted-");
    user.IsActive.Should().BeFalse();

    report.IsDeleted.Should().BeTrue();
    report.FileStorageKey.Should().BeNull();
    report.ChecksumSha256.Should().BeNull();
    report.Results.Parameters.Should().BeEmpty();
    report.Metadata.Tags.Should().BeEmpty();
    report.Parameters.Should().BeEmpty();

    auditLog.ActorUserId.Should().BeNull();
    auditLog.UserAgent.Should().BeNull();
    auditLog.ClientIp.Should().BeNull();
    auditLog.Details.Data["email"].Should().Be("[REDACTED]");

    response.AffectedRecords["users"].Should().Be(1);
    response.AffectedRecords["reports"].Should().Be(1);
    response.AffectedRecords["auditLogsAnonymized"].Should().Be(1);
    response.RetentionExceptions.Should().NotBeEmpty();

    reportRepository.Verify(x => x.Update(report), Times.Once);
    accessGrantRepository.Verify(x => x.Delete(grant), Times.Once);
    emergencyContactRepository.Verify(x => x.Delete(contact), Times.Once);
    consentRecordRepository.Verify(x => x.Delete(consent), Times.Once);
    auditLogRepository.Verify(x => x.Update(auditLog), Times.Once);
    userRepository.Verify(x => x.Update(user), Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    auditLoggingService.Verify(x => x.LogDataAccessAsync(
      user,
      "user_data.deletion_requested",
      "user",
      user.Id,
      200,
      It.IsAny<IReadOnlyDictionary<string, string>>(),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  private static UserDataRightsService CreateService()
  {
    return new UserDataRightsService(
      Mock.Of<IUserRepository>(),
      Mock.Of<IReportRepository>(),
      Mock.Of<IAccessGrantRepository>(),
      Mock.Of<IEmergencyContactRepository>(),
      Mock.Of<IConsentRecordRepository>(),
      Mock.Of<IAuditLogRepository>(),
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero)));
  }

  private static User CreateUser(string sub)
  {
    return new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = sub,
      Role = UserRole.Patient,
      FirstName = "Test",
      LastName = "Patient",
      Email = "patient@aarogya.dev",
      Phone = "+919999999999",
      IsActive = true
    };
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
