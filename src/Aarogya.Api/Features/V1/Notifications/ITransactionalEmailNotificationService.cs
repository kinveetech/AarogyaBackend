using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by service constructors and unit-test mocking.")]
public interface ITransactionalEmailNotificationService
{
  public Task SendReportUploadedAsync(
    User patient,
    Report report,
    CancellationToken cancellationToken = default);

  public Task SendAccessGrantedAsync(
    User patient,
    User doctor,
    AccessGrant grant,
    CancellationToken cancellationToken = default);

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
