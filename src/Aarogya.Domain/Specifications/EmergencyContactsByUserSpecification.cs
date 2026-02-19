using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class EmergencyContactsByUserSpecification : BaseSpecification<EmergencyContact>
{
  public EmergencyContactsByUserSpecification(Guid userId)
    : base(contact => contact.UserId == userId)
  {
    ApplyOrderByDescending(contact => contact.IsPrimary);
    ApplyAsNoTracking();
  }
}
