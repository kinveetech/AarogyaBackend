using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ConsentRecordsByUserSpecification : BaseSpecification<ConsentRecord>
{
  public ConsentRecordsByUserSpecification(Guid userId)
    : base(record => record.UserId == userId)
  {
    ApplyOrderByDescending(record => record.OccurredAt);
  }
}
