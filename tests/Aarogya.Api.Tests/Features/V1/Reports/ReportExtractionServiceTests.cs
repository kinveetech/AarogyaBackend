using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class ReportExtractionServiceTests
{
  private readonly Mock<IUserRepository> _userRepository = new();
  private readonly Mock<IReportRepository> _reportRepository = new();
  private readonly Mock<IAccessGrantRepository> _accessGrantRepository = new();
  private readonly Mock<IReportPdfExtractionProcessor> _processor = new();
  private readonly Mock<IUtcClock> _clock = new();
  private readonly ReportExtractionService _sut;

  private static readonly Guid UserId = Guid.NewGuid();
  private static readonly Guid ReportId = Guid.NewGuid();
  private const string UserSub = "test-user-sub";

  public ReportExtractionServiceTests()
  {
    _clock.Setup(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
    _sut = new ReportExtractionService(
      _userRepository.Object,
      _reportRepository.Object,
      _accessGrantRepository.Object,
      _processor.Object,
      _clock.Object);
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldThrow_WhenUserNotFoundAsync()
  {
    _userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(UserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var act = () => _sut.GetExtractionStatusAsync(UserSub, ReportId);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*not provisioned*");
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldThrow_WhenReportNotFoundAsync()
  {
    SetupUser(UserRole.Patient);
    _reportRepository
      .Setup(x => x.FirstOrDefaultAsync(
        It.IsAny<ReportByIdSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var act = () => _sut.GetExtractionStatusAsync(UserSub, ReportId);

    await act.Should().ThrowAsync<KeyNotFoundException>()
      .WithMessage("*not found*");
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldThrow_WhenPatientDoesNotOwnReportAsync()
  {
    SetupUser(UserRole.Patient);
    SetupReport(patientId: Guid.NewGuid());

    var act = () => _sut.GetExtractionStatusAsync(UserSub, ReportId);

    await act.Should().ThrowAsync<UnauthorizedAccessException>()
      .WithMessage("*do not have access*");
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldReturnNull_WhenNoExtractionMetadataAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, extraction: null);

    var result = await _sut.GetExtractionStatusAsync(UserSub, ReportId);

    result.Should().BeNull();
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldReturnStatus_WhenExtractionExistsAsync()
  {
    var user = SetupUser(UserRole.Patient);
    var extraction = new ExtractionMetadata
    {
      ExtractionMethod = "pdfpig",
      StructuringModel = "test-model",
      ExtractedParameterCount = 5,
      OverallConfidence = 0.85,
      PageCount = 2,
      ExtractedAt = DateTimeOffset.UtcNow,
      AttemptCount = 1
    };
    SetupReport(patientId: user.Id, extraction: extraction, status: ReportStatus.Extracted);

    var result = await _sut.GetExtractionStatusAsync(UserSub, ReportId);

    result.Should().NotBeNull();
    result!.ReportId.Should().Be(ReportId);
    result.ExtractionMethod.Should().Be("pdfpig");
    result.ExtractedParameterCount.Should().Be(5);
    result.OverallConfidence.Should().Be(0.85);
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldAllowLabTech_WhenUploadedByThemAsync()
  {
    var user = SetupUser(UserRole.LabTechnician);
    var extraction = new ExtractionMetadata { ExtractionMethod = "pdfpig", AttemptCount = 1 };
    SetupReport(uploadedByUserId: user.Id, extraction: extraction, status: ReportStatus.Extracted);

    var result = await _sut.GetExtractionStatusAsync(UserSub, ReportId);

    result.Should().NotBeNull();
  }

  [Fact]
  public async Task GetExtractionStatusAsync_ShouldAllowDoctor_WhenHasActiveGrantAsync()
  {
    var user = SetupUser(UserRole.Doctor);
    var patientId = Guid.NewGuid();
    var extraction = new ExtractionMetadata { ExtractionMethod = "pdfpig", AttemptCount = 1 };
    SetupReport(patientId: patientId, extraction: extraction, status: ReportStatus.Extracted);

    var grant = new AccessGrant { PatientId = patientId, GrantedToUserId = user.Id };
    _accessGrantRepository
      .Setup(x => x.ListAsync(
        It.IsAny<ActiveAccessGrantsForDoctorSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync([grant]);

    var result = await _sut.GetExtractionStatusAsync(UserSub, ReportId);

    result.Should().NotBeNull();
  }

  [Fact]
  public async Task TriggerExtractionAsync_ShouldThrow_WhenNoUploadedFileAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, fileStorageKey: null);

    var act = () => _sut.TriggerExtractionAsync(UserSub, ReportId);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*no uploaded file*");
  }

  [Fact]
  public async Task TriggerExtractionAsync_ShouldThrow_WhenStatusNotAllowedAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, status: ReportStatus.Extracting);

    var act = () => _sut.TriggerExtractionAsync(UserSub, ReportId);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*cannot be extracted*");
  }

  [Fact]
  public async Task TriggerExtractionAsync_ShouldCallProcessor_WhenStatusCleanAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, status: ReportStatus.Clean);

    _processor
      .Setup(x => x.ProcessReportAsync(
        ReportId,
        false,
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await _sut.TriggerExtractionAsync(UserSub, ReportId);

    _processor.Verify(
      x => x.ProcessReportAsync(ReportId, false, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task TriggerExtractionAsync_ShouldCallProcessor_WhenStatusExtractionFailedAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, status: ReportStatus.ExtractionFailed);

    _processor
      .Setup(x => x.ProcessReportAsync(
        ReportId,
        false,
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await _sut.TriggerExtractionAsync(UserSub, ReportId);

    _processor.Verify(
      x => x.ProcessReportAsync(ReportId, false, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task TriggerExtractionAsync_ShouldAllow_WhenForceReprocessOnExtractedStatusAsync()
  {
    var user = SetupUser(UserRole.Patient);
    SetupReport(patientId: user.Id, status: ReportStatus.Extracted);

    _processor
      .Setup(x => x.ProcessReportAsync(
        ReportId,
        true,
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await _sut.TriggerExtractionAsync(UserSub, ReportId, forceReprocess: true);

    _processor.Verify(
      x => x.ProcessReportAsync(ReportId, true, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  private User SetupUser(UserRole role)
  {
    var user = new User
    {
      Id = UserId,
      ExternalAuthId = UserSub,
      Role = role
    };
    _userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(UserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);
    return user;
  }

  private void SetupReport(
    Guid? patientId = null,
    Guid? uploadedByUserId = null,
    ExtractionMetadata? extraction = null,
    ReportStatus status = ReportStatus.Clean,
    string? fileStorageKey = "test-key.pdf")
  {
    var report = new Report
    {
      Id = ReportId,
      PatientId = patientId ?? Guid.NewGuid(),
      UploadedByUserId = uploadedByUserId ?? Guid.NewGuid(),
      Status = status,
      FileStorageKey = fileStorageKey,
      Extraction = extraction
    };
    _reportRepository
      .Setup(x => x.FirstOrDefaultAsync(
        It.IsAny<ReportByIdSpecification>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);
  }
}
