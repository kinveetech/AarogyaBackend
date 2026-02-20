using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by feature service constructor injection and unit tests.")]
public interface ICriticalSmsNotificationService
{
  public Task SendEmergencyAccessEventAsync(
    User patient,
    EmergencyContact contact,
    string action,
    CancellationToken cancellationToken = default);

  public Task SendEmergencyAccessRequestedAsync(
    User patient,
    EmergencyContact contact,
    User doctor,
    AccessGrant grant,
    CancellationToken cancellationToken = default);

  public Task SendEmergencyAccessExpiringSoonAsync(
    User patient,
    User doctor,
    AccessGrant grant,
    CancellationToken cancellationToken = default);
}
