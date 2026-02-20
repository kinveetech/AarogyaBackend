using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.AccessGrants;

internal sealed class AccessGrantService(
  IUserRepository userRepository,
  IReportRepository reportRepository,
  IAccessGrantRepository accessGrantRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  IOptions<AccessGrantOptions> options,
  IUtcClock clock)
  : IAccessGrantService
{
  private readonly AccessGrantOptions _options = options.Value;

  public async Task<IReadOnlyList<AccessGrantResponse>> GetForPatientAsync(string patientSub, CancellationToken cancellationToken = default)
  {
    var patient = await ResolvePatientAsync(patientSub, cancellationToken);
    var now = clock.UtcNow;
    var grants = await accessGrantRepository.ListAsync(new AccessGrantsByPatientSpecification(patient.Id), cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      patient,
      "access_grant.list.patient",
      "access_grant",
      null,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["count"] = grants.Count.ToString()
      },
      cancellationToken);

    return grants
      .Where(grant => grant.Status == AccessGrantStatus.Active
        && grant.StartsAt <= now
        && (grant.ExpiresAt == null || grant.ExpiresAt > now))
      .Select(MapGrant)
      .ToArray();
  }

  public async Task<IReadOnlyList<AccessGrantResponse>> GetForDoctorAsync(string doctorSub, CancellationToken cancellationToken = default)
  {
    var doctor = await userRepository.GetByExternalAuthIdAsync(doctorSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated doctor user is not provisioned in the database.");
    if (doctor.Role != UserRole.Doctor)
    {
      throw new InvalidOperationException("Only doctor users can list received grants.");
    }

    var grants = await accessGrantRepository.ListAsync(
      new ActiveAccessGrantsByDoctorSpecification(doctor.Id, clock.UtcNow),
      cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      doctor,
      "access_grant.list.doctor",
      "access_grant",
      null,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["count"] = grants.Count.ToString()
      },
      cancellationToken);

    return grants.Select(MapGrant).ToArray();
  }

  public async Task<AccessGrantResponse> CreateAsync(string patientSub, CreateAccessGrantRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var patient = await ResolvePatientAsync(patientSub, cancellationToken);
    var doctorSub = InputSanitizer.SanitizePlainText(request.DoctorSub);
    var doctor = await userRepository.GetByExternalAuthIdAsync(doctorSub, cancellationToken)
      ?? throw new InvalidOperationException("DoctorSub does not match a provisioned user.");
    if (doctor.Role != UserRole.Doctor)
    {
      throw new InvalidOperationException("DoctorSub must belong to a doctor user.");
    }
    if (string.Equals(patient.ExternalAuthId, doctor.ExternalAuthId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("Cannot grant access to self.");
    }

    var now = clock.UtcNow;
    var maxExpiresAt = now.AddDays(_options.MaxExpiryDays);
    var expiresAt = request.ExpiresAt ?? now.AddDays(_options.DefaultExpiryDays);
    if (expiresAt <= now)
    {
      throw new InvalidOperationException("ExpiresAt must be in the future.");
    }

    if (expiresAt > maxExpiresAt)
    {
      throw new InvalidOperationException($"ExpiresAt cannot exceed {_options.MaxExpiryDays} days from now.");
    }

    var reportIds = request.AllReports
      ? Array.Empty<Guid>()
      : (request.ReportIds ?? [])
        .Where(id => id != Guid.Empty)
        .Distinct()
        .ToArray();

    if (!request.AllReports)
    {
      if (reportIds.Length == 0)
      {
        throw new InvalidOperationException("At least one report ID is required when AllReports is false.");
      }

      var patientReportIds = (await reportRepository.ListByPatientAsync(patient.Id, cancellationToken))
        .Select(report => report.Id)
        .ToHashSet();

      var invalidReportIds = reportIds.Where(reportId => !patientReportIds.Contains(reportId)).ToArray();
      if (invalidReportIds.Length > 0)
      {
        throw new InvalidOperationException("All report IDs must belong to the patient.");
      }
    }

    var existingGrant = await accessGrantRepository.GetActiveGrantAsync(patient.Id, doctor.Id, cancellationToken);
    if (existingGrant is not null)
    {
      existingGrant.Status = AccessGrantStatus.Revoked;
      existingGrant.RevokedAt = now;
      accessGrantRepository.Update(existingGrant);
    }

    var grant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patient.Id,
      GrantedToUserId = doctor.Id,
      GrantedByUserId = patient.Id,
      GrantReason = InputSanitizer.SanitizePlainText(request.Purpose),
      Scope = new AccessGrantScope
      {
        CanReadReports = true,
        CanDownloadReports = true,
        AllowedReportIds = reportIds,
        AllowedReportTypes = []
      },
      Status = AccessGrantStatus.Active,
      StartsAt = now,
      ExpiresAt = expiresAt,
      CreatedAt = now
    };

    await accessGrantRepository.AddAsync(grant, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      patient,
      "access_grant.created",
      "access_grant",
      grant.Id,
      201,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["doctorUserId"] = doctor.Id.ToString("D"),
        ["allReports"] = request.AllReports.ToString()
      },
      cancellationToken);

    return new AccessGrantResponse(
      grant.Id,
      patient.ExternalAuthId ?? string.Empty,
      doctor.ExternalAuthId ?? string.Empty,
      request.AllReports,
      reportIds,
      grant.GrantReason ?? string.Empty,
      grant.StartsAt,
      grant.ExpiresAt ?? expiresAt,
      false);
  }

  public async Task<bool> RevokeAsync(string patientSub, Guid grantId, CancellationToken cancellationToken = default)
  {
    var patient = await ResolvePatientAsync(patientSub, cancellationToken);
    var grant = await accessGrantRepository.FirstOrDefaultAsync(
      new AccessGrantByIdForPatientSpecification(patient.Id, grantId),
      cancellationToken);
    if (grant is null || grant.Status != AccessGrantStatus.Active)
    {
      return false;
    }

    grant.Status = AccessGrantStatus.Revoked;
    grant.RevokedAt = clock.UtcNow;
    accessGrantRepository.Update(grant);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      patient,
      "access_grant.revoked",
      "access_grant",
      grant.Id,
      200,
      cancellationToken: cancellationToken);
    return true;
  }

  private async Task<User> ResolvePatientAsync(string patientSub, CancellationToken cancellationToken)
  {
    var patient = await userRepository.GetByExternalAuthIdAsync(patientSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated patient user is not provisioned in the database.");

    if (patient.Role != UserRole.Patient)
    {
      throw new InvalidOperationException("Only patient users can manage access grants.");
    }

    return patient;
  }

  private static AccessGrantResponse MapGrant(AccessGrant grant)
  {
    var reportIds = grant.Scope.AllowedReportIds?.Distinct().ToArray() ?? [];
    var allReports = reportIds.Length == 0;

    return new AccessGrantResponse(
      grant.Id,
      grant.Patient.ExternalAuthId ?? string.Empty,
      grant.GrantedToUser.ExternalAuthId ?? string.Empty,
      allReports,
      reportIds,
      grant.GrantReason ?? string.Empty,
      grant.StartsAt,
      grant.ExpiresAt ?? grant.StartsAt,
      grant.Status != AccessGrantStatus.Active);
  }
}
