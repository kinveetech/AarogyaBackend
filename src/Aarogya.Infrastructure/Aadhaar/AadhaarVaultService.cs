using System.Text.Json;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Infrastructure.Persistence;

namespace Aarogya.Infrastructure.Aadhaar;

public sealed class AadhaarVaultService(
  IAadhaarVaultRepository aadhaarVaultRepository,
  AarogyaDbContext dbContext,
  IMockAadhaarApiClient mockAadhaarApiClient)
  : IAadhaarVaultService
{
  private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

  public async Task<AadhaarVerificationResult> VerifyAndCreateReferenceTokenAsync(
    string aadhaarNumber,
    Guid? actorUserId = null,
    string? firstName = null,
    string? lastName = null,
    DateOnly? dateOfBirth = null,
    CancellationToken cancellationToken = default)
  {
    var normalizedAadhaar = AadhaarHashing.Normalize(aadhaarNumber);

    var validation = await mockAadhaarApiClient.ValidateAsync(normalizedAadhaar, firstName, lastName, dateOfBirth, cancellationToken);
    if (!validation.IsValid)
    {
      throw new InvalidOperationException(validation.Message ?? "Aadhaar validation failed.");
    }

    var aadhaarSha256 = AadhaarHashing.ComputeSha256(normalizedAadhaar);
    var existing = await aadhaarVaultRepository.GetBySha256Async(aadhaarSha256, cancellationToken);
    if (existing is not null)
    {
      var isUpdated = TryHydrateExistingDemographics(existing, validation);
      await WriteAccessAuditLogAsync(existing.ReferenceToken, actorUserId, "lookup_existing", 200, new { validation.RequestId }, cancellationToken);

      if (isUpdated)
      {
        await dbContext.SaveChangesAsync(cancellationToken);
      }

      return new AadhaarVerificationResult(
        existing.ReferenceToken,
        true,
        existing.VerificationProvider,
        ReadDemographics(existing.DemographicsJson),
        validation.RequestId);
    }

    var tokenResponse = await mockAadhaarApiClient.TokenizeAsync(aadhaarSha256, cancellationToken);
    var referenceToken = tokenResponse.ReferenceToken == Guid.Empty ? Guid.NewGuid() : tokenResponse.ReferenceToken;

    var vaultRecord = new AadhaarVaultRecord
    {
      Id = Guid.NewGuid(),
      ReferenceToken = referenceToken,
      AadhaarNumber = normalizedAadhaar,
      AadhaarSha256 = aadhaarSha256,
      ProviderRequestId = tokenResponse.RequestId ?? validation.RequestId,
      VerificationProvider = validation.Provider,
      DemographicsJson = SerializeDemographics(validation.Demographics)
    };

    await aadhaarVaultRepository.AddAsync(vaultRecord, cancellationToken);

    await WriteAccessAuditLogAsync(referenceToken, actorUserId, "create_token", 201, new
    {
      ValidationRequestId = validation.RequestId,
      TokenRequestId = tokenResponse.RequestId
    }, cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);

    return new AadhaarVerificationResult(
      referenceToken,
      false,
      validation.Provider,
      validation.Demographics,
      tokenResponse.RequestId ?? validation.RequestId);
  }

  public async Task<Guid> CreateOrGetReferenceTokenAsync(
    string aadhaarNumber,
    Guid? actorUserId = null,
    string? firstName = null,
    string? lastName = null,
    DateOnly? dateOfBirth = null,
    CancellationToken cancellationToken = default)
  {
    var verification = await VerifyAndCreateReferenceTokenAsync(aadhaarNumber, actorUserId, firstName, lastName, dateOfBirth, cancellationToken);
    return verification.ReferenceToken;
  }

  public async Task<string?> GetAadhaarByReferenceTokenAsync(
    Guid referenceToken,
    Guid? actorUserId = null,
    CancellationToken cancellationToken = default)
  {
    var record = await aadhaarVaultRepository.GetByReferenceTokenAsync(referenceToken, cancellationToken);

    await WriteAccessAuditLogAsync(referenceToken, actorUserId, "read", record is null ? 404 : 200, null, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    return record?.AadhaarNumber;
  }

  private async Task WriteAccessAuditLogAsync(
    Guid referenceToken,
    Guid? actorUserId,
    string action,
    int resultStatus,
    object? details,
    CancellationToken cancellationToken)
  {
    var log = new AadhaarVaultAccessLog
    {
      Id = Guid.NewGuid(),
      ReferenceToken = referenceToken,
      OccurredAt = DateTimeOffset.UtcNow,
      ActorUserId = actorUserId,
      Action = action,
      ResultStatus = resultStatus,
      Details = details is null ? null : JsonSerializer.Serialize(details)
    };

    await dbContext.AadhaarVaultAccessLogs.AddAsync(log, cancellationToken);
  }

  private static string? SerializeDemographics(MockAadhaarDemographics? demographics)
    => demographics is null ? null : JsonSerializer.Serialize(demographics, JsonSerializerOptions);

  private static MockAadhaarDemographics? ReadDemographics(string? demographicsJson)
    => string.IsNullOrWhiteSpace(demographicsJson)
      ? null
      : JsonSerializer.Deserialize<MockAadhaarDemographics>(demographicsJson, JsonSerializerOptions);

  private static bool TryHydrateExistingDemographics(AadhaarVaultRecord existing, MockAadhaarValidationResponse validation)
  {
    var changed = false;

    if (!string.IsNullOrWhiteSpace(validation.Provider) && string.IsNullOrWhiteSpace(existing.VerificationProvider))
    {
      existing.VerificationProvider = validation.Provider;
      changed = true;
    }

    if (validation.Demographics is not null && string.IsNullOrWhiteSpace(existing.DemographicsJson))
    {
      existing.DemographicsJson = SerializeDemographics(validation.Demographics);
      changed = true;
    }

    if (changed)
    {
      existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    return changed;
  }
}
