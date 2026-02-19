using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class UserByExternalAuthIdSpecification : BaseSpecification<User>
{
  public UserByExternalAuthIdSpecification(string externalAuthId)
    : base(user => user.ExternalAuthId == externalAuthId)
  {
    ApplyAsNoTracking();
  }
}
