using System.Collections.Concurrent;
using Aarogya.Api.Authentication;
using Aarogya.Api.Security;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class InMemoryDeviceTokenRegistry(IUtcClock clock) : IDeviceTokenRegistry
{
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DeviceRegistration>> _store =
    new(StringComparer.Ordinal);

  public Task<IReadOnlyList<DeviceTokenRegistrationResponse>> ListByUserAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    if (!_store.TryGetValue(userSub, out var registrations))
    {
      return Task.FromResult<IReadOnlyList<DeviceTokenRegistrationResponse>>([]);
    }

    var results = registrations.Values
      .OrderByDescending(x => x.UpdatedAt)
      .Select(Map)
      .ToArray();

    return Task.FromResult<IReadOnlyList<DeviceTokenRegistrationResponse>>(results);
  }

  public Task<DeviceTokenRegistrationResponse> UpsertAsync(
    string userSub,
    RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken = default)
  {
    var userDevices = _store.GetOrAdd(userSub, _ => new ConcurrentDictionary<string, DeviceRegistration>(StringComparer.Ordinal));
    var now = clock.UtcNow;

    var registration = userDevices.AddOrUpdate(
      request.DeviceToken,
      _ => new DeviceRegistration(
        Guid.NewGuid(),
        request.DeviceToken,
        request.Platform.Trim(),
        InputSanitizer.SanitizeNullablePlainText(request.DeviceName),
        InputSanitizer.SanitizeNullablePlainText(request.AppVersion),
        now,
        now),
      (_, existing) => existing with
      {
        Platform = request.Platform.Trim(),
        DeviceName = InputSanitizer.SanitizeNullablePlainText(request.DeviceName),
        AppVersion = InputSanitizer.SanitizeNullablePlainText(request.AppVersion),
        UpdatedAt = now
      });

    return Task.FromResult(Map(registration));
  }

  public Task<bool> RemoveAsync(
    string userSub,
    string deviceToken,
    CancellationToken cancellationToken = default)
  {
    if (!_store.TryGetValue(userSub, out var registrations))
    {
      return Task.FromResult(false);
    }

    var removed = registrations.TryRemove(deviceToken, out _);
    return Task.FromResult(removed);
  }

  public Task<IReadOnlyList<string>> GetDeviceTokensAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    if (!_store.TryGetValue(userSub, out var registrations))
    {
      return Task.FromResult<IReadOnlyList<string>>([]);
    }

    var tokens = registrations.Keys.ToArray();
    return Task.FromResult<IReadOnlyList<string>>(tokens);
  }

  private static DeviceTokenRegistrationResponse Map(DeviceRegistration registration)
  {
    return new DeviceTokenRegistrationResponse(
      registration.RegistrationId,
      registration.DeviceToken,
      registration.Platform,
      registration.DeviceName,
      registration.AppVersion,
      registration.RegisteredAt,
      registration.UpdatedAt);
  }

  private sealed record DeviceRegistration(
    Guid RegistrationId,
    string DeviceToken,
    string Platform,
    string? DeviceName,
    string? AppVersion,
    DateTimeOffset RegisteredAt,
    DateTimeOffset UpdatedAt);
}
