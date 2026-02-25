using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IUserRegistrationService
{
  public Task<RegisterUserResponse> RegisterAsync(
    string userSub,
    RegisterUserRequest request,
    CancellationToken cancellationToken = default);

  public Task<RegistrationStatusResponse> GetRegistrationStatusAsync(
    string userSub,
    CancellationToken cancellationToken = default);
}
