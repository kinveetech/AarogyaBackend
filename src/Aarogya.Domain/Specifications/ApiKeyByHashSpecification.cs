using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ApiKeyByHashSpecification : BaseSpecification<ApiKey>
{
  public ApiKeyByHashSpecification(string keyHash)
    : base(key => key.KeyHash == keyHash)
  {
    ApplyAsNoTracking();
  }
}
