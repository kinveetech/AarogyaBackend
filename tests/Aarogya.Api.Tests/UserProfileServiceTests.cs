using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserProfileServiceTests
{
  [Fact]
  public async Task UpdateCurrentUserAsync_ShouldUpdateOnlyProvidedFieldsAsync()
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Before",
      LastName = "Patient",
      Email = "before@aarogya.dev",
      Phone = "+919876543210",
      Address = "Old Address",
      BloodGroup = "O+",
      DateOfBirth = new DateOnly(1990, 1, 1),
      CreatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
      UpdatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
    };

    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var unitOfWork = new Mock<IUnitOfWork>();

    var clock = new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero));

    var service = new UserProfileService(repository.Object, unitOfWork.Object, clock);

    var response = await service.UpdateCurrentUserAsync(
      "seed-PATIENT-1",
      new UpdateUserProfileRequest(
        " After ",
        null,
        " after@aarogya.dev ",
        null,
        " New Address ",
        " ab+ ",
        null),
      CancellationToken.None);

    user.FirstName.Should().Be("After");
    user.LastName.Should().Be("Patient");
    user.Email.Should().Be("after@aarogya.dev");
    user.Address.Should().Be("New Address");
    user.BloodGroup.Should().Be("AB+");
    user.Phone.Should().Be("+919876543210");
    user.UpdatedAt.Should().Be(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero));

    response.FirstName.Should().Be("After");
    response.BloodGroup.Should().Be("AB+");

    repository.Verify(x => x.Update(user), Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetCurrentUserAsync_ShouldThrow_WhenUserNotFoundAsync()
  {
    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("missing-sub", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

    var service = new UserProfileService(
      repository.Object,
      Mock.Of<IUnitOfWork>(),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero)));

    var action = async () => await service.GetCurrentUserAsync("missing-sub", CancellationToken.None);

    await action.Should().ThrowAsync<KeyNotFoundException>();
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
