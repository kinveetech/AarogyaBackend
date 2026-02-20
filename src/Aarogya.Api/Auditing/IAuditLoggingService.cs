using Aarogya.Domain.Entities;

namespace Aarogya.Api.Auditing;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Exposed for unit test mocking and composition root registration.")]
public interface IAuditLoggingService
{
  public Task LogDataAccessAsync(
    User actor,
    string action,
    string resourceType,
    Guid? resourceId,
    int resultStatus,
    IReadOnlyDictionary<string, string>? data = null,
    CancellationToken cancellationToken = default);
}
