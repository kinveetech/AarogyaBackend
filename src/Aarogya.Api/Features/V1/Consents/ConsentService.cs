using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Features.V1.Consents;

internal sealed class ConsentService(
  IUserRepository userRepository,
  IConsentRecordRepository consentRecordRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  IUtcClock clock)
  : IConsentService
{
  public async Task<IReadOnlyList<ConsentRecordResponse>> GetCurrentForUserAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var user = await ResolveUserAsync(userSub, cancellationToken);
    var latest = await consentRecordRepository.ListLatestByUserAsync(user.Id, cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      user,
      "consent.listed",
      "consent_record",
      null,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["count"] = latest.Count.ToString()
      },
      cancellationToken);

    return latest.Select(Map).ToArray();
  }

  public async Task<ConsentRecordResponse> UpsertForUserAsync(
    string userSub,
    string purpose,
    UpsertConsentRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var user = await ResolveUserAsync(userSub, cancellationToken);
    var normalizedPurpose = NormalizeAndValidatePurpose(purpose);
    var source = InputSanitizer.SanitizeNullablePlainText(request.Source)?.Trim();
    if (string.IsNullOrWhiteSpace(source))
    {
      source = "api";
    }

    var now = clock.UtcNow;
    var record = new ConsentRecord
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      Purpose = normalizedPurpose,
      IsGranted = request.IsGranted,
      Source = source,
      OccurredAt = now,
      CreatedAt = now,
      UpdatedAt = now
    };

    await consentRecordRepository.AddAsync(record, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await auditLoggingService.LogDataAccessAsync(
      user,
      request.IsGranted ? "consent.granted" : "consent.withdrawn",
      "consent_record",
      record.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["purpose"] = normalizedPurpose,
        ["source"] = source
      },
      cancellationToken);

    return Map(record);
  }

  public async Task EnsureGrantedAsync(string userSub, string purpose, CancellationToken cancellationToken = default)
  {
    var user = await ResolveUserAsync(userSub, cancellationToken);
    var normalizedPurpose = NormalizeAndValidatePurpose(purpose);

    var granted = await consentRecordRepository.IsGrantedAsync(user.Id, normalizedPurpose, cancellationToken);
    if (granted)
    {
      return;
    }

    await auditLoggingService.LogDataAccessAsync(
      user,
      "consent.denied",
      "consent_record",
      null,
      403,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["purpose"] = normalizedPurpose
      },
      cancellationToken);

    throw new ConsentRequiredException(normalizedPurpose);
  }

  private static ConsentRecordResponse Map(ConsentRecord record)
    => new(record.Purpose, record.IsGranted, record.OccurredAt, record.Source);

  private static string NormalizeAndValidatePurpose(string purpose)
  {
    var normalizedPurpose = InputSanitizer.SanitizePlainText(purpose).Trim();
    if (!ConsentPurposeCatalog.IsSupported(normalizedPurpose))
    {
      throw new InvalidOperationException($"Unsupported consent purpose '{normalizedPurpose}'.");
    }

    return ConsentPurposeCatalog.All.First(
      x => string.Equals(x, normalizedPurpose, StringComparison.OrdinalIgnoreCase));
  }

  private async Task<User> ResolveUserAsync(string userSub, CancellationToken cancellationToken)
  {
    return await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new KeyNotFoundException("Authenticated user is not provisioned in the database.");
  }
}
