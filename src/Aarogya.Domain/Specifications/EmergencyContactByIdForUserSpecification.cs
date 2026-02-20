using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class EmergencyContactByIdForUserSpecification : BaseSpecification<EmergencyContact>
{
  public EmergencyContactByIdForUserSpecification(Guid userId, Guid contactId)
    : base(contact => contact.UserId == userId && contact.Id == contactId)
  {
  }
}
