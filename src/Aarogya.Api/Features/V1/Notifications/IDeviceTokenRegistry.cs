namespace Aarogya.Api.Features.V1.Notifications;

internal interface IDeviceTokenRegistry
{
  public Task<IReadOnlyList<DeviceTokenRegistrationResponse>> ListByUserAsync(
    string userSub,
    CancellationToken cancellationToken = default);

  public Task<DeviceTokenRegistrationResponse> UpsertAsync(
    string userSub,
    RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken = default);

  public Task<bool> RemoveAsync(
    string userSub,
    string deviceToken,
    CancellationToken cancellationToken = default);

  public Task<IReadOnlyList<string>> GetDeviceTokensAsync(
    string userSub,
    CancellationToken cancellationToken = default);
}
