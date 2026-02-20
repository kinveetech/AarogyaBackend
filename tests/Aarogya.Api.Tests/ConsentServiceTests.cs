using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ConsentServiceTests
{
  [Fact]
  public async Task UpsertForUserAsync_ShouldPersistConsentRecordAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var now = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    ConsentRecord? created = null;
    var consentRepository = new Mock<IConsentRecordRepository>();
    consentRepository
      .Setup(x => x.AddAsync(It.IsAny<ConsentRecord>(), It.IsAny<CancellationToken>()))
      .Callback<ConsentRecord, CancellationToken>((record, _) => created = record)
      .Returns(Task.CompletedTask);

    var unitOfWork = new Mock<IUnitOfWork>();
    var service = new ConsentService(
      userRepository.Object,
      consentRepository.Object,
      unitOfWork.Object,
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(now));

    var result = await service.UpsertForUserAsync(
      user.ExternalAuthId!,
      ConsentPurposeCatalog.ProfileManagement,
      new UpsertConsentRequest(true, "settings"),
      CancellationToken.None);

    result.Purpose.Should().Be(ConsentPurposeCatalog.ProfileManagement);
    result.IsGranted.Should().BeTrue();
    result.Source.Should().Be("settings");
    created.Should().NotBeNull();
    created!.UserId.Should().Be(user.Id);
    created.Purpose.Should().Be(ConsentPurposeCatalog.ProfileManagement);
    created.IsGranted.Should().BeTrue();
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnsureGrantedAsync_ShouldThrow_WhenConsentMissingAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var consentRepository = new Mock<IConsentRecordRepository>();
    consentRepository
      .Setup(x => x.IsGrantedAsync(user.Id, ConsentPurposeCatalog.MedicalRecordsProcessing, It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var service = new ConsentService(
      userRepository.Object,
      consentRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.EnsureGrantedAsync(
      user.ExternalAuthId!,
      ConsentPurposeCatalog.MedicalRecordsProcessing,
      CancellationToken.None);

    await action.Should().ThrowAsync<ConsentRequiredException>();
  }

  [Fact]
  public async Task GetCurrentForUserAsync_ShouldReturnLatestPerPurposeAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var now = DateTimeOffset.UtcNow;
    var consentRepository = new Mock<IConsentRecordRepository>();
    consentRepository
      .Setup(x => x.ListLatestByUserAsync(user.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        new ConsentRecord
        {
          Id = Guid.NewGuid(),
          UserId = user.Id,
          Purpose = ConsentPurposeCatalog.ProfileManagement,
          IsGranted = true,
          Source = "api",
          OccurredAt = now
        },
        new ConsentRecord
        {
          Id = Guid.NewGuid(),
          UserId = user.Id,
          Purpose = ConsentPurposeCatalog.MedicalRecordsProcessing,
          IsGranted = false,
          Source = "api",
          OccurredAt = now
        }
      ]);

    var service = new ConsentService(
      userRepository.Object,
      consentRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(now));

    var result = await service.GetCurrentForUserAsync(user.ExternalAuthId!, CancellationToken.None);

    result.Should().HaveCount(2);
    result.Should().Contain(x => x.Purpose == ConsentPurposeCatalog.ProfileManagement && x.IsGranted);
    result.Should().Contain(x => x.Purpose == ConsentPurposeCatalog.MedicalRecordsProcessing && !x.IsGranted);
  }

  [Fact]
  public async Task UpsertForUserAsync_ShouldDefaultSourceToApi_WhenSourceIsBlankAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    ConsentRecord? created = null;
    var consentRepository = new Mock<IConsentRecordRepository>();
    consentRepository
      .Setup(x => x.AddAsync(It.IsAny<ConsentRecord>(), It.IsAny<CancellationToken>()))
      .Callback<ConsentRecord, CancellationToken>((record, _) => created = record)
      .Returns(Task.CompletedTask);

    var service = new ConsentService(
      userRepository.Object,
      consentRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var result = await service.UpsertForUserAsync(
      user.ExternalAuthId!,
      ConsentPurposeCatalog.MedicalDataSharing,
      new UpsertConsentRequest(true, "   "),
      CancellationToken.None);

    result.Source.Should().Be("api");
    created.Should().NotBeNull();
    created!.Source.Should().Be("api");
  }

  [Fact]
  public async Task EnsureGrantedAsync_ShouldNotThrow_WhenConsentExistsAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var consentRepository = new Mock<IConsentRecordRepository>();
    consentRepository
      .Setup(x => x.IsGrantedAsync(user.Id, ConsentPurposeCatalog.ProfileManagement, It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var service = new ConsentService(
      userRepository.Object,
      consentRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.EnsureGrantedAsync(
      user.ExternalAuthId!,
      ConsentPurposeCatalog.ProfileManagement,
      CancellationToken.None);

    await action.Should().NotThrowAsync();
  }

  [Fact]
  public async Task EnsureGrantedAsync_ShouldRejectUnsupportedPurposeAsync()
  {
    var user = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-user-1" };
    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync(user.ExternalAuthId!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var service = new ConsentService(
      userRepository.Object,
      Mock.Of<IConsentRecordRepository>(),
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.EnsureGrantedAsync(
      user.ExternalAuthId!,
      "unsupported-purpose",
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Unsupported consent purpose*");
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
