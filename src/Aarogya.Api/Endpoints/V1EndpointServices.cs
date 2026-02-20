using System.Collections.Concurrent;
using System.Security.Claims;

namespace Aarogya.Api.Endpoints;

internal interface IUsersEndpointService
{
  public UserProfileResponse GetCurrentUser(ClaimsPrincipal principal);
}

internal interface IReportsEndpointService
{
  public Task<IReadOnlyList<ReportSummaryResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<ReportSummaryResponse> AddForUserAsync(string userSub, CreateReportRequest request, CancellationToken cancellationToken = default);
}

internal interface IAccessGrantsEndpointService
{
  public Task<IReadOnlyList<AccessGrantResponse>> GetForPatientAsync(string patientSub, CancellationToken cancellationToken = default);

  public Task<AccessGrantResponse> CreateAsync(string patientSub, CreateAccessGrantRequest request, CancellationToken cancellationToken = default);

  public Task<bool> RevokeAsync(string patientSub, Guid grantId, CancellationToken cancellationToken = default);
}

internal interface IEmergencyContactsEndpointService
{
  public Task<IReadOnlyList<EmergencyContactResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<EmergencyContactResponse> AddForUserAsync(string userSub, CreateEmergencyContactRequest request, CancellationToken cancellationToken = default);

  public Task<bool> DeleteForUserAsync(string userSub, Guid contactId, CancellationToken cancellationToken = default);
}

internal sealed class InMemoryUsersEndpointService : IUsersEndpointService
{
  public UserProfileResponse GetCurrentUser(ClaimsPrincipal principal)
  {
    var sub = principal.FindFirstValue("sub") ?? string.Empty;
    var email = principal.FindFirstValue("email") ?? string.Empty;
    var roles = principal.Claims
      .Where(claim => claim.Type is ClaimTypes.Role or "role" or "cognito:groups")
      .Select(claim => claim.Value)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return new UserProfileResponse(sub, email, roles);
  }
}

internal sealed class InMemoryReportsEndpointService : IReportsEndpointService
{
  private readonly ConcurrentDictionary<string, List<ReportSummaryResponse>> _reportsByUser = new(StringComparer.Ordinal);

  public Task<IReadOnlyList<ReportSummaryResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);

    lock (reports)
    {
      return Task.FromResult<IReadOnlyList<ReportSummaryResponse>>(reports.OrderByDescending(report => report.CreatedAt).ToArray());
    }
  }

  public Task<ReportSummaryResponse> AddForUserAsync(string userSub, CreateReportRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);
    var created = new ReportSummaryResponse(Guid.NewGuid(), request.Title.Trim(), "uploaded", DateTimeOffset.UtcNow);

    lock (reports)
    {
      reports.Add(created);
    }

    return Task.FromResult(created);
  }
}

internal sealed class InMemoryAccessGrantsEndpointService : IAccessGrantsEndpointService
{
  private readonly ConcurrentDictionary<string, List<AccessGrantResponse>> _grantsByPatient = new(StringComparer.Ordinal);

  public Task<IReadOnlyList<AccessGrantResponse>> GetForPatientAsync(string patientSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var grants = _grantsByPatient.GetOrAdd(patientSub, static _ => []);

    lock (grants)
    {
      return Task.FromResult<IReadOnlyList<AccessGrantResponse>>(grants.OrderByDescending(grant => grant.ExpiresAt).ToArray());
    }
  }

  public Task<AccessGrantResponse> CreateAsync(string patientSub, CreateAccessGrantRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var grants = _grantsByPatient.GetOrAdd(patientSub, static _ => []);
    var created = new AccessGrantResponse(
      Guid.NewGuid(),
      request.DoctorSub.Trim(),
      request.ReportIds.Distinct().ToArray(),
      request.ExpiresAt,
      false);

    lock (grants)
    {
      grants.Add(created);
    }

    return Task.FromResult(created);
  }

  public Task<bool> RevokeAsync(string patientSub, Guid grantId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var grants = _grantsByPatient.GetOrAdd(patientSub, static _ => []);
    lock (grants)
    {
      var index = grants.FindIndex(grant => grant.GrantId == grantId && !grant.Revoked);
      if (index < 0)
      {
        return Task.FromResult(false);
      }

      var existing = grants[index];
      grants[index] = existing with { Revoked = true };
      return Task.FromResult(true);
    }
  }
}

internal sealed class InMemoryEmergencyContactsEndpointService : IEmergencyContactsEndpointService
{
  private readonly ConcurrentDictionary<string, List<EmergencyContactResponse>> _contactsByUser = new(StringComparer.Ordinal);

  public Task<IReadOnlyList<EmergencyContactResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    lock (contacts)
    {
      return Task.FromResult<IReadOnlyList<EmergencyContactResponse>>(contacts.ToArray());
    }
  }

  public Task<EmergencyContactResponse> AddForUserAsync(string userSub, CreateEmergencyContactRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    var created = new EmergencyContactResponse(
      Guid.NewGuid(),
      request.Name.Trim(),
      request.PhoneNumber.Trim(),
      request.Relationship.Trim());

    lock (contacts)
    {
      contacts.Add(created);
    }

    return Task.FromResult(created);
  }

  public Task<bool> DeleteForUserAsync(string userSub, Guid contactId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    lock (contacts)
    {
      var removed = contacts.RemoveAll(contact => contact.ContactId == contactId) > 0;
      return Task.FromResult(removed);
    }
  }
}
