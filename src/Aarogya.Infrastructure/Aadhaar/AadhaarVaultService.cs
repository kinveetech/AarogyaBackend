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
  public async Task<Guid> CreateOrGetReferenceTokenAsync(
    string aadhaarNumber,
    Guid? actorUserId = null,
    CancellationToken cancellationToken = default)
  {
    var normalizedAadhaar = AadhaarHashing.Normalize(aadhaarNumber);

    var validation = await mockAadhaarApiClient.ValidateAsync(normalizedAadhaar, cancellationToken);
    if (!validation.IsValid)
    {
      throw new InvalidOperationException(validation.Message ?? "Aadhaar validation failed.");
    }

    var aadhaarSha256 = AadhaarHashing.ComputeSha256(normalizedAadhaar);

    var existing = await aadhaarVaultRepository.GetBySha256Async(aadhaarSha256, cancellationToken);
    if (existing is not null)
    {
      await WriteAccessAuditLogAsync(existing.ReferenceToken, actorUserId, "lookup_existing", 200, new { validation.RequestId }, cancellationToken);
      return existing.ReferenceToken;
    }

    var tokenResponse = await mockAadhaarApiClient.TokenizeAsync(aadhaarSha256, cancellationToken);
    var referenceToken = tokenResponse.ReferenceToken == Guid.Empty ? Guid.NewGuid() : tokenResponse.ReferenceToken;

    var vaultRecord = new AadhaarVaultRecord
    {
      Id = Guid.NewGuid(),
      ReferenceToken = referenceToken,
      AadhaarNumber = normalizedAadhaar,
      AadhaarSha256 = aadhaarSha256,
      ProviderRequestId = tokenResponse.RequestId ?? validation.RequestId
    };

    await aadhaarVaultRepository.AddAsync(vaultRecord, cancellationToken);

    await WriteAccessAuditLogAsync(referenceToken, actorUserId, "create_token", 201, new
    {
      ValidationRequestId = validation.RequestId,
      TokenRequestId = tokenResponse.RequestId
    }, cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);

    return referenceToken;
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
}
