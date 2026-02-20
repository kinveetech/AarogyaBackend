using System.Collections.Concurrent;

namespace Aarogya.Api.Features.V1.AccessGrants;

internal sealed class InMemoryAccessGrantService : IAccessGrantService
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
