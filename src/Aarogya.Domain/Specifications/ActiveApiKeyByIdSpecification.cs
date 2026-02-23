using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ActiveApiKeyByIdSpecification : BaseSpecification<ApiKey>
{
  public ActiveApiKeyByIdSpecification(Guid keyId, DateTimeOffset now)
    : base(key =>
      key.Id == keyId
      && !key.IsRevoked
      && key.ExpiresAt > now)
  {
  }
}
