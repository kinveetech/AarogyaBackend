using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class UsersByRegistrationStatusSpecification : BaseSpecification<User>
{
  public UsersByRegistrationStatusSpecification(RegistrationStatus status)
    : base(user => user.RegistrationStatus == status)
  {
    ApplyAsNoTracking();
    ApplyOrderByDescending(user => user.CreatedAt);
  }
}
